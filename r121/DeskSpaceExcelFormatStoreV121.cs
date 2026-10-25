using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

// Dedicated, per-user encrypted format library. Never uses the header/value
// the header/value library file; old mapping records are neither migrated nor overwritten.
internal static class DeskSpaceExcelFormatStoreV121
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob {public int cbData; public IntPtr pbData;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input,string? description,
        IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob result);
    [DllImport("crypt32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input,IntPtr desc,
        IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob result);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);

    private static readonly object Gate=new();
    private static readonly string Folder=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","ExcelMappings");
    private static readonly string Store=Path.Combine(Folder,"formats-v121.dpapi");

    private static byte[] Protect(byte[] raw,bool decrypt)
    {
        var input=new Blob{cbData=raw.Length,pbData=Marshal.AllocHGlobal(raw.Length)};
        Marshal.Copy(raw,0,input.pbData,raw.Length);
        try
        {
            Blob output;
            bool ok=decrypt
                ?CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,
                    IntPtr.Zero,IntPtr.Zero,1,out output)
                :CryptProtectData(ref input,"DeskSpace format library",
                    IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new InvalidOperationException(
                "Windows encrypted format library operation failed ("+
                Marshal.GetLastWin32Error()+").");
            try
            {
                var bytes=new byte[output.cbData];
                Marshal.Copy(output.pbData,bytes,0,bytes.Length);
                return bytes;
            }
            finally {LocalFree(output.pbData);}
        }
        finally {Marshal.FreeHGlobal(input.pbData);}
    }

    // Strip values, source labels, screenshot bytes and legacy value actions.
    // The style visual retains the exact R1C1 formulas (required on blank
    // destination), native dimensions, underline, table and number formats.
    internal static DeskSpaceExcelTemplateV16 OnlyFormatting(
        DeskSpaceExcelTemplateV16 captured,string name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length>140)
            throw new InvalidOperationException(
                "Enter a format template name (1-140 characters).");
        if(captured.SheetVisualV101 is not {} view ||
           view.Rows<1 || view.Columns<1 ||
           view.Rows*(long)view.Columns>1800 ||
           view.Cells.Count!=view.Rows*view.Columns)
            throw new InvalidDataException(
                "The captured Excel format is incomplete; nothing was saved.");
        var output=JsonSerializer.Deserialize<DeskSpaceExcelTemplateV16>(
            JsonSerializer.Serialize(captured)) ??
            throw new InvalidDataException("Cannot copy format snapshot.");
        output.Name=name.Trim();
        output.Cells.Clear();
        output.Anchors.Clear();
        output.ScreenshotBase64="";
        output.ScreenshotSelection="";
        output.GroupPath="";
        return output;
    }
    internal static List<DeskSpaceExcelTemplateV16> Load()
    {
        lock(Gate)
        {
            if(!File.Exists(Store))return new();
            var plain=Protect(File.ReadAllBytes(Store),true);
            try
            {
                var models=JsonSerializer.Deserialize<List<DeskSpaceExcelTemplateV16>>(
                    Encoding.UTF8.GetString(plain)) ??
                    throw new InvalidDataException("Empty Excel format library.");
                if(models.Any(x=>x.Cells.Count>0 || x.Anchors.Count>0 ||
                    x.SheetVisualV101==null))
                    throw new InvalidDataException(
                        "Invalid format-only library. No changes were made.");
                return models;
            }
            finally {Array.Clear(plain);}
        }
    }
    internal static DeskSpaceExcelTemplateV16 Save(
        DeskSpaceExcelTemplateV16 source,string name)
    {
        lock(Gate)
        {
            var entry=OnlyFormatting(source,name);
            // Reread the disk on EACH write. A second DeskSpace process must
            // not silently overwrite another entry with a stale in-memory list.
            var entries=Load();
            int idx=entries.FindIndex(x=>x.Id==entry.Id);
            if(idx>=0)entries[idx]=entry;
            else entries.Add(entry);
            if(entries.Count>250)
                throw new InvalidOperationException("Excel format library is full (250).");
            Directory.CreateDirectory(Folder);
            var clear=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries));
            byte[] encrypted;
            try{encrypted=Protect(clear,false);}
            finally {Array.Clear(clear);}
            string tmp=Store+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllBytes(tmp,encrypted);
                if(File.Exists(Store))File.Copy(Store,Store+".previous",true);
                File.Move(tmp,Store,true);
            }
            finally {if(File.Exists(tmp))File.Delete(tmp);}
            return entry;
        }
    }
}
