using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Saved as metadata in the existing DPAPI-encrypted mapping. This is NOT VBA
// and never executes recorded macros or reads VBProject source code.
internal sealed class DeskSpaceExcelFeatureReportV106
{
    public List<DeskSpaceExcelCellFeatureV106> Cells {get;set;}=new();
    public List<DeskSpaceExcelTableV106> Tables {get;set;}=new();
    public List<string> Limitations {get;set;}=new();
}
internal sealed class DeskSpaceExcelCellFeatureV106
{
    public int Row {get;set;}
    public int Column {get;set;}
    public string InputShape {get;set;}="Unknown";
    public string Formula2 {get;set;}="";
    public bool IsArrayFormula {get;set;}
    public int UnderlineStyle {get;set;}=-4142;
    public bool Superscript {get;set;}
    public bool Subscript {get;set;}
    public string StyleName {get;set;}="";
    public int ValidationType {get;set;}=-1;
    public string ValidationFormula1 {get;set;}="";
    public string ValidationFormula2 {get;set;}="";
    public int ConditionalRuleCount {get;set;}
    public bool Locked {get;set;}
    public bool FormulaHidden {get;set;}
    public string TableName {get;set;}="";
    public string TableRole {get;set;}="";
    public DeskSpaceExcelCellFeatureV106 Shift(int r,int c) =>
        new(){Row=Row+r,Column=Column+c,InputShape=InputShape,
            Formula2=Formula2,IsArrayFormula=IsArrayFormula,
            UnderlineStyle=UnderlineStyle,Superscript=Superscript,
            Subscript=Subscript,StyleName=StyleName,ValidationType=ValidationType,
            ValidationFormula1=ValidationFormula1,
            ValidationFormula2=ValidationFormula2,
            ConditionalRuleCount=ConditionalRuleCount,Locked=Locked,
            FormulaHidden=FormulaHidden,TableName=TableName,TableRole=TableRole};
}
internal sealed class DeskSpaceExcelTableV106
{
    public string Name {get;set;}="";
    public string Address {get;set;}="";
    public string TableStyle {get;set;}="";
    public List<string> ColumnNames {get;set;}=new();
    public bool ShowAutoFilter {get;set;}
    public bool FirstColumnEmphasis {get;set;}
    public bool LastColumnEmphasis {get;set;}
    public int Row {get;set;}
    public int Column {get;set;}
    public int RowCount {get;set;}
    public int ColumnCount {get;set;}
    public bool HasHeaders {get;set;}
    public bool HasTotals {get;set;}
    public bool RowStripes {get;set;}
    public bool ColumnStripes {get;set;}
    public bool FullySelected {get;set;}
    public DeskSpaceExcelTableV106 Shift(int r,int c)=>
        new(){Name=Name,Address=Address,TableStyle=TableStyle,
            ColumnNames=new List<string>(ColumnNames),
            ShowAutoFilter=ShowAutoFilter,
            FirstColumnEmphasis=FirstColumnEmphasis,
            LastColumnEmphasis=LastColumnEmphasis,
            Row=Row+r,Column=Column+c,RowCount=RowCount,
            ColumnCount=ColumnCount,HasHeaders=HasHeaders,
            HasTotals=HasTotals,RowStripes=RowStripes,
            ColumnStripes=ColumnStripes,FullySelected=FullySelected};
}
internal static class DeskSpaceExcelSemanticV106
{
    private static string Safe(Func<object?> read,string fallback="")
    {
        try{return Convert.ToString(read(),CultureInfo.InvariantCulture)??fallback;}
        catch(COMException){return fallback;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return fallback;}
        catch(InvalidCastException){return fallback;}
        catch(ArgumentException){return fallback;}
        catch(FormatException){return fallback;}
    }
    // A ListObject.TableStyle is a Variant. Office may hand COM clients a
    // TableStyle object, whose default ToString() is System._ComObject.
    // Store its exact .Name instead, for later target-workbook preflight.
    private static string TableStyleName(Func<object?> read)
    {
        try
        {
            object? value=read();
            if(value is string name)
                return name.StartsWith("System._ComObject",StringComparison.OrdinalIgnoreCase)
                    ? "" : name;
            if(value is not null && Marshal.IsComObject(value))
            {
                dynamic style=value;
                object? name=style.Name;
                return name is string text &&
                    !text.StartsWith("System._ComObject",StringComparison.OrdinalIgnoreCase)
                    ? text : "";
            }
            return "";
        }
        catch(COMException){return "";}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return "";}
        catch(InvalidCastException){return "";}
        catch(ArgumentException){return "";}
    }
    private static int Int(Func<object?> read,int fallback=0)
    {
        var text=Safe(read);
        return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,
            out int number)?number:fallback;
    }
    private static bool Flag(Func<object?> read)
    {
        var text=Safe(read);
        return bool.TryParse(text,out bool flag)&&flag;
    }
    internal static string InputShape(object? raw,string stored,string expected)
    {
        if(stored=="Formula")return "Formula";
        if(stored=="Blank")return "Blank";
        if(stored=="Error")return "Excel error";
        if(expected is "Date" or "DateTime" or "Time")
            return stored=="Text"?"Date/time text":expected;
        if(stored=="Boolean")return "Boolean";
        if(stored=="Number")return expected=="Percentage"?"Percentage":"Number";
        if(raw is not string text)return stored;
        if(text.Length==0)return "Empty text";
        if(text.All(char.IsDigit))return "Digits stored as text";
        if(text.All(char.IsLetter))return "Alphabet";
        if(text.Any(char.IsWhiteSpace) && text.Any(char.IsLetter))return "Words";
        if(text.Any(char.IsLetter) && text.Any(char.IsDigit))return "Alphanumeric";
        if(text.Any(char.IsDigit))return "Numeric/symbolic text";
        return "Text/symbols";
    }
    internal static DeskSpaceExcelFeatureReportV106 Start(dynamic range,
        int firstRow,int firstCol,int height,int width)
    {
        var report=new DeskSpaceExcelFeatureReportV106();
        report.Limitations.Add(
            "VBA projects, charts/shapes, PivotTable definitions, Power Query, "+
            "events and external connections are NOT stored or replayed.");
        report.Limitations.Add(
            "Conditional rules and mixed-character rich text are not fully "+
            "replayable; computed appearance is separate from rule logic.");
        try
        {
            dynamic tables=range.Worksheet.ListObjects;
            int count=(int)tables.Count;
            for(int i=1;i<=Math.Min(count,1000);i++)
            {
                dynamic table=tables[i];
                dynamic area=table.Range;
                int row=(int)area.Row-firstRow,col=(int)area.Column-firstCol;
                int rows=(int)area.Rows.Count,cols=(int)area.Columns.Count;
                if(row>=height || col>=width || row+rows<=0 || col+cols<=0)
                    continue;
                var snapshot=new DeskSpaceExcelTableV106{
                    Name=Safe(()=>(object?)table.Name),
                    Address=Safe(()=>(object?)area.Address),
                    TableStyle=TableStyleName(()=>(object?)table.TableStyle),
                    ShowAutoFilter=Flag(()=>(object?)table.ShowAutoFilter),
                    FirstColumnEmphasis=Flag(()=>(object?)table.ShowTableStyleFirstColumn),
                    LastColumnEmphasis=Flag(()=>(object?)table.ShowTableStyleLastColumn),
                    Row=row,Column=col,RowCount=rows,ColumnCount=cols,
                    HasHeaders=Flag(()=>(object?)table.ShowHeaders),
                    HasTotals=Flag(()=>(object?)table.ShowTotals),
                    RowStripes=Flag(()=>(object?)table.ShowTableStyleRowStripes),
                    ColumnStripes=Flag(()=>(object?)table.ShowTableStyleColumnStripes),
                    FullySelected=row>=0 && col>=0 &&
                        row+rows<=height && col+cols<=width
                };
                for(int columnIndex=1;columnIndex<=cols;columnIndex++)
                    snapshot.ColumnNames.Add(Safe(()=>(object?)table.ListColumns[columnIndex].Name));
                report.Tables.Add(snapshot);
                if(!snapshot.FullySelected)
                    report.Limitations.Add("Table "+snapshot.Name+
                        " is only partially within the captured selection.");
            }
        }
        catch(COMException){report.Limitations.Add(
            "Excel table object inspection failed; table status unverified.");}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {report.Limitations.Add("Excel ListObjects unavailable from selection.");}
        catch(ArgumentException)
        {report.Limitations.Add("Excel ListObject property returned an invalid range/argument; table metadata unverified.");}
        return report;
    }
    internal static void CaptureCell(DeskSpaceExcelFeatureReportV106 report,
        dynamic cell,int row,int col,object? raw,string stored,string expected,
        bool formula)
    {
        var record=new DeskSpaceExcelCellFeatureV106{
            Row=row,Column=col,InputShape=InputShape(raw,stored,expected),
            UnderlineStyle=Int(()=>(object?)cell.Font.Underline,-4142),
            Superscript=Flag(()=>(object?)cell.Font.Superscript),
            Subscript=Flag(()=>(object?)cell.Font.Subscript),
            StyleName=Safe(()=>(object?)cell.Style),
            Locked=Flag(()=>(object?)cell.Locked),
            FormulaHidden=Flag(()=>(object?)cell.FormulaHidden),
            ConditionalRuleCount=Int(()=>(object?)cell.FormatConditions.Count),
            ValidationType=Int(()=>(object?)cell.Validation.Type,-1)
        };
        if(formula)
        {
            record.Formula2=Safe(()=>(object?)cell.Formula2);
            record.IsArrayFormula=Flag(()=>(object?)cell.HasArray);
        }
        if(record.ValidationType>=0)
        {
            record.ValidationFormula1=Safe(()=>(object?)cell.Validation.Formula1);
            record.ValidationFormula2=Safe(()=>(object?)cell.Validation.Formula2);
        }
        foreach(var table in report.Tables)
        {
            if(row>=table.Row && row<table.Row+table.RowCount &&
               col>=table.Column && col<table.Column+table.ColumnCount)
            {
                record.TableName=table.Name;
                record.TableRole=table.HasHeaders && row==table.Row?"Header":
                    table.HasTotals && row==table.Row+table.RowCount-1?
                        "Totals":"Table data";
                break;
            }
        }
        report.Cells.Add(record);
    }
    internal static DeskSpaceExcelFeatureReportV106 Merge(
        DeskSpaceExcelFeatureReportV106? prior,int pr,int pc,
        DeskSpaceExcelFeatureReportV106? addition,int ar,int ac)
    {
        var merged=new DeskSpaceExcelFeatureReportV106();
        var cells=new Dictionary<(int,int),DeskSpaceExcelCellFeatureV106>();
        foreach(var (source,r,c) in new[]{(prior,pr,pc),(addition,ar,ac)})
        {
            if(source==null)continue;
            foreach(var item in source.Cells)
            {
                var shifted=item.Shift(r,c);
                cells[(shifted.Row,shifted.Column)]=shifted;
            }
            foreach(var table in source.Tables)
            {
                var shifted=table.Shift(r,c);
                merged.Tables.RemoveAll(x=>x.Name==shifted.Name);
                merged.Tables.Add(shifted);
            }
            foreach(var limitation in source.Limitations)
                if(!merged.Limitations.Contains(limitation))
                    merged.Limitations.Add(limitation);
        }
        merged.Cells=cells.Values.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToList();
        return merged;
    }
}
