using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Read-only preflight of the user's LIVE Excel selection. This never writes
// source or destination workbook content. It is deliberately fail-closed.
internal static class DeskSpaceExcelDestinationV108
{
    [DllImport("ole32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    private static extern void CLSIDFromProgID(string program,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)]out object application);

    internal sealed record Result(
        DeskSpaceExcelFullRangeGateV107.Decision Gate,
        string Workbook,string Worksheet,string TargetAddress,
        IReadOnlyList<string> Blockers)
    {
        // Gate success does not equal formatting-engine readiness.
        public bool SafeToApply => Gate.MayApply && Blockers.Count==0;
    }

    private static dynamic ActiveExcel()
    {
        CLSIDFromProgID("Excel.Application",out var id);
        GetActiveObject(ref id,IntPtr.Zero,out object app);
        dynamic excel=app;
        if(excel.ActiveWorkbook==null)throw new InvalidOperationException(
            "Open the destination workbook in Excel first.");
        return excel;
    }
    private static bool HasFormula(dynamic cell)
    {
        try{return (bool)cell.HasFormula;}
        catch(COMException){throw;}
        catch(InvalidCastException){return false;}
    }
    private static object? Raw(dynamic cell)=>(object?)cell.Value2;
    private static string Text(object? input)=>
        Convert.ToString(input,CultureInfo.InvariantCulture)??"";

    internal static Result Check(DeskSpaceExcelTemplateV16 entry)
    {
        if(entry.SheetVisualV101==null)
            throw new InvalidOperationException(
                "This saved mapping predates per-cell format capture. "+
                "Recapture the original Excel selection.");
        var visual=entry.SheetVisualV101;
        int height=visual.Rows,width=visual.Columns;
        if(height<1 || width<1 || height*(long)width>1800)
            throw new InvalidOperationException("Invalid saved Excel selection size.");
        var index=visual.Cells.Where(c=>c.Row>=0&&c.Row<height &&
            c.Column>=0&&c.Column<width).
            GroupBy(c=>(c.Row,c.Column)).
            ToDictionary(g=>g.Key,g=>g.First());
        if(index.Count!=height*width || visual.Cells.Count!=height*width)
            throw new InvalidDataException(
                "Saved per-cell formatting is incomplete or contains duplicates.");

        dynamic excel=ActiveExcel();
        dynamic sheet=excel.ActiveSheet,selected=excel.Selection;
        if((int)selected.Areas.Count!=1)
            throw new InvalidOperationException("Select one contiguous Excel range.");
        int r=(int)selected.Row,c=(int)selected.Column;
        int sh=(int)selected.Rows.Count,sw=(int)selected.Columns.Count;
        if((sh!=1 || sw!=1) && (sh!=height || sw!=width))
            throw new InvalidOperationException(
                "Select either the first destination cell or exactly "+
                height+" rows by "+width+" columns.");
        if(r+height-1>1048576 || c+width-1>16384)
            throw new InvalidOperationException(
                "The saved formatting would extend beyond Excel's sheet bounds.");
        dynamic target=sheet.Range[
            sheet.Cells[r,c],sheet.Cells[r+height-1,c+width-1]];
        var from=new List<DeskSpaceExcelFullRangeGateV107.Cell>(height*width);
        var to=new List<DeskSpaceExcelFullRangeGateV107.Cell>(height*width);
        for(int row=0;row<height;row++)
        for(int col=0;col<width;col++)
        {
            var source=index[(row,col)];
            from.Add(new(source.StoredType!="Blank",source.StoredType));
            dynamic dest=target.Cells[row+1,col+1];
            bool formula=HasFormula(dest);
            var val=Raw(dest);
            var kind=DeskSpaceExcelInputTypeV101.Stored(val,formula);
            to.Add(new(formula || val!=null,kind));
        }
        var gate=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        var blockers=new List<string>();
        if((bool)sheet.ProtectContents)
            blockers.Add("Destination sheet is protected.");
        if((bool)excel.ActiveWorkbook.ReadOnly)
            blockers.Add("Destination workbook is read-only.");

        // The validator must never promise an apply engine that is not yet
        // implemented. ListObjects / merges / advanced formatting need their
        // own write+readback transaction before release.
        if(visual.FeaturesV106 is not { } features)
            blockers.Add("Legacy source does not include Excel table/semantic metadata.");
        else
        {
            if(features.Tables.Count>0)
                blockers.Add("One or more real Excel Tables require verified "+
                    "ListObject create/update and structural readback.");
            if(features.Cells.Any(x=>x.ValidationType>=0 ||
                x.ConditionalRuleCount>0))
                blockers.Add("Some cells require data-validation or conditional "+
                    "rule application, not just colour copying.");
        }
        if(visual.Cells.Any(x=>x.MergeRows>1 || x.MergeColumns>1))
            blockers.Add("Merged cells require a verified non-destructive merge plan.");
        if(visual.Cells.Any(x=>x.Underline))
            blockers.Add("Exact underline styles require write/readback verification.");
        if(!gate.MayApply)
            blockers.Add("Whole-selection gate rejected destination: "+gate.Explanation);
        return new(gate,Text(excel.ActiveWorkbook.FullName),
            Text(sheet.Name),Text(target.Address),blockers);
    }
}
