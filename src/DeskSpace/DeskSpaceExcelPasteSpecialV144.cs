using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Excel-native equivalent of Paste Special -> Formulas for a saved template.
// There is no source Excel Range to Copy(), so xlPasteFormulas (-4123) cannot
// be called safely. Assigning each saved FormulaR1C1 via Excel COM applies
// the SAME native relative/absolute reference translation without touching
// the Windows clipboard, source data, or destination formatting.
internal static class DeskSpaceExcelPasteSpecialV144
{
    [DllImport("ole32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    private static extern void CLSIDFromProgID(string progId,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)]out object application);

    private static int Number(object? value)=>
        Convert.ToInt32(value,CultureInfo.InvariantCulture);
    private static string Text(object? value)=>
        Convert.ToString(value,CultureInfo.InvariantCulture)??"";
    private sealed record Original(int Row,int Column,bool HasFormula,
        string FormulaR1C1,object? Value);
    private sealed record Applied(int Row,int Column,string FormulaR1C1);

    internal static string Paste(DeskSpaceExcelTemplateV16 template)
    {
        DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(template);
        var view=template.SheetVisualV101 ??
            throw new InvalidDataException("Saved mapping has no cell snapshot.");
        if(view.Rows<1 || view.Columns<1 ||
           view.Cells.Count!=view.Rows*view.Columns)
            throw new InvalidDataException(
                "Incomplete saved Excel capture; recapture before formula paste.");
        if(view.FeaturesV106?.Cells.Any(x=>x.IsArrayFormula)==true)
            throw new InvalidDataException(
                "Array/spill formulas need dedicated Excel spill verification. "+
                "This formula-only paste will not write them.");

        CLSIDFromProgID("Excel.Application",out var excelId);
        GetActiveObject(ref excelId,IntPtr.Zero,out object excelObject);
        dynamic excel=excelObject;
        if(excel.ActiveWorkbook==null)
            throw new InvalidOperationException("Open the destination workbook in Excel.");
        dynamic workbook=excel.ActiveWorkbook, sheet=excel.ActiveSheet;
        if((bool)workbook.ReadOnly || (bool)sheet.ProtectContents)
            throw new InvalidOperationException(
                "Destination workbook is read-only or the sheet is protected.");
        dynamic selected=excel.Selection;
        if(Number((object?)selected.Areas.Count)!=1)
            throw new InvalidOperationException(
                "Select one continuous destination area in Excel.");
        int firstRow=Number((object?)selected.Row);
        int firstCol=Number((object?)selected.Column);
        int selectedRows=Number((object?)selected.Rows.Count);
        int selectedCols=Number((object?)selected.Columns.Count);
        if((selectedRows!=1 || selectedCols!=1) &&
           (selectedRows!=view.Rows || selectedCols!=view.Columns))
            throw new InvalidOperationException(
                "Select the destination's first cell or the exact "+
                view.Rows+"×"+view.Columns+" range.");
        if(firstRow+view.Rows-1>1048576 || firstCol+view.Columns-1>16384)
            throw new InvalidOperationException(
                "Selected range extends beyond Excel's worksheet.");
        dynamic target=sheet.Range[
            sheet.Cells[firstRow,firstCol],
            sheet.Cells[firstRow+view.Rows-1,firstCol+view.Columns-1]];
        // Prevent applying a saved mapping over the original captured area.
        static string Addr(string a)=>a.Replace("$","",StringComparison.Ordinal)
            .ToUpperInvariant();
        if(string.Equals(Text((object?)workbook.Name),template.SourceWorkbook,
            StringComparison.OrdinalIgnoreCase) &&
           string.Equals(Text((object?)sheet.Name),template.SourceSheet,
            StringComparison.OrdinalIgnoreCase) &&
           Addr(Text((object?)target.Address))==Addr(template.SourceAddress))
            throw new InvalidOperationException(
                "The original captured Excel range is selected, not a new destination.");

        var source=view.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
        var formulas=source.Where(x=>x.StoredType=="Formula").Select(x=>
            new DeskSpaceExcelFormulaOnlyPolicyV144.Source(
                x.Row,x.Column,x.FormulaA1,x.FormulaR1C1)).ToArray();
        var original=new List<Original>(source.Length);
        var actualFormulaPositions=new List<DeskSpaceExcelFormulaOnlyPolicyV144.Target>();
        foreach(var cell in source)
        {
            dynamic current=target.Cells[cell.Row+1,cell.Column+1];
            bool hasFormula=(bool)current.HasFormula;
            string expression=hasFormula?Text((object?)current.FormulaR1C1):"";
            object? val=(object?)current.Value2;
            original.Add(new Original(cell.Row,cell.Column,
                hasFormula,expression,val));
            if(cell.StoredType=="Formula")
            {
                if((bool)current.MergeCells)
                {
                    dynamic merge=current.MergeArea;
                    if(Number((object?)merge.Row)!=firstRow+cell.Row ||
                       Number((object?)merge.Column)!=firstCol+cell.Column)
                        throw new InvalidOperationException(
                            "Cannot paste formula into non-anchor merged cell.");
                }
                actualFormulaPositions.Add(new(
                    cell.Row,cell.Column,hasFormula,val!=null,expression));
            }
        }
        var planned=DeskSpaceExcelFormulaOnlyPolicyV144.Plan(formulas,
            actualFormulaPositions,view.Rows,view.Columns);
        if(planned.Assignments.Length==0)
            return "All "+formulas.Length+
                " saved formulas are already present and match exactly. "+
                "No workbook contents were changed.";

        // Mandatory safe copy before any Excel mutation.
        var backups=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelBackups");
        Directory.CreateDirectory(backups);
        string extension=Path.GetExtension(Text((object?)workbook.Name));
        if(string.IsNullOrEmpty(extension))extension=".xlsx";
        string copy=Path.Combine(backups,
            "Before_Excel_FormulaPaste_"+DateTime.Now.ToString(
                "yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+extension);
        workbook.SaveCopyAs(copy);
        if(!File.Exists(copy)||new FileInfo(copy).Length==0)
            throw new IOException("Formula-paste safety backup could not be verified.");

        bool eventState=(bool)excel.EnableEvents;
        bool autoFillAvailable=false,oldAutoFill=true;
        var wrote=new List<Applied>();
        try
        {
            excel.EnableEvents=false;
            // Excel ListObjects can auto-generate calculated-column formulas
            // in neighboring rows. Disable that application preference for
            // the duration of this operation, then restore it exactly.
            if(Number((object?)sheet.ListObjects.Count)>0)
            {
                try
                {
                    oldAutoFill=(bool)excel.AutoCorrect.AutoFillFormulasInLists;
                    autoFillAvailable=true;
                    excel.AutoCorrect.AutoFillFormulasInLists=false;
                }
                catch(Exception e)
                {
                    throw new InvalidOperationException(
                        "Excel table auto-fill cannot be safely disabled. "+
                        "No formulas were pasted.",e);
                }
            }

            foreach(var item in planned.Assignments)
            {
                dynamic cell=target.Cells[item.Row+1,item.Column+1];
                // The saved expression uses R1C1, so the destination
                // receives the correct relative and $-absolute references.
                if((bool)cell.HasFormula || (object?)cell.Value2!=null)
                    throw new InvalidDataException(
                        "Destination changed before formula paste; write cancelled.");
                cell.FormulaR1C1=item.R1C1;
                wrote.Add(new Applied(item.Row,item.Column,item.R1C1));
            }
            var inserted=planned.Assignments.ToDictionary(x=>
                (x.Row,x.Column),x=>x.R1C1);
            var observedV145=new List<DeskSpaceExcelZeroIntegrityV145.CellState>();
            foreach(var was in original)
            {
                dynamic cell=target.Cells[was.Row+1,was.Column+1];
                bool nowFormula=(bool)cell.HasFormula;
                var nowExpression=nowFormula?Text((object?)cell.FormulaR1C1):"";
                var nowValue=(object?)cell.Value2;
                observedV145.Add(new(was.HasFormula,was.Value,nowFormula,
                    nowValue,was.FormulaR1C1,nowExpression));
                if(inserted.TryGetValue((was.Row,was.Column),out var wanted))
                {
                    if(!nowFormula ||
                       !string.Equals(nowExpression,wanted,StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "Excel formula write/readback mismatch at "+
                            Text((object?)cell.Address));
                }
                else if(was.HasFormula!=nowFormula ||
                        (was.HasFormula &&
                         !string.Equals(was.FormulaR1C1,nowExpression,
                            StringComparison.Ordinal)) ||
                        DeskSpaceExcelZeroIntegrityV145.HasUnexpectedConstant(
                            observedV145[observedV145.Count-1]))
                    throw new InvalidDataException(
                        "Excel changed an unrelated value or formula at "+
                        Text((object?)cell.Address));
            }
            int zeroResultsV145=DeskSpaceExcelZeroIntegrityV145.
                FormulaZeroCount(observedV145);
            return "Paste Special · Formulas verified: "+
                planned.Assignments.Length+" full formulas inserted; "+
                planned.AlreadyPresent+" already matching. "+
                "0 constant input values copied. All other cells preserved. "+
                DeskSpaceExcelZeroIntegrityV145.ZeroExplanation(zeroResultsV145)+
                " Safety backup: "+copy;
        }
        catch(Exception problem)
        {
            string rollback="";bool restored=true;
            try
            {
                // Clear only cells proven blank BEFORE this operation and
                // written during it. Never erase pre-existing user contents.
                var byPosition=original.ToDictionary(x=>(x.Row,x.Column));
                // Revert every originally empty physical cell that Excel
                // changed (including automatic calculated-column fill).
                // Existing values and formulas are never cleared.
                foreach(var prior in original)
                {
                    if(prior.HasFormula || prior.Value!=null)continue;
                    dynamic cell=target.Cells[prior.Row+1,prior.Column+1];
                    if((bool)cell.HasFormula || (object?)cell.Value2!=null)
                    {
                        if((bool)cell.MergeCells)
                        {
                            dynamic merged=cell.MergeArea;
                            if(Number((object?)merged.Row)!=firstRow+prior.Row ||
                               Number((object?)merged.Column)!=firstCol+prior.Column)
                                continue;
                        }
                        cell.ClearContents();
                    }
                }
            }
            catch(Exception recoveryError)
            {restored=false;rollback=recoveryError.Message;}
            throw new InvalidOperationException(
                "Formula paste did not verify. "+
                (restored?"Inserted formulas were cleared. ":
                    "Automatic cell rollback was incomplete ("+rollback+"). ")+
                "Unmodified workbook backup: "+copy+". "+
                problem.Message,problem);
        }
        finally
        {
            if(autoFillAvailable)
            {
                try{excel.AutoCorrect.AutoFillFormulasInLists=oldAutoFill;}
                catch(COMException){}
            }
            excel.EnableEvents=eventState;
        }
    }
}
