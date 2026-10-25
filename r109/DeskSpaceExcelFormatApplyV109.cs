using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Direct Excel COM formatting, NOT a recorded macro or keystroke sequence.
// Only runs after verifying the entire destination as Empty or Fully Matching.
// Unsupported workbook structures block before the first change.
internal static class DeskSpaceExcelFormatApplyV109
{
    [DllImport("ole32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    private static extern void CLSIDFromProgID(string progId,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)]out object application);

    internal sealed record Review(string Workbook,string Sheet,string Destination,
        DeskSpaceExcelFullRangeGateV107.Decision Gate,IReadOnlyList<string> Blockers)
    {
        internal bool Eligible=>Gate.MayApply && Blockers.Count==0;
        public override string ToString()=>Gate.Explanation+
            (Blockers.Count>0?Environment.NewLine+string.Join(Environment.NewLine,Blockers):"");
    }
    private static dynamic Excel()
    {
        CLSIDFromProgID("Excel.Application",out var id);
        GetActiveObject(ref id,IntPtr.Zero,out object app);
        dynamic excel=app;
        if(excel.ActiveWorkbook==null)
            throw new InvalidOperationException("Open a workbook in Microsoft Excel.");
        return excel;
    }
    private static string Text(object? obj)=>
        Convert.ToString(obj,CultureInfo.InvariantCulture)??"";
    private static bool IsFormula(dynamic cell)=>Convert.ToBoolean((object?)cell.HasFormula);
    private static string Norm(string text)=>
        text.Replace("$","",StringComparison.Ordinal).ToUpperInvariant();
    private static int ToInt(object? value)=>
        Convert.ToInt32(value,CultureInfo.InvariantCulture);

    private sealed class Checked
    {
        internal required dynamic Excel;
        internal required dynamic Sheet;
        internal required dynamic Range;
        internal required DeskSpaceExcelSheetVisualV101 Visual;
        internal required Review Review;
        internal required int FirstRow;
        internal required int FirstColumn;
    }
    private static Checked Inspect(DeskSpaceExcelTemplateV16 template)
    {
        var visual=template.SheetVisualV101 ??
            throw new InvalidOperationException("Recapture source Excel format; old snapshot is incomplete.");
        int rows=visual.Rows,cols=visual.Columns;
        if(rows<1||cols<1||rows*(long)cols>1800 ||
           visual.Cells.Count!=rows*cols ||
           visual.Cells.Select(c=>(c.Row,c.Column)).Distinct().Count()!=rows*cols)
            throw new InvalidDataException("Incomplete source formatting; nothing will be changed.");
        if(visual.NativeRowHeights.Count!=rows ||
           visual.NativeColumnWidths.Count!=cols ||
           visual.NativeColumnWidths.Any(x=>x<=0 || x>255) ||
           visual.NativeRowHeights.Any(x=>x<=0 || x>409))
            throw new InvalidDataException(
                "Exact native Excel row/column dimensions missing. Recapture source.");
        if(visual.FeaturesV106 is not { } features ||
           features.Cells.Count!=rows*cols)
            throw new InvalidDataException(
                "Source is missing table/underline/input metadata. Recapture using new version.");

        dynamic app=Excel(), sheet=app.ActiveSheet, selected=app.Selection;
        if(ToInt(selected.Areas.Count)!=1)
            throw new InvalidOperationException("Select one contiguous destination range.");
        int sr=ToInt(selected.Row),sc=ToInt(selected.Column);
        int sh=ToInt(selected.Rows.Count),sw=ToInt(selected.Columns.Count);
        if((sh!=1||sw!=1)&&(sh!=rows||sw!=cols))
            throw new InvalidOperationException(
                "Select only the first destination cell or the exact "+rows+"×"+cols+" range.");
        if(sr+rows-1>1048576||sc+cols-1>16384)
            throw new InvalidOperationException("Destination extends outside the worksheet.");
        dynamic target=sheet.Range[sheet.Cells[sr,sc],sheet.Cells[sr+rows-1,sc+cols-1]];
        var order=visual.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
        var from=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        var to=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        var problems=new List<string>();
        bool sourceFormula=false;
        for(int i=0;i<order.Length;i++)
        {
            int r=i/cols,c=i%cols;
            var source=order[i];
            if(source.Row!=r || source.Column!=c)
                throw new InvalidDataException("Source positions are incomplete or out of order.");
            from.Add(new(source.StoredType!="Blank",source.StoredType));
            dynamic cell=target.Cells[r+1,c+1];
            bool formula=IsFormula(cell);
            var raw=(object?)cell.Value2;
            to.Add(new(formula||raw!=null,
                DeskSpaceExcelInputTypeV101.Stored(raw,formula)));
            if(source.FormulaR1C1.Length>0)sourceFormula=true;
        }
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        if(!decision.MayApply)problems.Add(decision.Explanation);
        if((bool)sheet.ProtectContents)problems.Add("Worksheet is protected.");
        if((bool)app.ActiveWorkbook.ReadOnly)problems.Add("Workbook is read-only.");
        // A wholly empty destination may receive *recorded formulas* as part
        // of its reusable Excel structure. Existing populated data/formulas
        // are NEVER overwritten. This is declarative cell state, not macros.
        if(sourceFormula && decision.State==DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty &&
           order.Any(x=>x.StoredType=="Formula" && x.FormulaR1C1.Length==0))
            problems.Add("One or more source formulas lack their exact R1C1 definition.");
        if(features.Cells.Any(c=>c.ValidationType>=0 || c.ConditionalRuleCount>0))
            problems.Add("Source uses data validation or conditional rules not covered by this format-only transaction.");
        if(features.Cells.Any(c=>c.IsArrayFormula))
            problems.Add("Source uses array/spill formulas; format-only apply is not safe.");
        if(features.Cells.Any(c=>c.Superscript || c.Subscript))
            problems.Add("Superscript/subscript characters require rich-text verification.");
        if(features.Tables.Any(x=>!x.FullySelected))
            problems.Add("Source table is only partly captured.");
        if(features.Limitations.Any(x=>
            x.Contains("ListObjects unavailable",StringComparison.OrdinalIgnoreCase) ||
            x.Contains("table object inspection failed",StringComparison.OrdinalIgnoreCase)))
            problems.Add("Source table recognition could not be verified.");
        if(features.Tables.Any(x=>x.TableStyle.Length==0))
            problems.Add("Excel Table style was not captured.");
        foreach(var t in features.Tables)
        {
            if(!t.HasHeaders || t.ColumnNames.Count!=t.ColumnCount ||
               t.ColumnNames.Any(x=>string.IsNullOrWhiteSpace(x)) ||
               t.ColumnNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=t.ColumnCount)
                problems.Add("A source Excel Table has missing/duplicate header metadata.");
            if(decision.State==DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty &&
               t.HasTotals)
                problems.Add("Creating a blank Excel Table with an active Total Row is not yet supported.");
            if(!t.FullySelected || t.Row<0 || t.Column<0 ||
               t.Row+t.RowCount>rows || t.Column+t.ColumnCount>cols)
                problems.Add("Source Excel Table coordinates do not fit the captured range.");
        }
        // A formatted table must be an ACTUAL ListObject in the destination.
        // Never fake a table by colouring a rectangle. Creating a new table
        // can rewrite header values; block rather than silently do that.
        int targetTableCount=0;
        try
        {
            dynamic tableObjects=sheet.ListObjects;
            for(int i=1;i<=ToInt(tableObjects.Count);i++)
            {
                dynamic table=tableObjects[i],area=table.Range;
                int tr=ToInt(area.Row)-sr,tc=ToInt(area.Column)-sc;
                int th=ToInt(area.Rows.Count),tw=ToInt(area.Columns.Count);
                bool overlap=tr<rows&&tc<cols&&tr+th>0&&tc+tw>0;
                if(!overlap)continue;
                targetTableCount++;
                if(!features.Tables.Any(src=>src.Row==tr &&
                    src.Column==tc && src.RowCount==th && src.ColumnCount==tw))
                    problems.Add("An unrelated or differently sized Excel Table overlaps the destination.");
            }
        }
        catch(COMException){problems.Add("Cannot inspect destination Excel Table objects.");}
        bool allEmpty=decision.State==DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
        if(targetTableCount!=features.Tables.Count &&
           !(allEmpty && targetTableCount==0))
            problems.Add("Destination contains an incomplete or overlapping Excel Table structure.");
        if(allEmpty && targetTableCount==0)
        {
            // Names are workbook-global. Reject collisions before any write.
            try
            {
                dynamic workbookSheets=app.ActiveWorkbook.Worksheets;
                for(int sheetNo=1;sheetNo<=ToInt(workbookSheets.Count);sheetNo++)
                {
                    dynamic other=workbookSheets[sheetNo];
                    dynamic tables=other.ListObjects;
                    for(int i=1;i<=ToInt(tables.Count);i++)
                        if(features.Tables.Any(t=>string.Equals(t.Name,
                            Text(tables[i].Name),StringComparison.OrdinalIgnoreCase)))
                            problems.Add("Excel Table name already exists in workbook.");
                }
            }
            catch(COMException){problems.Add("Could not verify workbook-wide Excel Table name uniqueness.");}
        }
        // Merged areas must be pre-existing and match exactly. Changing
        // merge topology can erase data in non-anchor cells.
        foreach(var source in order.Where(x=>x.MergeRows>1||x.MergeColumns>1))
        {
            dynamic cell=target.Cells[source.Row+1,source.Column+1];
            if(!(bool)cell.MergeCells)
            {
                if(!allEmpty)
                    problems.Add("Missing pre-existing merged destination at "+
                        Text(cell.Address));
                continue;
            }
            dynamic merge=cell.MergeArea;
            if(ToInt(merge.Row)!=sr+source.Row ||
               ToInt(merge.Column)!=sc+source.Column ||
               ToInt(merge.Rows.Count)!=source.MergeRows ||
               ToInt(merge.Columns.Count)!=source.MergeColumns)
                problems.Add("Merged-cell geometry mismatch at "+Text(cell.Address));
        }
        // Prevent overwriting the captured source workbook or accidental
        // changes when the user simply selects the same region again.
        if(string.Equals(Text(app.ActiveWorkbook.Name),template.SourceWorkbook,
                StringComparison.OrdinalIgnoreCase) &&
           string.Equals(Text(sheet.Name),template.SourceSheet,
                StringComparison.OrdinalIgnoreCase) &&
           Norm(Text(target.Address))==Norm(template.SourceAddress))
            problems.Add("This is the original captured source range, not a destination.");
        return new Checked{
            Excel=app,Sheet=sheet,Range=target,Visual=visual,
            FirstRow=sr,FirstColumn=sc,
            Review=new(Text(app.ActiveWorkbook.FullName),Text(sheet.Name),
                Text(target.Address),decision,problems)
        };
    }
    internal static Review Preview(DeskSpaceExcelTemplateV16 template)=>
        Inspect(template).Review;

    private static void WriteCell(dynamic cell,DeskSpaceExcelStyledCellV101 s,
        DeskSpaceExcelCellFeatureV106? feature)
    {
        cell.NumberFormat=s.NumberFormat;
        cell.Font.Name=s.FontName;
        cell.Font.Size=s.FontSize;
        cell.Font.Bold=s.Bold;
        cell.Font.Italic=s.Italic;
        cell.Font.Strikethrough=s.StrikeThrough;
        cell.Font.Underline=feature?.UnderlineStyle??(s.Underline?2:-4142);
        cell.Font.Color=s.FontBgr;
        cell.Interior.Color=s.FillBgr;
        cell.HorizontalAlignment=s.HorizontalAlignment;
        cell.VerticalAlignment=s.VerticalAlignment;
        cell.WrapText=s.WrapText;
        cell.ShrinkToFit=s.ShrinkToFit;
        cell.IndentLevel=s.IndentLevel;
        // Neither formulas nor values are changed by format-only application.
        for(int i=0;i<4;i++)
        {
            int edge=new[]{7,8,9,10}[i];
            bool present=new[]{s.BorderLeft,s.BorderTop,s.BorderBottom,s.BorderRight}[i];
            dynamic border=cell.Borders[edge];
            border.LineStyle=present?1:-4142;
            if(!present)continue;
            border.Color=new[]{s.BorderLeftColor,s.BorderTopColor,
                s.BorderBottomColor,s.BorderRightColor}[i];
            int weight=new[]{s.BorderLeftWeight,s.BorderTopWeight,
                s.BorderBottomWeight,s.BorderRightWeight}[i];
            if(weight!=0)border.Weight=weight;
        }
    }
    private static void WriteSnapshot(dynamic target,DeskSpaceExcelSheetVisualV101 snap,
        bool writeTables)
    {
        if(snap.NativeColumnWidths.Count!=snap.Columns ||
           snap.NativeRowHeights.Count!=snap.Rows)
            throw new InvalidDataException("Missing exact original Excel dimensions.");
        var feature=snap.FeaturesV106?.Cells.ToDictionary(c=>(c.Row,c.Column));
        var mergedUnderlying=new HashSet<(int,int)>();
        foreach(var anchor in snap.Cells.Where(x=>x.MergeRows>1 || x.MergeColumns>1))
            for(int mr=anchor.Row;mr<anchor.Row+anchor.MergeRows;mr++)
                for(int mc=anchor.Column;mc<anchor.Column+anchor.MergeColumns;mc++)
                    if(mr!=anchor.Row || mc!=anchor.Column)
                        mergedUnderlying.Add((mr,mc));
        foreach(var s in snap.Cells.OrderBy(c=>c.Row).ThenBy(c=>c.Column))
        {
            // Never individually edit a non-anchor cell inside a merged area;
            // Excel rejects it and could otherwise corrupt the merge.
            if(mergedUnderlying.Contains((s.Row,s.Column)))continue;
            // Avoid relying on an out variable from null-conditional dynamic access.
            DeskSpaceExcelCellFeatureV106? item=
                feature!=null && feature.TryGetValue((s.Row,s.Column),out var found)?
                    found:null;
            WriteCell(target.Cells[s.Row+1,s.Column+1],s,item);
        }
        for(int i=0;i<snap.NativeRowHeights.Count;i++)
            target.Rows[i+1].RowHeight=snap.NativeRowHeights[i];
        // Excel ColumnWidth uses character units, not points or pixels.
        for(int i=0;i<snap.NativeColumnWidths.Count;i++)
            target.Columns[i+1].ColumnWidth=snap.NativeColumnWidths[i];
        if(writeTables && snap.FeaturesV106!=null)
        {
            dynamic sheet=target.Worksheet;
            foreach(var metadata in snap.FeaturesV106.Tables)
            {
                dynamic tRange=sheet.Range[
                    target.Cells[metadata.Row+1,metadata.Column+1],
                    target.Cells[metadata.Row+metadata.RowCount,
                        metadata.Column+metadata.ColumnCount]];
                bool found=false;
                dynamic objects=sheet.ListObjects;
                for(int i=1;i<=ToInt(objects.Count);i++)
                {
                    dynamic table=objects[i];
                    if(Norm(Text(table.Range.Address))!=Norm(Text(tRange.Address)))
                        continue;
                    table.TableStyle=metadata.TableStyle;
                    table.ShowTableStyleRowStripes=metadata.RowStripes;
                    table.ShowTableStyleColumnStripes=metadata.ColumnStripes;
                    table.ShowTableStyleFirstColumn=metadata.FirstColumnEmphasis;
                    table.ShowTableStyleLastColumn=metadata.LastColumnEmphasis;
                    table.ShowAutoFilter=metadata.ShowAutoFilter;
                    if((bool)table.ShowTotals!=metadata.HasTotals ||
                       (bool)table.ShowHeaders!=metadata.HasHeaders)
                        throw new InvalidOperationException(
                            "Destination table header/totals configuration differs.");
                    found=true;break;
                }
                if(!found)throw new InvalidOperationException("Excel table disappeared before apply.");
            }
        }
    }
    // Explicit, supervised call; users must have previewed and approved.
    internal static string Apply(DeskSpaceExcelTemplateV16 template,Review approved)
    {
        var live=Inspect(template);
        if(!live.Review.Eligible)
            throw new InvalidOperationException("Cannot apply format: "+live.Review);
        if(live.Review.Workbook!=approved.Workbook ||
           live.Review.Sheet!=approved.Sheet ||
           live.Review.Destination!=approved.Destination ||
           live.Review.Gate.State!=approved.Gate.State)
            throw new InvalidOperationException("Target changed after preview. Preview again.");
        dynamic book=live.Excel.ActiveWorkbook;
        var backupFolder=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","ExcelBackups");
        Directory.CreateDirectory(backupFolder);
        var extension=Path.GetExtension(Text(book.Name));
        if(extension.Length==0)extension=".xlsx";
        var backup=Path.Combine(backupFolder,
            "Before_Excel_Formatting_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+
            extension);
        book.SaveCopyAs(backup);
        if(!File.Exists(backup) || new FileInfo(backup).Length==0)
            throw new IOException("Could not create safety copy; nothing was changed.");
        DeskSpaceExcelSheetVisualV101 before=
            DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
        // Original stored input/formulas are never copied or changed.
        var signatures=new List<(string Address,string Formula,string Value)>();
        for(int r=1;r<=live.Visual.Rows;r++)
        for(int c=1;c<=live.Visual.Columns;c++)
        {
            dynamic cell=live.Range.Cells[r,c];
            signatures.Add((Text(cell.Address),
                IsFormula(cell)?Text(cell.FormulaR1C1):"",
                Text((object?)cell.Value2)));
        }
        try
        {
            WriteSnapshot(live.Range,live.Visual,true);
            DeskSpaceExcelSheetVisualV101 after=
                DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
            var source=live.Visual.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
            var actual=after.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
            for(int i=0;i<source.Length;i++)
            {
                var a=actual[i];
                var s=source[i];
                if(s.NumberFormat!=a.NumberFormat ||
                   s.FontName!=a.FontName ||
                   Math.Abs(s.FontSize-a.FontSize)>0.5 ||
                   s.Bold!=a.Bold || s.Italic!=a.Italic ||
                   s.Underline!=a.Underline || s.StrikeThrough!=a.StrikeThrough ||
                   s.FillBgr!=a.FillBgr || s.FontBgr!=a.FontBgr ||
                   s.BorderLeft!=a.BorderLeft||s.BorderRight!=a.BorderRight ||
                   s.BorderTop!=a.BorderTop||s.BorderBottom!=a.BorderBottom)
                    throw new InvalidDataException(
                        "Formatting readback differs at "+
                        Text(live.Range.Cells[s.Row+1,s.Column+1].Address));
                dynamic cell=live.Range.Cells[s.Row+1,s.Column+1];
                var signature=signatures[i];
                if(signature.Formula!=(IsFormula(cell)?Text(cell.FormulaR1C1):"") ||
                   signature.Value!=Text((object?)cell.Value2))
                    throw new InvalidDataException(
                        "An existing Excel value/formula changed unexpectedly.");
            }
            // Verify EXACT underline styles, not merely "underlined=true".
            var requestedFeatures=live.Visual.FeaturesV106!;
            var measuredFeatures=after.FeaturesV106 ??
                throw new InvalidDataException("Excel feature readback unavailable.");
            var byPosition=measuredFeatures.Cells.ToDictionary(x=>(x.Row,x.Column));
            foreach(var wanted in requestedFeatures.Cells)
            {
                if(!byPosition.TryGetValue((wanted.Row,wanted.Column),out var observed))
                    throw new InvalidDataException("Missing cell during Excel feature readback.");
                if(wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.TableRole!=observed.TableRole ||
                   wanted.Locked!=observed.Locked ||
                   wanted.FormulaHidden!=observed.FormulaHidden)
                    throw new InvalidDataException("Excel underline, table role or protection mismatch at "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
            }
            foreach(var expectedTable in requestedFeatures.Tables)
            {
                if(!measuredFeatures.Tables.Any(x=>x.Row==expectedTable.Row &&
                    x.Column==expectedTable.Column && x.RowCount==expectedTable.RowCount &&
                    x.ColumnCount==expectedTable.ColumnCount &&
                    x.TableStyle==expectedTable.TableStyle &&
                    x.HasHeaders==expectedTable.HasHeaders &&
                    x.HasTotals==expectedTable.HasTotals &&
                    x.RowStripes==expectedTable.RowStripes &&
                    x.ColumnStripes==expectedTable.ColumnStripes &&
                    x.FirstColumnEmphasis==expectedTable.FirstColumnEmphasis &&
                    x.LastColumnEmphasis==expectedTable.LastColumnEmphasis &&
                    x.ShowAutoFilter==expectedTable.ShowAutoFilter))
                    throw new InvalidDataException("A real Excel Table structure/style was not reproduced.");
            }
            for(int row=0;row<live.Visual.Rows;row++)
                if(Math.Abs(live.Visual.NativeRowHeights[row]-after.NativeRowHeights[row])>0.2)
                    throw new InvalidDataException("Excel row height readback mismatch.");
            for(int col=0;col<live.Visual.Columns;col++)
                if(Math.Abs(live.Visual.NativeColumnWidths[col]-after.NativeColumnWidths[col])>0.3)
                    throw new InvalidDataException("Excel native column width readback mismatch.");
            // Do not save the open workbook behind the user's back.
            return "Verified "+source.Length+" cells, "+requestedFeatures.Tables.Count+
                " real Excel tables and exact underline styles. Existing values preserved. "+
                "Safety copy: "+backup;
        }
        catch
        {
            // Best-effort in-memory rollback, backed by an independent
            // SaveCopyAs file even if COM is in an unusable state.
            try{WriteSnapshot(live.Range,before,true);}catch(COMException){}
            throw new InvalidOperationException(
                "Excel formatting could not be fully verified. A pre-change "+
                "workbook copy is at "+backup+". Review before saving.");
        }
    }
}
