using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

// Exact cells are captured from real Excel workbooks via native Excel COM.
// A screenshot by itself is never used as a source for coordinates or values.
internal sealed class DeskSpaceExcelCellV16
{
    public bool Include {get;set;}=true;
    public int RowOffset {get;set;}
    public int ColumnOffset {get;set;}
    public string Address {get;set;}="";
    public string Type {get;set;}="";
    public string Raw {get;set;}="";
    public string Display {get;set;}="";
    public bool IsFormula {get;set;}
    public string Formula {get;set;}="";
    public string Caption => IsFormula?"Formula (not imported)":Display;
}
internal sealed class DeskSpaceExcelAnchorV16
{
    public int RowOffset {get;set;}
    public int ColumnOffset {get;set;}
    public string Label {get;set;}="";
}
internal sealed class DeskSpaceExcelProfileV16
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public string SourceWorkbook {get;set;}="";
    public string Sheet {get;set;}="";
    public string SourceRange {get;set;}="";
    public int FirstRow {get;set;}
    public int FirstColumn {get;set;}
    public List<DeskSpaceExcelAnchorV16> Anchors {get;set;}=new();
    public List<DeskSpaceExcelCellV16> Cells {get;set;}=new();
    public override string ToString()=>Name+" · "+Sheet+"!"+SourceRange;
}
internal sealed class DeskSpaceExcelPreviewV16
{
    public string Cell {get;set;}="";
    public string SavedValue {get;set;}="";
    public string ExistingValue {get;set;}="";
    public string Status {get;set;}="";
    public int Row {get;set;}
    public int Column {get;set;}
    public DeskSpaceExcelCellV16? Source {get;set;}
}
internal sealed class DeskSpaceExcelPlanV16
{
    public List<DeskSpaceExcelPreviewV16> Entries {get;set;}=new();
    public List<string> Issues {get;set;}=new();
    public bool CanApply => Issues.Count==0 &&
        Entries.All(x=>x.Status is "Fill" or "Replace" or "Unchanged");
}
internal static class DeskSpaceExcelEngineV16
{
    private const int MaxCells=1800;
    public static string Address(int row,int column)
    {
        if(row<1||column<1||column>16384)throw new ArgumentOutOfRangeException();
        var col="";
        for(int x=column;x>0;x=(x-1)/26)col=(char)('A'+(x-1)%26)+col;
        return col+row;
    }
    private static string Normalize(string? input)=>
        string.Join(" ",(input??"").Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).Trim().ToUpperInvariant();
    private static string Stringify(object? value)
    {
        if(value==null)return "";
        return Convert.ToString(value,CultureInfo.InvariantCulture)??"";
    }
    private static (string Type,string Raw) Encode(object? value)
    {
        if(value==null)return ("blank","");
        if(value is bool b)return ("boolean",b?"true":"false");
        if(value is double or float or decimal or int or long)
            return ("number",Convert.ToString(value,CultureInfo.InvariantCulture)??"0");
        return ("text",Stringify(value));
    }
    private static object? Decode(DeskSpaceExcelCellV16 cell)
    {
        return cell.Type switch
        {
            "boolean" => cell.Raw=="true",
            "number" => double.Parse(cell.Raw,CultureInfo.InvariantCulture),
            "text" => cell.Raw,
            _ => null
        };
    }
    internal static bool SameValue(object? a,DeskSpaceExcelCellV16 stored)
    {
        if(stored.Type=="blank")return a==null||Stringify(a).Length==0;
        var (type,raw)=Encode(a);
        if(type=="number" && stored.Type=="number" &&
           double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var x) &&
           double.TryParse(stored.Raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var y))
            return x.Equals(y);
        return type==stored.Type&&raw==stored.Raw;
    }
    public static (string Path,string Sheet,string Range) ActiveSelection()
    {
        using var app=ExcelBridge.Attach();
        dynamic selection=app.Excel.Selection;
        dynamic worksheet=selection.Worksheet;
        dynamic book=worksheet.Parent;
        string path=Stringify(book.FullName);
        if(!File.Exists(path))throw new InvalidOperationException(
            "Save your currently open Excel workbook first.");
        return (path,Stringify(worksheet.Name),
            Stringify(selection.Address).Replace("$",""));
    }
    public static DeskSpaceExcelProfileV16 Capture(string path,string sheet,string range,string name,string purpose)
    {
        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Enter a name for the saved data.");
        using var x=ExcelBridge.Open(path,true);
        dynamic worksheet=x.GetSheet(sheet);
        dynamic area=worksheet.Range[range];
        int rows=Convert.ToInt32(area.Rows.Count),cols=Convert.ToInt32(area.Columns.Count);
        if(rows<=0||cols<=0||((long)rows*cols)>MaxCells)
            throw new InvalidOperationException("Select at most "+MaxCells+" cells per capture.");
        int firstRow=Convert.ToInt32(area.Row),firstCol=Convert.ToInt32(area.Column);
        var profile=new DeskSpaceExcelProfileV16{
            Name=name.Trim(),Description=purpose.Trim(),SourceWorkbook=Path.GetFileName(path),
            Sheet=Stringify(worksheet.Name),SourceRange=Stringify(area.Address).Replace("$",""),
            FirstRow=firstRow,FirstColumn=firstCol
        };
        for(int c=0;c<cols;c++)
        {
            for(int up=1;up<=5&&firstRow-up>=1;up++)
            {
                dynamic label=worksheet.Cells[firstRow-up,firstCol+c];
                var visible=Stringify(label.Value2);
                if(string.IsNullOrWhiteSpace(visible))continue;
                profile.Anchors.Add(new DeskSpaceExcelAnchorV16{
                    ColumnOffset=c,RowOffset=-up,Label=visible
                });
                break;
            }
        }
        for(int r=0;r<rows;r++)
            for(int c=0;c<cols;c++)
            {
                dynamic cell=worksheet.Cells[firstRow+r,firstCol+c];
                object? value=cell.Value2;
                var (type,raw)=Encode(value);
                if(type=="blank")continue;
                string formula=Stringify(cell.Formula);
                bool isFormula=formula.StartsWith("=",StringComparison.Ordinal);
                profile.Cells.Add(new DeskSpaceExcelCellV16{
                    Address=Address(firstRow+r,firstCol+c),RowOffset=r,ColumnOffset=c,
                    Type=type,Raw=raw,Display=Stringify(cell.Text),
                    IsFormula=isFormula,Formula=isFormula?formula:"",
                    Include=!isFormula
                });
            }
        if(profile.Cells.Count==0)
            throw new InvalidOperationException("The selected Excel range has no nonempty cells.");
        return profile;
    }
    private static (int Row,int Col) Origin(DeskSpaceExcelProfileV16 profile,string? start)
    {
        if(string.IsNullOrWhiteSpace(start))return (profile.FirstRow,profile.FirstColumn);
        var code=start.Trim().Replace("$","").ToUpperInvariant();
        if(code.Contains(':'))code=code.Split(':')[0];
        var letters=new string(code.TakeWhile(char.IsLetter).ToArray());
        var numbers=code.Substring(letters.Length);
        if(letters.Length==0||letters.Length>3||!int.TryParse(numbers,out int row)||
           row<1||row>1048576)throw new ArgumentException("Enter a top-left Excel cell such as B21.");
        var col=0;
        foreach(char c in letters)
        {
            if(c<'A'||c>'Z')throw new ArgumentException("Invalid Excel column.");
            col=col*26+(c-'A'+1);
        }
        if(col>16384)throw new ArgumentException("Column exceeds Excel limits.");
        return (row,col);
    }
    private static DeskSpaceExcelPlanV16 PlanOnSheet(dynamic worksheet,
        DeskSpaceExcelProfileV16 profile,string? targetStart,bool overwrite)
    {
        var plan=new DeskSpaceExcelPlanV16();
        var (firstRow,firstCol)=Origin(profile,targetStart);
        foreach(var anchor in profile.Anchors)
        {
            if(anchor.RowOffset+firstRow<1)
            {
                plan.Issues.Add("A reference heading is outside the worksheet.");
                continue;
            }
            dynamic cell=worksheet.Cells[firstRow+anchor.RowOffset,firstCol+anchor.ColumnOffset];
            if(Normalize(Stringify(cell.Value2))!=Normalize(anchor.Label))
                plan.Issues.Add("Header mismatch at "+
                    Address(firstRow+anchor.RowOffset,firstCol+anchor.ColumnOffset)+
                    ": expected \""+anchor.Label+"\".");
        }
        if(profile.Anchors.Count==0)
            plan.Issues.Add("No matching template headers were captured. Select a range with headings above it.");
        foreach(var stored in profile.Cells.Where(x=>x.Include))
        {
            if(stored.IsFormula)
            {
                plan.Issues.Add("Saved formula "+stored.Address+" is not eligible for value-only autofill.");
                continue;
            }
            var row=firstRow+stored.RowOffset;
            var col=firstCol+stored.ColumnOffset;
            if(row<1||row>1048576||col<1||col>16384)
            {
                plan.Issues.Add("Mapped cell is outside Excel worksheet limits.");
                continue;
            }
            dynamic cell=worksheet.Cells[row,col];
            string originalFormula=Stringify(cell.Formula);
            bool hasFormula=originalFormula.StartsWith("=",StringComparison.Ordinal);
            object? current=cell.Value2;
            bool empty=current==null || Stringify(current)=="";
            var status=hasFormula?"Formula: blocked":
                SameValue(current,stored)?"Unchanged":
                empty?"Fill":overwrite?"Replace":"Occupied: blocked";
            var address=Address(row,col);
            plan.Entries.Add(new DeskSpaceExcelPreviewV16{
                Cell=address,ExistingValue=Stringify(current),
                SavedValue=stored.Display,Status=status,Row=row,Column=col,Source=stored
            });
            if(status.Contains("blocked",StringComparison.Ordinal))
                plan.Issues.Add(address+" contains an existing value or formula.");
        }
        if(plan.Entries.Count==0)plan.Issues.Add("No source values are selected.");
        return plan;
    }
    public static DeskSpaceExcelPlanV16 Preview(
        string target,DeskSpaceExcelProfileV16 profile,string? targetSheet,string? topLeft,bool overwrite)
    {
        using var x=ExcelBridge.Open(target,true);
        var sheet=string.IsNullOrWhiteSpace(targetSheet)?profile.Sheet:targetSheet.Trim();
        return PlanOnSheet(x.GetSheet(sheet),profile,topLeft,overwrite);
    }
    public static string ApplyCopy(string target,DeskSpaceExcelProfileV16 profile,
        string? targetSheet,string? topLeft,bool overwrite)
    {
        if(!File.Exists(target))throw new FileNotFoundException("Target Excel workbook is missing.",target);
        var test=Preview(target,profile,targetSheet,topLeft,overwrite);
        if(!test.CanApply)throw new InvalidOperationException(
            "The target is not safe to autofill: "+string.Join("; ",test.Issues.Take(5)));
        var downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        Directory.CreateDirectory(downloads);
        var name=Path.GetFileNameWithoutExtension(target);
        var output=Path.Combine(downloads,name+"_DeskSpaceFilled_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+
            Path.GetExtension(target));
        File.Copy(target,output,false);
        try
        {
            using var book=ExcelBridge.Open(output,false);
            var sheet=string.IsNullOrWhiteSpace(targetSheet)?profile.Sheet:targetSheet.Trim();
            dynamic ws=book.GetSheet(sheet);
            var recheck=PlanOnSheet(ws,profile,topLeft,overwrite);
            if(!recheck.CanApply)throw new InvalidOperationException(
                "The copied workbook changed during preview; no changes were saved.");
            foreach(var line in recheck.Entries.Where(e=>e.Status is "Fill" or "Replace"))
            {
                dynamic cell=ws.Cells[line.Row,line.Column];
                cell.Value2=Decode(line.Source!);
            }
            book.Save();
            return output;
        }
        catch
        {
            try{if(File.Exists(output))File.Delete(output);}catch{}
            throw;
        }
    }
}
// Excel needs to be installed locally. Nothing runs macros during read/write.
internal sealed class ExcelBridge : IDisposable
{
    public dynamic Excel {get;private set;}
    private dynamic? workbook;
    private readonly bool ownsApp;
    private ExcelBridge(dynamic app,bool owns){Excel=app;ownsApp=owns;}
    [DllImport("ole32.dll",CharSet=CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object app);
    public static ExcelBridge Attach()
    {
        if(CLSIDFromProgID("Excel.Application",out var id)!=0)
            throw new InvalidOperationException("Microsoft Excel is not installed.");
        GetActiveObject(ref id,IntPtr.Zero,out var app);
        return new ExcelBridge(app,false);
    }
    public static ExcelBridge Open(string path,bool readOnly)
    {
        path=Path.GetFullPath(path);
        if(!File.Exists(path))throw new FileNotFoundException("Excel workbook not found.",path);
        if(!new[]{".xlsx",".xlsm",".xls",".xlsb"}.Contains(
            Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Select an Excel workbook (.xlsx, .xlsm, .xls or .xlsb).");
        var type=Type.GetTypeFromProgID("Excel.Application")??
            throw new InvalidOperationException("Desktop Microsoft Excel is required.");
        dynamic app=Activator.CreateInstance(type) ??
            throw new InvalidOperationException("Excel could not start.");
        var owner=new ExcelBridge(app,true);
        try{
            app.Visible=false;
            app.DisplayAlerts=false;
            app.AskToUpdateLinks=false;
            try{app.AutomationSecurity=3;}catch{}
            owner.workbook=app.Workbooks.Open(path,0,readOnly);
            return owner;
        }catch{owner.Dispose();throw;}
    }
    public dynamic GetSheet(string name)
    {
        if(workbook==null)throw new InvalidOperationException("No workbook.");
        try{return workbook.Worksheets[name];}
        catch(Exception){throw new InvalidOperationException(
            "Worksheet \""+name+"\" was not found in the workbook.");}
    }
    public void Save(){if(workbook==null)throw new InvalidOperationException();workbook.Save();}
    public void Dispose()
    {
        try{if(workbook!=null)workbook.Close(false);}catch{}
        workbook=null;
        if(ownsApp)try{Excel.Quit();}catch{}
    }
}
internal static class DeskSpaceExcelStorageV16
{
    private static readonly string Folder=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","ExcelTemplates");
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob{public int cbData;public IntPtr pbData;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob data,string? name,IntPtr entropy,
        IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob data,IntPtr name,
        IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Transform(byte[] bytes,bool encrypt)
    {
        var input=new Blob{cbData=bytes.Length,pbData=Marshal.AllocHGlobal(bytes.Length)};
        try{
            Marshal.Copy(bytes,0,input.pbData,bytes.Length);
            Blob output;
            bool okay=encrypt
                ? CryptProtectData(ref input,"DeskSpace Excel template",IntPtr.Zero,
                    IntPtr.Zero,IntPtr.Zero,1,out output)
                : CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,
                    IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!okay)throw new InvalidOperationException(
                "Windows could not securely "+(encrypt?"protect":"open")+" the saved Excel template.");
            try{
                var result=new byte[output.cbData];
                Marshal.Copy(output.pbData,result,0,result.Length);
                return result;
            }finally{LocalFree(output.pbData);}
        }
        finally{Marshal.FreeHGlobal(input.pbData);}
    }
    public static void Save(DeskSpaceExcelProfileV16 profile)
    {
        Directory.CreateDirectory(Folder);
        var bytes=Transform(JsonSerializer.SerializeToUtf8Bytes(profile),true);
        var path=Path.Combine(Folder,profile.Id.ToString("N")+".dsexcel");
        var temp=path+".pending";
        File.WriteAllBytes(temp,bytes);
        File.Move(temp,path,true);
    }
    public static List<DeskSpaceExcelProfileV16> Load()
    {
        if(!Directory.Exists(Folder))return new();
        var profiles=new List<DeskSpaceExcelProfileV16>();
        foreach(var path in Directory.EnumerateFiles(Folder,"*.dsexcel"))
        {
            try{
                var raw=Transform(File.ReadAllBytes(path),false);
                var item=JsonSerializer.Deserialize<DeskSpaceExcelProfileV16>(raw);
                if(item!=null)profiles.Add(item);
            }catch(Exception){/* corrupt/foreign-user data is not modified */}
        }
        return profiles.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
    }
}
