using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Normalize NEW values in an existing selected range against the captured
// format/type structure. Never replay old captured data. No changes occur
// on Preview; Apply requires explicit confirmation from the Excel editor.
internal sealed class DeskSpaceExcelNormalizePlanV102
{
    internal sealed class Fix
    {
        public int Row,Column;
        public string Address="",Expected="",OldFormat="",Format="";
        public string OriginalText="";
        public object? TypedValue;
        public bool ChangeValue,ChangeFormat;
    }
    public Guid MappingId {get;set;}
    public string Workbook="",Sheet="",Range="";
    public int Rows,Columns;
    public List<Fix> Changes {get;set;}=new();
    public List<string> Warnings {get;set;}=new();
    public int Values=>Changes.Count(x=>x.ChangeValue);
    public int Formats=>Changes.Count(x=>x.ChangeFormat);
}

internal static class DeskSpaceExcelNormalizeV102
{
    private static string Text(object? o)=>
        Convert.ToString(o,CultureInfo.InvariantCulture)??"";
    private static bool Formula(dynamic cell)
    {
        try{return Convert.ToBoolean((object?)cell.HasFormula);}
        catch(COMException){return false;}
    }
    private static bool IsEditableExpected(string kind)=>
        kind is "Number" or "Percentage" or "Date" or "DateTime" or "Time";
    private static dynamic Excel()=>DeskSpaceExcelBridgeV16.ExcelForNormalizationV102();

    internal static DeskSpaceExcelNormalizePlanV102 Preview(
        DeskSpaceExcelTemplateV16 model,string dateOrder)
    {
        var structure=model.SheetVisualV101??throw new InvalidOperationException(
            "This mapping has no captured sheet structure. Open the original "+
            "workbook, select its cells, and capture formatting first.");
        dynamic app=Excel();
        dynamic selection=app.ActiveWindow.RangeSelection;
        dynamic sheet=selection.Worksheet;
        if((int)selection.Areas.Count!=1 ||
           (int)selection.Rows.Count!=structure.Rows ||
           (int)selection.Columns.Count!=structure.Columns)
            throw new InvalidOperationException(
                "Select a single contiguous destination range of "+
                structure.Rows+" rows and "+structure.Columns+" columns.");
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException(
                "The selected worksheet is protected; no changes were made.");
        var plan=new DeskSpaceExcelNormalizePlanV102{
            MappingId=model.Id,
            Workbook=Text(app.ActiveWorkbook.FullName),
            Sheet=Text(sheet.Name),
            Range=Text(selection.Address),
            Rows=structure.Rows,Columns=structure.Columns
        };
        foreach(var expectedCell in structure.Cells)
        {
            if(expectedCell.Row<0||expectedCell.Column<0 ||
               expectedCell.Row>=structure.Rows||
               expectedCell.Column>=structure.Columns)continue;
            dynamic cell=selection.Cells[expectedCell.Row+1,expectedCell.Column+1];
            string address=Text(cell.Address);
            if(Formula(cell))
            {
                // Input normalization must NEVER replace a live formula.
                continue;
            }
            if(expectedCell.ExpectedType=="Formula")
            {
                // Formula restoration belongs to its own explicit, reviewed
                // structural operation; not an input type repair.
                plan.Warnings.Add(address+" · expected formula missing; not overwritten");
                continue;
            }
            string type=expectedCell.ExpectedType;
            if(!IsEditableExpected(type))continue;
            var actual=(object?)cell.Value2;
            if(actual==null)continue;
            string beforeFormat=Text(cell.NumberFormat);
            var decision=DeskSpaceExcelInputTypeV101.Normalize(
                actual,type,expectedCell.NumberFormat,dateOrder);
            if(decision.Status=="NeedsReview")
            {
                plan.Warnings.Add(address+" · "+decision.Explanation);
                continue;
            }
            bool convert=decision.Status=="Converted";
            string targetFormat=expectedCell.NumberFormat;
            // Date/number display depends on the captured Excel format;
            // do NOT convert a plain-looking identifier or paint data.
            bool style=targetFormat.Length>0 &&
                !string.Equals(targetFormat,beforeFormat,StringComparison.Ordinal);
            if(!convert&&!style)continue;
            plan.Changes.Add(new DeskSpaceExcelNormalizePlanV102.Fix{
                Row=expectedCell.Row,Column=expectedCell.Column,Address=address,
                Expected=type,OldFormat=beforeFormat,Format=targetFormat,
                OriginalText=Text(actual),
                TypedValue=decision.TypedValue??actual,
                ChangeValue=convert,ChangeFormat=style
            });
        }
        return plan;
    }

    internal static string Apply(DeskSpaceExcelNormalizePlanV102 plan,
        Guid confirmedMappingId)
    {
        if(plan.MappingId!=confirmedMappingId)
            throw new InvalidOperationException(
                "Saved mapping changed since the type preview. Preview again.");
        if(plan.Changes.Count==0)
            throw new InvalidOperationException("There are no safe corrections.");
        dynamic app=Excel();
        dynamic workbook=app.ActiveWorkbook;
        dynamic selection=app.ActiveWindow.RangeSelection;
        dynamic sheet=selection.Worksheet;
        if(!string.Equals(Text(workbook.FullName),plan.Workbook,
                StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(Text(sheet.Name),plan.Sheet,StringComparison.Ordinal) ||
           !string.Equals(Text(selection.Address),plan.Range,StringComparison.Ordinal) ||
           (int)selection.Rows.Count!=plan.Rows ||
           (int)selection.Columns.Count!=plan.Columns ||
           (int)selection.Areas.Count!=1 ||
           (bool)sheet.ProtectContents)
            throw new InvalidOperationException(
                "Workbook, worksheet or selected cells changed. Preview again.");

        // Recheck every destination BEFORE writing a single cell.
        foreach(var change in plan.Changes)
        {
            dynamic cell=selection.Cells[change.Row+1,change.Column+1];
            if(Formula(cell) ||
               !string.Equals(Text(cell.Value2),change.OriginalText,
                   StringComparison.Ordinal) ||
               !string.Equals(Text(cell.NumberFormat),change.OldFormat,
                   StringComparison.Ordinal))
                throw new InvalidOperationException(
                    change.Address+" changed after preview; no edits applied.");
        }
        var folder=Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelBackups");
        Directory.CreateDirectory(folder);
        string ext=Path.GetExtension(Text(workbook.Name));
        if(ext.Length==0)ext=".xlsx";
        string backup=Path.Combine(folder,
            "Excel_before_type_repair_"+DateTime.Now.ToString(
                "yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+ext);
        workbook.SaveCopyAs(backup);
        if(!File.Exists(backup)||new FileInfo(backup).Length==0)
            throw new IOException("A verified workbook backup could not be created.");
        var rollback=new List<(dynamic Cell,object? OldValue,string OldFormat)>();
        try
        {
            foreach(var change in plan.Changes)
            {
                dynamic cell=selection.Cells[change.Row+1,change.Column+1];
                var original=(object?)cell.Value2;
                rollback.Add((cell,original,Text(cell.NumberFormat)));
                if(change.ChangeValue)
                {
                    if(change.TypedValue is DateTime date)
                        cell.Value=date; // COM DATE respects workbook date system.
                    else cell.Value2=change.TypedValue;
                }
                if(change.ChangeFormat)
                    cell.NumberFormat=change.Format; // Long date remains long.
            }
        }
        catch
        {
            foreach(var item in rollback.AsEnumerable().Reverse())
            {
                try
                {
                    item.Cell.Value2=item.OldValue;
                    item.Cell.NumberFormat=item.OldFormat;
                }
                catch(COMException){}
            }
            throw;
        }
        // User reviews Excel itself and decides when to save.
        return backup;
    }
}
