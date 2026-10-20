using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

internal sealed class DeskSpaceExcelChangeV16
{
    public int Row {get;set;}
    public int Column {get;set;}
    public string CellAddress {get;set;}="";
    public string Previous {get;set;}="";
    public string NewValue {get;set;}="";
    public string Decision {get;set;}="";
    public DeskSpaceExcelCellV16 Source {get;set;}=new();
    public bool CanApply=>Decision=="Ready";
    public override string ToString()=>CellAddress+"   "+(CanApply?"→ ":"· ")+
        NewValue+"    ("+Decision+")";
}
internal sealed class DeskSpaceExcelProposalV16
{
    public int StartRow {get;set;}
    public int StartColumn {get;set;}
    public string SheetName {get;set;}="";
    public string Reason {get;set;}="";
    public bool Verified {get;set;}
    public List<DeskSpaceExcelChangeV16> Changes {get;set;}=new();
    public int Writable=>Changes.Count(c=>c.CanApply);
}
internal static class DeskSpaceExcelBridgeV16
{
    [DllImport("ole32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    private static extern void CLSIDFromProgID(string progId,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)]out object application);
    private static dynamic Excel()
    {
        try
        {
            CLSIDFromProgID("Excel.Application",out var classId);
            GetActiveObject(ref classId,IntPtr.Zero,out var application);
            dynamic app=application;
            if(app.ActiveWorkbook==null)
                throw new InvalidOperationException("Open a workbook in Excel first.");
            return app;
        }
        catch(COMException ex)
        {
            throw new InvalidOperationException(
                "No running Microsoft Excel workbook was found. Open Excel first.",ex);
        }
    }
    private static string Text(object? o)=>
        Convert.ToString(o,CultureInfo.InvariantCulture)??"";
    private static string Normalize(string text)=>string.Concat(
        text.Trim().ToUpperInvariant().Where(c=>char.IsLetterOrDigit(c)));
    private static bool Formula(dynamic cell)
    {
        try{return (bool)cell.HasFormula;}
        catch{return false;}
    }
    public static void OpenWorkbook(string path)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Workbook not found",path);
        if(!new[]{".xlsx",".xlsm",".xls",".xlsb"}.Contains(
            Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a genuine Excel workbook.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){
            UseShellExecute=true
        });
    }
    public static DeskSpaceExcelTemplateV16 Capture(string title,string rangeAddress,
        bool includeFormulas)
    {
        if(string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Enter a name for the saved information.");
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        dynamic range=string.IsNullOrWhiteSpace(rangeAddress)
            ? app.Selection : sheet.Range[rangeAddress];
        int row=(int)range.Row,column=(int)range.Column;
        int rows=(int)range.Rows.Count,cols=(int)range.Columns.Count;
        if(rows<=0||cols<=0||rows*(long)cols>1800)
            throw new InvalidOperationException("Select 1–1800 cells, not an entire worksheet.");
        var entry=new DeskSpaceExcelTemplateV16 {
            Name=title.Trim(),
            SourceWorkbook=Path.GetFileName(Text(app.ActiveWorkbook.Name)),
            SourceSheet=Text(sheet.Name),
            SourceAddress=Text(range.Address),
            SourceRow=row,SourceColumn=column,
            RowCount=rows,ColumnCount=cols
        };
        for(int r=1;r<=rows;r++)
            for(int c=1;c<=cols;c++)
            {
                dynamic cell=range.Cells[r,c];
                var raw=(object?)cell.Value2;
                bool hasFormula=Formula(cell);
                if(raw==null&&!hasFormula)continue;
                var record=new DeskSpaceExcelCellV16 {
                    RowOffset=r-1,ColumnOffset=c-1,
                    RawValue=Text(raw),
                    Display=Text(cell.Text),
                    NumberFormat=Text(cell.NumberFormat)
                };
                if(hasFormula&&includeFormulas)
                {
                    record.ValueKind="formula";
                    record.FormulaR1C1=Text(cell.FormulaR1C1);
                }
                else if(raw is bool)record.ValueKind="boolean";
                else if(raw is double || raw is float || raw is decimal || raw is int)
                    record.ValueKind="number";
                else record.ValueKind="text";
                entry.Cells.Add(record);
            }
        // Labels neighboring the selection are structural anchors. They are not
        // part of the data that gets written into the destination workbook.
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int r=Math.Max(1,row-4);r<=row+Math.Min(rows+1,5);r++)
            for(int c=Math.Max(1,column-3);c<=column+Math.Min(cols+2,8);c++)
            {
                if(r>=row&&r<row+rows&&c>=column&&c<column+cols)continue;
                if(entry.Anchors.Count>=16)break;
                try
                {
                    var raw=(object?)sheet.Cells[r,c].Value2;
                    if(raw is not string label)continue;
                    label=label.Trim();
                    if(label.Length<3||label.Length>72)continue;
                    var norm=Normalize(label);
                    if(norm.Length<3||!seen.Add(norm))continue;
                    entry.Anchors.Add(new DeskSpaceExcelAnchorV16 {
                        RowOffset=r-row,ColumnOffset=c-column,Label=label
                    });
                }catch(COMException){}
            }
        if(entry.Cells.Count==0)
            throw new InvalidOperationException("The selected cells contain no values to save.");
        return entry;
    }
    private static IEnumerable<(int Row,int Col,string Label)> SheetLabels(dynamic sheet)
    {
        dynamic used=sheet.UsedRange;
        int startRow=(int)used.Row,startCol=(int)used.Column;
        int rows=(int)used.Rows.Count,cols=(int)used.Columns.Count;
        // Restrict enormous sheets: Excel may report a whole sheet as used
        // because a cell was formatted far away from the data.
        if(rows*(long)cols>70000 || rows>1400 || cols>120)
        {
            rows=Math.Min(rows,400);
            cols=Math.Min(cols,50);
            dynamic bounds=sheet.Range[sheet.Cells[startRow,startCol],
                sheet.Cells[startRow+rows-1,startCol+cols-1]];
            used=bounds;
        }
        object values=used.Value2;
        if(values is Array a&&a.Rank==2)
        {
            var loR=a.GetLowerBound(0);var loC=a.GetLowerBound(1);
            for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
            {
                if(a.GetValue(r+loR,c+loC) is string label)
                    yield return (startRow+r,startCol+c,label);
            }
        }
        else if(values is string str)yield return (startRow,startCol,str);
    }
    private static (int Row,int Col,int Matches,bool Unique) FindAnchorMatch(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry)
    {
        var anchors=entry.Anchors.Where(a=>Normalize(a.Label).Length>2).ToArray();
        if(anchors.Length==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var indexed=anchors.GroupBy(a=>Normalize(a.Label)).ToDictionary(
            x=>x.Key,x=>x.ToList(),StringComparer.OrdinalIgnoreCase);
        var votes=new Dictionary<(int,int),HashSet<string>>();
        foreach(var (row,col,label) in SheetLabels(sheet))
        {
            var key=Normalize(label);
            if(!indexed.TryGetValue(key,out var matches))continue;
            foreach(var a in matches)
            {
                var loc=(row-a.RowOffset,col-a.ColumnOffset);
                if(loc.Item1<1||loc.Item2<1)continue;
                if(!votes.TryGetValue(loc,out var seen))
                    votes[loc]=seen=new HashSet<string>();
                seen.Add(key);
            }
        }
        if(votes.Count==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var ranked=votes.OrderByDescending(v=>v.Value.Count)
            .ThenBy(v=>Math.Abs(v.Key.Item1-entry.SourceRow)+
                Math.Abs(v.Key.Item2-entry.SourceColumn)).ToArray();
        var top=ranked[0];
        var unique=ranked.Length==1||top.Value.Count>ranked[1].Value.Count;
        return (top.Key.Item1,top.Key.Item2,top.Value.Count,unique);
    }
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        int r,c;
        bool verified;
        string reason;
        if(useSelection)
        {
            dynamic selected=app.Selection;
            r=(int)selected.Row;c=(int)selected.Column;
            verified=true;
            reason="Manual target: starting from the selected Excel cell.";
        }
        else
        {
            var match=FindAnchorMatch(sheet,entry);
            r=match.Row;c=match.Col;
            int required=Math.Min(3,entry.Anchors.Count);
            verified=match.Unique&&match.Matches>=Math.Max(2,required) &&
                match.Matches*2>=entry.Anchors.Count;
            reason=verified
                ? "Matched "+match.Matches+" nearby source labels at corresponding positions."
                : "Insufficient unique matching labels. Select the target cell in Excel and preview again.";
        }
        var preview=new DeskSpaceExcelProposalV16 {
            StartRow=r,StartColumn=c,SheetName=Text(sheet.Name),
            Verified=verified,Reason=reason
        };
        foreach(var source in entry.Cells)
        {
            int destRow=r+source.RowOffset,destCol=c+source.ColumnOffset;
            dynamic cell=sheet.Cells[destRow,destCol];
            var old=(object?)cell.Value2;
            var oldText=Text(cell.Text);
            bool destinationFormula=Formula(cell);
            string decision="Ready";
            if(!verified)decision="Unconfirmed target";
            else if(destinationFormula)decision="Existing formula protected";
            else if(old!=null&&!overwrite)decision="Existing value protected";
            else if((bool)sheet.ProtectContents)decision="Sheet protected";
            else if((bool)cell.MergeCells)
            {
                dynamic primary=cell.MergeArea.Cells[1,1];
                if((int)primary.Row!=destRow||(int)primary.Column!=destCol)
                    decision="Merged cell: not top-left";
            }
            var address=Text(cell.Address);
            preview.Changes.Add(new DeskSpaceExcelChangeV16 {
                Row=destRow,Column=destCol,CellAddress=address,
                Previous=oldText,NewValue=source.Display,
                Decision=decision,Source=source
            });
        }
        return preview;
    }
    private static object? TypedValue(DeskSpaceExcelCellV16 cell)
    {
        if(cell.ValueKind=="boolean")
            return string.Equals(cell.RawValue,"True",StringComparison.OrdinalIgnoreCase);
        if(cell.ValueKind=="number")
        {
            if(!double.TryParse(cell.RawValue,NumberStyles.Any,
                CultureInfo.InvariantCulture,out var value))
                throw new InvalidDataException("Invalid saved Excel number.");
            return value;
        }
        return cell.RawValue;
    }
    public static string Apply(DeskSpaceExcelProposalV16 preview,
        DeskSpaceExcelTemplateV16 entry,bool overwrite)
    {
        if(!preview.Verified)throw new InvalidOperationException(
            "Target cells are not verified. Select the first destination cell and preview again.");
        // Re-preview immediately before writing to prevent a stale preview
        // being applied after switching workbooks or sheets.
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        if(!string.Equals(Text(sheet.Name),preview.SheetName,StringComparison.Ordinal))
            throw new InvalidOperationException("The active Excel sheet changed. Preview again.");
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        // Save an independent workbook backup before *any* COM edits.
        var folder=Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelBackups");
        Directory.CreateDirectory(folder);
        var ext=Path.GetExtension(Text(app.ActiveWorkbook.Name));
        if(string.IsNullOrEmpty(ext))ext=".xlsx";
        var backup=Path.Combine(folder,"Excel_before_autofill_"+
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+ext);
        app.ActiveWorkbook.SaveCopyAs(backup);
        if(!File.Exists(backup)||new FileInfo(backup).Length==0)
            throw new IOException("Excel backup was not created. Nothing was changed.");
        var rollback=new List<(dynamic Cell,object? Old,bool HadFormula,string Formula)>();
        try
        {
            foreach(var change in preview.Changes.Where(x=>x.CanApply))
            {
                dynamic cell=sheet.Cells[change.Row,change.Column];
                var previous=(object?)cell.Value2;
                var hadFormula=Formula(cell);
                if(hadFormula)continue;
                if(previous!=null&&!overwrite)continue;
                rollback.Add((cell,previous,hadFormula,hadFormula?Text(cell.FormulaR1C1):""));
                if(change.Source.ValueKind=="formula")
                    cell.FormulaR1C1=change.Source.FormulaR1C1;
                else cell.Value2=TypedValue(change.Source);
            }
        }
        catch
        {
            foreach(var x in rollback.AsEnumerable().Reverse())
                try{
                    if(x.HadFormula)x.Cell.FormulaR1C1=x.Formula;
                    else x.Cell.Value2=x.Old;
                }catch(COMException){}
            throw;
        }
        // Don't auto-save. User may inspect the workbook and save when ready.
        return backup;
    }
}
