using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

internal sealed class DeskSpaceExcelCellV16
{
    public int RowOffset {get;set;}
    public int ColumnOffset {get;set;}
    public string ValueKind {get;set;}="";
    public string RawValue {get;set;}="";
    public string Display {get;set;}="";
    public string FormulaR1C1 {get;set;}="";
    public string NumberFormat {get;set;}="";
}
internal sealed class DeskSpaceExcelAnchorV16
{
    public int RowOffset {get;set;}
    public int ColumnOffset {get;set;}
    public string Label {get;set;}="";
}
internal sealed class DeskSpaceExcelTemplateV16
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public string SourceWorkbook {get;set;}="";
    public string SourceSheet {get;set;}="";
    public string SourceAddress {get;set;}="";
    public int SourceRow {get;set;}
    public int SourceColumn {get;set;}
    public int RowCount {get;set;}
    public int ColumnCount {get;set;}
    public DateTime CreatedUtc {get;set;}=DateTime.UtcNow;
    public List<DeskSpaceExcelCellV16> Cells {get;set;}=new();
    public List<DeskSpaceExcelAnchorV16> Anchors {get;set;}=new();
    // The source screenshot is a visual reference, not an OCR substitute for Excel cells.
    public string ScreenshotBase64 {get;set;}="";
    public string ScreenshotSelection {get;set;}="";
}
internal static class DeskSpaceExcelStoreV16
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob {public int cbData;public IntPtr pbData;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob source,string? description,
        IntPtr optionalEntropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob result);
    [DllImport("crypt32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob source,
        IntPtr description,IntPtr optionalEntropy,IntPtr reserved,IntPtr prompt,
        uint flags,out Blob result);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static readonly string Folder=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","ExcelMappings");
    private static readonly string PathName=Path.Combine(Folder,"templates.dpapi");
    private static byte[] Crypt(byte[] bytes,bool decrypt)
    {
        var input=new Blob{cbData=bytes.Length,pbData=Marshal.AllocHGlobal(bytes.Length)};
        Marshal.Copy(bytes,0,input.pbData,bytes.Length);
        try
        {
            Blob output;
            var ok=decrypt
                ? CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,
                    IntPtr.Zero,IntPtr.Zero,1,out output)
                : CryptProtectData(ref input,"DeskSpace Excel mapping",
                    IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new InvalidOperationException(
                "Windows could not "+(decrypt?"decrypt":"encrypt")+
                " Excel mappings (error "+Marshal.GetLastWin32Error()+").");
            try
            {
                var result=new byte[output.cbData];
                Marshal.Copy(output.pbData,result,0,result.Length);
                return result;
            }
            finally {LocalFree(output.pbData);}
        }
        finally {Marshal.FreeHGlobal(input.pbData);}
    }
    public static List<DeskSpaceExcelTemplateV16> Load()
    {
        if(!File.Exists(PathName))return new();
        var decoded=Crypt(File.ReadAllBytes(PathName),true);
        var json=Encoding.UTF8.GetString(decoded);
        Array.Clear(decoded,0,decoded.Length);
        return JsonSerializer.Deserialize<List<DeskSpaceExcelTemplateV16>>(json)
            ??throw new InvalidDataException("Saved Excel mappings could not be read.");
    }
    public static void Save(List<DeskSpaceExcelTemplateV16> entries)
    {
        Directory.CreateDirectory(Folder);
        var data=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries));
        var encrypted=Crypt(data,false);
        Array.Clear(data,0,data.Length);
        var temp=PathName+".tmp";
        File.WriteAllBytes(temp,encrypted);
        if(File.Exists(PathName))File.Copy(PathName,PathName+".previous",true);
        File.Move(temp,PathName,true);
    }
}
