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
        DeskSpaceExcelFullRangeGateV107.Decision Gate,IReadOnlyList<string> Blockers,
        string PlannedActions)
    {
        internal bool Eligible=>Gate.MayApply && Blockers.Count==0;
        public override string ToString()=>Gate.Explanation+
            Environment.NewLine+PlannedActions+
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
    // Excel requires internal ListObject column names. They must not be
    // copied from the saved source, and the destination's own names
    // must never be compared against the captured workbook's headers.
    internal static string StructuralHeaderV123(int column)=>
        "Column"+(column+1).ToString(CultureInfo.InvariantCulture);
    internal static bool HeadersIndependentV123(
        IEnumerable<string> destination,IEnumerable<string> source)=>
        destination.All(x=>!string.IsNullOrWhiteSpace(x)) &&
        destination.Distinct(StringComparer.OrdinalIgnoreCase).Count()==
            destination.Count();

    private sealed class Checked
    {
        internal required dynamic Excel;
        internal required dynamic Sheet;
        internal required dynamic Range;
        internal required DeskSpaceExcelSheetVisualV101 Visual;
        internal required Review Review;
        internal required int FirstRow;
        internal required int FirstColumn;
        internal required IReadOnlyList<DeskSpaceExcelFormulaPlanV266.Placement>
            FormulaPlanV266;
    }
    // Excel can reject a TableStyle setter even after a template is saved.
    // Verify that all requested styles exist in THIS workbook before any
    // SaveCopyAs, table creation or other mutation. Fail closed when Office
    // cannot verify style availability instead of risking a partial write.
    private static void PreflightTableStylesV131(
        dynamic workbook,IEnumerable<DeskSpaceExcelTableV106> tables,
        List<string> problems)
    {
        foreach(var name in tables.Select(t=>t.TableStyle)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(string.IsNullOrWhiteSpace(name) ||
                name.StartsWith("System._ComObject",StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("This saved mapping has an invalid legacy Excel table-style reference (System._ComObject). Open the original source workbook, select the original cells, then use Format / Formula COPY and Save Mapping to recapture. Ctrl+T alone will not repair saved table metadata. No workbook cells were changed.");
                continue;
            }
            try
            {
                dynamic available=workbook.TableStyles[name];
                string found=Text((object?)available.Name);
                if(!string.Equals(found,name,StringComparison.OrdinalIgnoreCase))
                    problems.Add("Table style '"+name+
                        "' does not resolve in the destination workbook.");
            }
            catch(Exception error) when(error is COMException ||
                error is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ||
                error is System.Reflection.TargetInvocationException ||
                error is ArgumentException)
            {
                problems.Add("Table style '"+name+
                    "' is unavailable or cannot be verified in the destination workbook: "+
                    error.Message+". No workbook changes will be made.");
            }
        }
    }
    private static void SetTableStyleV131(dynamic table,string required)
    {
        try
        {
            var current=TableStyleNameV132((object?)table.TableStyle);
            // Avoid redundant Excel COM setter calls. On some versions of
            // Office this setter throws even though the style is unchanged.
            if(string.Equals(current,required,StringComparison.OrdinalIgnoreCase))
                return;
            table.TableStyle=required;
            var observed=TableStyleNameV132((object?)table.TableStyle);
            if(!string.Equals(observed,required,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Excel TableStyle readback differs from the requested style.");
        }
        catch(Exception error) when(error is COMException ||
            error is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ||
            error is System.Reflection.TargetInvocationException)
        {
            throw new InvalidOperationException(
                "Excel could not set or read table style '"+required+
                "'. The style must be supported by the destination workbook.",error);
        }
    }
    // ListObject.TableStyle is a Variant: Excel can return a COM TableStyle
    // object instead of a string. Never compare the COM wrapper's ToString().
    private static string TableStyleNameV132(object? raw)
    {
        if(raw is string name)return name;
        if(raw==null)return "";
        if(Marshal.IsComObject(raw))
        {
            dynamic style=raw;
            object? styleName=style.Name;
            if(styleName is string resolved)return resolved;
        }
        return "";
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
        // Formula definitions remain saved in the FORMAT library.
        // A formatting-only transaction NEVER copies formulas to blank cells.
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
            // Do not inspect source formula text for blank-destination values.
        }
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        // Reject a broken source template even when the target is blank.
        // Never rely on the old sourceFormula flag (all source formulas
        // could be absent or contradictory).
        if(order.Any(x=>!DeskSpaceFormulaMetadataV251.Valid(
            x.StoredType,x.FormulaR1C1)))
            problems.Add("Captured formula metadata is incomplete, invalid or "+
                "attached to a non-formula cell. Recapture source.");
        if(!decision.MayApply &&
           DeskSpaceExcelFormulaPasteV143.Ready(order,to))
            decision=new DeskSpaceExcelFullRangeGateV107.Decision(
                DeskSpaceExcelFullRangeGateV107.Status.CompleteMatchingLayout,
                to.Count,to.Count(c=>c.HasData),from.Count(c=>c.HasData),
                DeskSpaceExcelFormulaPasteV143.Explanation);
        if(!decision.MayApply)problems.Add(decision.Explanation);
        // R147 shared formatting/formula paste preflight. A saved formula is
        // not optional data: it belongs in this same mapping operation.
        var formulaSourcesV147=order.Where(c=>c.StoredType=="Formula")
            .Select(c=>new DeskSpaceExcelCombinedFormulaV147.Cell(
                c.Row,c.Column,c.FormulaA1,c.FormulaR1C1)).ToArray();
        if(formulaSourcesV147.Length>0)
        {
            try
            {
                var currentV147=new List<DeskSpaceExcelCombinedFormulaV147.Target>();
                foreach(var formulaV147 in formulaSourcesV147)
                {
                    dynamic dest=target.Cells[formulaV147.Row+1,formulaV147.Column+1];
                    bool exists=IsFormula(dest);
                    currentV147.Add(new(formulaV147.Row,formulaV147.Column,
                        exists,!exists && (object?)dest.Value2!=null,
                        exists?Text(dest.FormulaR1C1):""));
                }
                DeskSpaceExcelCombinedFormulaV147.Inspect(
                    formulaSourcesV147,currentV147);
            }
            catch(InvalidDataException error)
            {
                problems.Add("Combined formats/formulas blocked: "+error.Message);
            }
        }
        if((bool)sheet.ProtectContents)problems.Add("Worksheet is protected.");
        if((bool)app.ActiveWorkbook.ReadOnly)problems.Add("Workbook is read-only.");
        // Formula expressions are kept in the captured template but not
        // copied into a blank destination: formatting-only must not show 0.
        // Existing populated destination values/formulas stay protected.
        var mergedChildren=new HashSet<(int,int)>();
        foreach(var parent in order.Where(x=>x.MergeRows>1 || x.MergeColumns>1))
            for(int mr=parent.Row;mr<parent.Row+parent.MergeRows;mr++)
                for(int mc=parent.Column;mc<parent.Column+parent.MergeColumns;mc++)
                    if(mr!=parent.Row || mc!=parent.Column)
                        mergedChildren.Add((mr,mc));
        // Restore actual recorded formulas when FORMATS+FORMULAS is pasted
        // onto a wholly blank target. This is strictly cell-position based:
        // no source body values, empty source values, or synthetic zeros.
        // Validate all formula metadata and header/merge topology before
        // calling SaveCopyAs or writing ANY Excel cells.
        IReadOnlyList<DeskSpaceExcelFormulaPlanV266.Placement> formulaPlanV266=
            Array.Empty<DeskSpaceExcelFormulaPlanV266.Placement>();
        try
        {
            var inputsV266=order.Select(cell=>
                new DeskSpaceExcelFormulaPlanV266.Input(
                    cell.Row,cell.Column,cell.StoredType,cell.FormulaR1C1,
                    mergedChildren.Contains((cell.Row,cell.Column)),
                    features.Tables.Any(t=>t.HasHeaders && cell.Row==t.Row &&
                        cell.Column>=t.Column &&
                        cell.Column<t.Column+t.ColumnCount)))
                .ToArray();
            formulaPlanV266=DeskSpaceExcelFormulaPlanV266.Build(
                inputsV266,rows,cols);
        }
        catch(InvalidDataException)
        {
            problems.Add("Recorded formulas or positions are invalid. "+
                "Recapture the template before pasting.");
        }
        if(order.Any(c=>!mergedChildren.Contains((c.Row,c.Column)) &&
            (c.DirectFillPattern==int.MinValue ||
             c.DirectFontColorIndex==int.MinValue ||
             (c.DirectFillPattern!=1 && c.DirectFillPattern!=-4142 &&
              c.DirectFillPattern!=-4105))))
            problems.Add("Some source cells have unsupported or unreadable direct fill-pattern/font-colour metadata.");
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
        PreflightTableStylesV131(app.ActiveWorkbook,features.Tables,problems);
        foreach(var t in features.Tables)
        {
            // The recorded table footprint is structural. Header TEXT is
            // value data and is irrelevant for format/formula application.
            if(!t.HasHeaders || t.ColumnCount<1)
                problems.Add("A source Excel Table lacks a valid header row structure.");
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
                var sourceTable=features.Tables.FirstOrDefault(src=>
                    src.Row==tr && src.Column==tc &&
                    src.RowCount==th && src.ColumnCount==tw);
                if(sourceTable==null)
                    problems.Add("An unrelated or differently sized Excel Table overlaps the destination.");
                else
                {
                    // Existing tables retain their OWN column names.
                    // Only geometric/header visibility compatibility matters.
                    var currentNames=new string[tw];
                    for(int headerIndex=0;headerIndex<tw;headerIndex++)
                        currentNames[headerIndex]=Text(
                            (object?)table.ListColumns[headerIndex+1].Name);
                    if(!HeadersIndependentV123(currentNames,sourceTable.ColumnNames))
                        problems.Add("Destination Excel Table has blank/duplicate column names.");
                    if((bool)table.ShowHeaders!=sourceTable.HasHeaders ||
                       (bool)table.ShowTotals!=sourceTable.HasTotals)
                        problems.Add("Destination Excel Table header/totals settings differ.");
                }
            }
        }
        catch(COMException){problems.Add("Cannot inspect destination Excel Table objects.");}
        bool allEmpty=decision.State==DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
        bool fullData=decision.State==
            DeskSpaceExcelFullRangeGateV107.Status.CompleteMatchingLayout;
        if(targetTableCount!=features.Tables.Count &&
           !(targetTableCount==0 && (allEmpty || fullData)))
            problems.Add("Destination contains an incomplete or overlapping Excel Table structure.");
        if(fullData && targetTableCount==0)
        {
            foreach(var sourceTable in features.Tables)
            {
                var destinationLabels=new List<string>();
                for(int c=0;c<sourceTable.ColumnCount;c++)
                {
                    dynamic header=target.Cells[sourceTable.Row+1,
                        sourceTable.Column+c+1];
                    destinationLabels.Add(Text((object?)header.Value2));
                }
                if(!HeadersIndependentV123(destinationLabels,sourceTable.ColumnNames))
                    problems.Add("Populated destination Excel Table needs unique, nonempty OWN column names, not source headers.");
            }
        }
        // R146: source Table1 already existing elsewhere is not a conflict.
        // R132 creates a new ListObject with Excel's own unique Name.
        // Still reject overlapping existing tables, invalid structures,
        // mismatched cells and unsafe placement using checks above.
        // Merged areas must be pre-existing and match exactly. Changing
        // merge topology can erase data in non-anchor cells.
        foreach(var source in order.Where(x=>x.MergeRows>1||x.MergeColumns>1))
        {
            dynamic cell=target.Cells[source.Row+1,source.Column+1];
            if(!(bool)cell.MergeCells)
            {
                if(!allEmpty && !fullData)
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
        // An unexpected merge anywhere inside the destination could alter
        // unrelated structural cells. Preflight ALL physical cells, not only
        // the expected source merge anchors.
        for(int rr=0;rr<rows;rr++)
        for(int cc=0;cc<cols;cc++)
        {
            dynamic physical=target.Cells[rr+1,cc+1];
            if(!(bool)physical.MergeCells)continue;
            dynamic actualMerge=physical.MergeArea;
            int mr=ToInt(actualMerge.Row)-sr;
            int mc=ToInt(actualMerge.Column)-sc;
            int mh=ToInt(actualMerge.Rows.Count);
            int mw=ToInt(actualMerge.Columns.Count);
            if(!order.Any(src=>src.Row==mr && src.Column==mc &&
                src.MergeRows==mh && src.MergeColumns==mw &&
                (mh>1||mw>1)))
            {
                problems.Add("Destination contains an unrelated merged area at "+
                    Text(actualMerge.Address)+". No formatting is permitted.");
                rr=rows;break;
            }
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
            FormulaPlanV266=formulaPlanV266,
            Review=new(Text(app.ActiveWorkbook.FullName),Text(sheet.Name),
                Text(target.Address),decision,problems,
                allEmpty
                    ?"Whole destination empty. Will create "+features.Tables.Count+
                      " real Excel table(s), "+order.Count(x=>x.MergeRows>1 ||
                      x.MergeColumns>1)+" merged area(s), insert "+
                      features.Tables.Sum(x=>x.ColumnNames.Count)+
                      " Excel-generated neutral table column names. "+
                      order.Count(x=>x.StoredType=="Formula")+
                      " saved R1C1 formulas will be inserted into their "+
                      "original positions. Other input cells stay blank."
                    :"All input positions match. "+
                      (targetTableCount==0 && features.Tables.Count>0?
                          "Create "+features.Tables.Count+" real table(s) using destination's existing columns; ":"")+
                      "Apply cell-specific formatting. "+
                      (decision.Explanation==DeskSpaceExcelFormulaPasteV143.Explanation?
                          "Restore only saved formulas into verified empty formula cells; "+
                          "retain all user-entered inputs.":
                          "Existing values and formulas retained."))
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
        // Do not paint every table cell white or set every font black:
        // those explicit overrides suppress Excel's real ListObject styling.
        // Restore the source DIRECT style layer, never the computed display
        // colour from DisplayFormat. Excel recreates the display via TableStyle.
        // Colour mode takes precedence over derived workbook display RGB.
        // Excel frequently reports ThemeColor=2 even for an AUTOMATIC font.
        // Preserve exact source DIRECT mode; ListObject controls appearance.
        if(s.DirectFontColorIndex==-4105)
            cell.Font.ColorIndex=-4105;
        else if(s.DirectFontThemeColor>0)
            cell.Font.ThemeColor=s.DirectFontThemeColor;
        else
            cell.Font.Color=s.FontBgr;
        if(s.DirectFontColorIndex!=-4105 &&
           s.DirectFontTintAndShade.HasValue)
            cell.Font.TintAndShade=s.DirectFontTintAndShade.Value;
        if(s.DirectFillPattern==-4142)
            cell.Interior.Pattern=-4142;
        else if(s.DirectFillPattern==-4105)
            cell.Interior.Pattern=-4105;
        else if(s.DirectFillPattern==1)
        {
            cell.Interior.Pattern=1;
            if(s.DirectFillThemeColor>0)
                cell.Interior.ThemeColor=s.DirectFillThemeColor;
            else
                cell.Interior.Color=s.FillBgr;
        }
        else
            throw new InvalidDataException("Unsupported direct Excel fill pattern.");
        if(s.DirectFillPattern==1 && s.DirectFillTintAndShade.HasValue)
            cell.Interior.TintAndShade=s.DirectFillTintAndShade.Value;
        cell.HorizontalAlignment=s.HorizontalAlignment;
        cell.VerticalAlignment=s.VerticalAlignment;
        cell.WrapText=s.WrapText;
        cell.ShrinkToFit=s.ShrinkToFit;
        cell.IndentLevel=s.IndentLevel;
        cell.Orientation=s.TextOrientation;
        if(feature!=null)
        {
            if(feature.Superscript && feature.Subscript)
                throw new InvalidDataException(
                    "Source cell has contradictory superscript/subscript metadata.");
            cell.Font.Superscript=feature.Superscript;
            cell.Font.Subscript=feature.Subscript;
            // These settings are security metadata. Excel sometimes rejects
            // a write on a table/range even when other formats succeed.
            // Write only when necessary; the readback below reports any
            // protection that Excel refuses to copy without weakening the
            // visual/table/value/formula gates.
            DeskSpaceExcelProtectionV141.Apply(
                cell,feature.Locked,feature.FormulaHidden);
        }
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
    // Only called after a full-range gate and complete SaveCopyAs backup.
    // On an empty target, structural table headers are created. On a fully
    // populated target, headers must have been verified to match already.
    private static void BuildMissingStructure(Checked live,bool empty)
    {
        var features=live.Visual.FeaturesV106 ??
            throw new InvalidDataException("No structural Excel metadata.");
        foreach(var table in features.Tables)
        {
            dynamic region=live.Sheet.Range[
                live.Range.Cells[table.Row+1,table.Column+1],
                live.Range.Cells[table.Row+table.RowCount,
                    table.Column+table.ColumnCount]];
            bool exists=false;
            dynamic objects=live.Sheet.ListObjects;
            for(int priorIndex=1;priorIndex<=ToInt(objects.Count);priorIndex++)
                if(Norm(Text(objects[priorIndex].Range.Address))==
                   Norm(Text(region.Address)))
                    exists=true;
            if(exists)continue;
            if(empty)
                for(int i=0;i<table.ColumnCount;i++)
                    region.Cells[1,i+1].Value2=StructuralHeaderV123(i);
            // Microsoft Excel: xlSrcRange=1, xlYes=1. Source is an
            // explicitly sized Range; omit LinkSource with Type.Missing.
            dynamic created=live.Sheet.ListObjects.Add(
                1,region,Type.Missing,1);
            // Excel ListObject names are workbook-global. The source may
            // still have Table1 in this SAME workbook: preserve Excel's unique
            // generated destination name, never steal/duplicate source ID.
            SetTableStyleV131(created,table.TableStyle);
            created.ShowTableStyleRowStripes=table.RowStripes;
            created.ShowTableStyleColumnStripes=table.ColumnStripes;
            created.ShowTableStyleFirstColumn=table.FirstColumnEmphasis;
            created.ShowTableStyleLastColumn=table.LastColumnEmphasis;
            created.ShowAutoFilter=table.ShowAutoFilter;
            // Excel may normalise its own generated names. Ensure unique
            // valid structural names without demanding SOURCE header data.
            var createdNames=new string[table.ColumnCount];
            for(int i=0;i<table.ColumnCount;i++)
                createdNames[i]=Text((object?)created.ListColumns[i+1].Name);
            if(!HeadersIndependentV123(createdNames,table.ColumnNames))
                throw new InvalidDataException(
                    "Excel Table creation produced invalid structural names.");
        }
        foreach(var merge in live.Visual.Cells.Where(x=>
            x.MergeRows>1 || x.MergeColumns>1))
        {
            dynamic area=live.Sheet.Range[
                live.Range.Cells[merge.Row+1,merge.Column+1],
                live.Range.Cells[merge.Row+merge.MergeRows,
                    merge.Column+merge.MergeColumns]];
            if(!(bool)area.MergeCells)area.Merge(false);
        }
    }
    // R266: reinsert only verified captured R1C1 formulas after R136 has
    // removed Excel-generated calculated-column contents. Never copy a
    // source literal, an artificial zero, or a formula onto existing data.
    private static void RestoreCapturedFormulasV266(Checked live)
    {
        foreach(var item in live.FormulaPlanV266)
        {
            dynamic cell=live.Range.Cells[item.Row+1,item.Column+1];
            if(IsFormula(cell) || (object?)cell.Value2!=null)
                throw new InvalidDataException(
                    "Formula destination was changed during formatting.");
            cell.FormulaR1C1=item.FormulaR1C1;
        }
    }

    // Excel may automatically fill calculated ListObject columns while the
    // genuine Table is being created. On a verified ENTIRELY EMPTY target,
    // remove ONLY Excel-generated contents outside required table headers.
    // This intentionally does NOT run on partially/full-populated targets.
    private static void KeepBlankDestinationValuesV136(Checked live)
    {
        var tables=live.Visual.FeaturesV106!.Tables;
        var mergedChildren=new HashSet<(int,int)>();
        foreach(var anchor in live.Visual.Cells.Where(c=>
            c.MergeRows>1 || c.MergeColumns>1))
            for(int r=anchor.Row;r<anchor.Row+anchor.MergeRows;r++)
            for(int c=anchor.Column;c<anchor.Column+anchor.MergeColumns;c++)
                if(r!=anchor.Row || c!=anchor.Column)
                    mergedChildren.Add((r,c));
        for(int r=0;r<live.Visual.Rows;r++)
        for(int c=0;c<live.Visual.Columns;c++)
        {
            // Excel requires nonempty unique header identifiers for genuine
            // ListObjects. No source transaction/body values are permitted.
            string header=DeskSpaceExcelBlankFormatPolicyV136.
                AllowedStructuralHeader(r,c,tables);
            if(header.Length>0 || mergedChildren.Contains((r,c)))continue;
            dynamic cell=live.Range.Cells[r+1,c+1];
            if(IsFormula(cell) || (object?)cell.Value2!=null)
                cell.ClearContents();
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
                    SetTableStyleV131(table,metadata.TableStyle);
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
    // Best-effort rollback, performed only after a verified SaveCopyAs.
    // The backup remains authoritative if Excel refuses any COM operation.
    private static void RestoreAfterFailure(Checked live,
        DeskSpaceExcelSheetVisualV101 before,bool originallyEmpty)
    {
        {
            // Newly created tables/merges must be reverted in BOTH allowed
            // destination states: entirely empty or fully populated.
            dynamic tables=live.Sheet.ListObjects;
            // Remove only tables occupying exact newly constructed positions.
            // Iterate backwards because Unlist changes the collection.
            for(int i=ToInt(tables.Count);i>=1;i--)
            {
                dynamic table=tables[i],area=table.Range;
                int row=ToInt(area.Row)-live.FirstRow;
                int col=ToInt(area.Column)-live.FirstColumn;
                int height=ToInt(area.Rows.Count),width=ToInt(area.Columns.Count);
                bool wasThere=before.FeaturesV106?.Tables.Any(t=>
                    t.Row==row && t.Column==col &&
                    t.RowCount==height && t.ColumnCount==width)==true;
                bool created=live.Visual.FeaturesV106?.Tables.Any(t=>
                    t.Row==row && t.Column==col &&
                    t.RowCount==height && t.ColumnCount==width)==true;
                if(created && !wasThere)table.Unlist();
            }
            foreach(var anchor in live.Visual.Cells.Where(x=>
                x.MergeRows>1 || x.MergeColumns>1))
            {
                var prior=before.Cells.FirstOrDefault(x=>
                    x.Row==anchor.Row && x.Column==anchor.Column);
                if(prior!=null && prior.MergeRows==anchor.MergeRows &&
                   prior.MergeColumns==anchor.MergeColumns)continue;
                dynamic area=live.Sheet.Range[
                    live.Range.Cells[anchor.Row+1,anchor.Column+1],
                    live.Range.Cells[anchor.Row+anchor.MergeRows,
                        anchor.Column+anchor.MergeColumns]];
                area.UnMerge();
            }
            // Never clear the user's pre-existing full-data destination.
            // Only a blank original may have our new table headers or
            // Excel-generated contents erased during rollback.
            if(originallyEmpty)live.Range.ClearContents();
            else
            {
                // In formula-ready mode, only previously blank source slots
                // may have received new content. On failure, remove new
                // content from those slots, never from pre-existing inputs.
                foreach(var prior in before.Cells.Where(c=>c.StoredType=="Blank"))
                {
                    dynamic cell=live.Range.Cells[prior.Row+1,prior.Column+1];
                    if((bool)cell.MergeCells)
                    {
                        dynamic region=cell.MergeArea;
                        if(ToInt(region.Row)!=live.FirstRow+prior.Row ||
                           ToInt(region.Column)!=live.FirstColumn+prior.Column)
                            continue;
                    }
                    if(IsFormula(cell) || (object?)cell.Value2!=null)
                        cell.ClearContents();
                }
            }
        }
        WriteSnapshot(live.Range,before,true);
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
        // Keep the original SaveCopyAs authoritative. Record and verify
        // its SHA-256 BEFORE touching any destination cell or table.
        // Never overwrite a previous backup receipt silently.
        string backupReceiptV252=
            DeskSpaceExcelBackupReceiptV252.Create(backup);
        if(!DeskSpaceExcelBackupReceiptV252.Verify(backup,backupReceiptV252))
            throw new IOException(
                "Safety copy checksum receipt invalid; Excel was not modified.");
        DeskSpaceExcelSheetVisualV101 before=
            DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
        var originalProtectionV141=(before.FeaturesV106?.Cells ??
            throw new InvalidDataException(
                "Cannot verify destination cell protection baseline."))
            .ToDictionary(x=>(x.Row,x.Column));
        int unavailableProtectionV141=0;
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
        bool guardedAutoFillV145=false,priorAutoFillV145=true;
        try
        {
            if(live.Visual.FeaturesV106!.Tables.Count>0)
            {
                // Fail BEFORE any sheet mutation if Excel cannot suspend
                // automatic formula fill. Restore original setting in finally.
                priorAutoFillV145=(bool)live.Excel.AutoCorrect.AutoFillFormulasInLists;
                live.Excel.AutoCorrect.AutoFillFormulasInLists=false;
                guardedAutoFillV145=true;
            }
            bool originallyEmpty=live.Review.Gate.State==
                DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
            bool restoreFormulasV143=
                live.Review.Gate.Explanation==
                    DeskSpaceExcelFormulaPasteV143.Explanation;
            BuildMissingStructure(live,originallyEmpty);
            WriteSnapshot(live.Range,live.Visual,true);
            if(originallyEmpty)
            {
                KeepBlankDestinationValuesV136(live);
                RestoreCapturedFormulasV266(live);
            }
            else if(restoreFormulasV143)
                DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(
                    live.Range,live.Visual.Cells);
            DeskSpaceExcelSheetVisualV101 after=
                DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
            // Only true ListObject cells may use structural/table-style
            // verification instead of comparing Excel-generated DisplayFormat
            // pixel colours, which may vary between workbooks and neutral
            // destination table headers. ALL direct cell-format checks remain.
            var sourceTablesV135=live.Visual.FeaturesV106!.Tables;
            var source=live.Visual.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
            var actual=after.Cells.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
            var mergeChildren=new HashSet<(int,int)>();
            foreach(var area in live.Visual.Cells.Where(c=>
                c.MergeRows>1 || c.MergeColumns>1))
                for(int mr=area.Row;mr<area.Row+area.MergeRows;mr++)
                    for(int mc=area.Column;mc<area.Column+area.MergeColumns;mc++)
                        if(mr!=area.Row || mc!=area.Column)
                            mergeChildren.Add((mr,mc));
            for(int i=0;i<source.Length;i++)
            {
                var a=actual[i];
                var s=source[i];
                if(!mergeChildren.Contains((s.Row,s.Column)))
                {
                    var directDifferences=
                        DeskSpaceExcelDirectReadbackV139.Differences(s,a);
                    if(directDifferences.Length>0)
                        throw new InvalidDataException(
                            "Formatting readback differs at "+
                            Text(live.Range.Cells[s.Row+1,s.Column+1].Address)+
                            ": "+string.Join("; ",directDifferences)+
                            ". Excel's automatic/theme colours are compared "+
                            "by their actual colour mode, not cross-workbook "+
                            "computed RGB. Existing changes will be rolled back.");
                }
                // Excel TableStyle and conditional effects are DISPLAY layers,
                // not necessarily equal to the cell's direct Font/Interior.
                // Comparing only direct properties could report success even
                // when a restored real table looks completely different.
                if(!mergeChildren.Contains((s.Row,s.Column)) &&
                   DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(
                       s.Row,s.Column,sourceTablesV135) &&
                   s.DisplayStyleAvailable &&
                   (!a.DisplayStyleAvailable ||
                    s.DisplayFillBgr!=a.DisplayFillBgr ||
                    s.DisplayFontBgr!=a.DisplayFontBgr ||
                    s.DisplayBold!=a.DisplayBold ||
                    s.DisplayItalic!=a.DisplayItalic))
                    throw new InvalidDataException(
                        "Rendered non-table cell appearance differs at "+
                        Text(live.Range.Cells[s.Row+1,s.Column+1].Address)+
                        "; source and destination colours/fonts not identical.");
                dynamic cell=live.Range.Cells[s.Row+1,s.Column+1];
                var signature=signatures[i];
                if(originallyEmpty)
                {
                    string header=DeskSpaceExcelBlankFormatPolicyV136.
                        AllowedStructuralHeader(s.Row,s.Column,
                            live.Visual.FeaturesV106!.Tables);
                    // Formula cells must contain the EXACT captured relative
                    // R1C1 expression, not Excel's displayed calculated Value2.
                    // A formula may legitimately calculate 0; we never insert
                    // literal numeric zero into any non-formula blank cell.
                    var recordedV266=live.FormulaPlanV266.FirstOrDefault(x=>
                        x.Row==s.Row && x.Column==s.Column);
                    if(recordedV266!=null)
                    {
                        if(!DeskSpaceExcelFormulaPlanV266.Matches(recordedV266,
                            IsFormula(cell),Text((object?)cell.FormulaR1C1)))
                            throw new InvalidDataException(
                                "Captured Excel formula readback failed at "+
                                Text(cell.Address));
                    }
                    else if(IsFormula(cell) ||
                            header!=Text((object?)cell.Value2))
                        throw new InvalidDataException(
                            "An unexpected non-formula input or auto-filled "+
                            "formula appeared at "+Text(cell.Address)+
                            ". No input values may be pasted.");
                }
                else if(restoreFormulasV143 && s.StoredType=="Formula" &&
                        signature.Formula.Length==0 && signature.Value.Length==0)
                {
                    if(!IsFormula(cell) || Text(cell.FormulaR1C1)!=s.FormulaR1C1)
                        throw new InvalidDataException(
                            "Inserted formula readback did not exactly match "+
                            "saved R1C1 expression at "+Text(cell.Address));
                }
                else if(signature.Formula!=(IsFormula(cell)?Text(cell.FormulaR1C1):"") ||
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
                if(mergeChildren.Contains((wanted.Row,wanted.Column)))
                    continue;
                if(wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.Superscript!=observed.Superscript ||
                   wanted.Subscript!=observed.Subscript ||
                   wanted.TableRole!=observed.TableRole)
                    throw new InvalidDataException(
                        "Excel underline, text style or table role mismatch at "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                if(!originalProtectionV141.TryGetValue(
                    (wanted.Row,wanted.Column),out var priorFlags))
                    throw new InvalidDataException(
                        "No original protection baseline for cell "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                // A refused COM Locked/FormulaHidden write is an unsupported
                // protection feature, NOT a reason to undo correct visual
                // formatting. It is only allowed when the original security
                // flag is demonstrably unchanged. Unexpected security changes
                // are still a fatal error and trigger the workbook rollback.
                if(wanted.Locked!=observed.Locked)
                {
                    if(priorFlags.Locked!=observed.Locked)
                        throw new InvalidDataException(
                            "Existing Locked protection changed unexpectedly at "+
                            Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                    unavailableProtectionV141++;
                }
                if(wanted.FormulaHidden!=observed.FormulaHidden)
                {
                    if(priorFlags.FormulaHidden!=observed.FormulaHidden)
                        throw new InvalidDataException(
                            "Existing FormulaHidden protection changed unexpectedly at "+
                            Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                    unavailableProtectionV141++;
                }
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
                    x.ShowAutoFilter==expectedTable.ShowAutoFilter &&
                    HeadersIndependentV123(x.ColumnNames,expectedTable.ColumnNames)))
                    throw new InvalidDataException("A real Excel Table structure/style was not reproduced.");
            }
            foreach(var anchor in live.Visual.Cells.Where(c=>
                c.MergeRows>1 || c.MergeColumns>1))
            {
                var observed=actual.FirstOrDefault(c=>c.Row==anchor.Row &&
                    c.Column==anchor.Column);
                if(observed==null || observed.MergeRows!=anchor.MergeRows ||
                   observed.MergeColumns!=anchor.MergeColumns)
                    throw new InvalidDataException(
                        "A source merged-cell area could not be reproduced.");
            }
            for(int row=0;row<live.Visual.Rows;row++)
                if(Math.Abs(live.Visual.NativeRowHeights[row]-after.NativeRowHeights[row])>0.2)
                    throw new InvalidDataException("Excel row height readback mismatch.");
            for(int col=0;col<live.Visual.Columns;col++)
                if(Math.Abs(live.Visual.NativeColumnWidths[col]-after.NativeColumnWidths[col])>0.3)
                    throw new InvalidDataException("Excel native column width readback mismatch.");
            // R147: combined paste. Native R1C1 formula writes use the exact
            // saved relative/absolute expressions, independent of old source
            // data. All destination cells were inspected before formatting.
            var wantedV147=source.Where(c=>c.StoredType=="Formula")
                .Select(c=>new DeskSpaceExcelCombinedFormulaV147.Cell(
                    c.Row,c.Column,c.FormulaA1,c.FormulaR1C1)).ToArray();
            int writtenV147=0,matchingV147=0;
            if(wantedV147.Length>0)
            {
                var currentV147=new List<DeskSpaceExcelCombinedFormulaV147.Target>();
                foreach(var itemV147 in wantedV147)
                {
                    dynamic formulaCell=live.Range.Cells[
                        itemV147.Row+1,itemV147.Column+1];
                    bool exists=IsFormula(formulaCell);
                    currentV147.Add(new(itemV147.Row,itemV147.Column,
                        exists,!exists && (object?)formulaCell.Value2!=null,
                        exists?Text(formulaCell.FormulaR1C1):""));
                }
                var planV147=DeskSpaceExcelCombinedFormulaV147.Inspect(
                    wantedV147,currentV147);
                // Preserve Excel's calculated-column setting, previously
                // disabled by R145 until this transaction's finally block.
                foreach(var put in planV147.Writes)
                {
                    dynamic cell=live.Range.Cells[put.Row+1,put.Column+1];
                    if(IsFormula(cell) || (object?)cell.Value2!=null)
                        throw new InvalidDataException(
                            "Formula destination changed before native Excel write.");
                    cell.FormulaR1C1=put.FormulaR1C1;
                    writtenV147++;
                }
                matchingV147=planV147.ExistingMatches;
                foreach(var wantedFormulaV147 in wantedV147)
                {
                    dynamic cell=live.Range.Cells[
                        wantedFormulaV147.Row+1,wantedFormulaV147.Column+1];
                    if(!IsFormula(cell) ||
                       !string.Equals(Text(cell.FormulaR1C1),
                           wantedFormulaV147.FormulaR1C1,
                           StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "Combined formula readback differs from saved formula at "+
                            Text(cell.Address));
                }
            }
            // Do not save the open workbook behind the user's back.
            return "Verified "+source.Length+" cells, "+requestedFeatures.Tables.Count+
                " real Excel tables and exact underline styles. "+
                (originallyEmpty?"Blank input cells stayed blank. "+
                    live.FormulaPlanV266.Count+
                    " saved formulas restored and read back. ":
                    "Existing values/formulas preserved. ")+
                (unavailableProtectionV141>0
                    ?"IMPORTANT: "+unavailableProtectionV141+
                     " Locked/FormulaHidden settings could not be copied; "+
                     "the destination's original protection settings were "+
                     "left unchanged. All other supported formats verified. "
                    :"Cell-protection readback verified. ")+
                "Safety copy: "+backup;
        }
        catch(Exception failure)
        {
            bool restored=false;
            string restoreError="";
            try
            {
                RestoreAfterFailure(live,before,
                    live.Review.Gate.State==
                        DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty);
                restored=true;
            }
            catch(Exception rollbackError)
            {
                restoreError=rollbackError.Message;
            }
            throw new InvalidOperationException(
                "Excel formatting failed verification: "+failure.Message+
                ". In-memory rollback "+(restored?"attempted":"FAILED: "+restoreError)+
                ". Verified pre-change workbook backup: "+backup+
                ". Do not save the affected workbook until reviewed.",failure);
        }
        finally
        {
            if(guardedAutoFillV145)
            {
                try{
                    live.Excel.AutoCorrect.AutoFillFormulasInLists=priorAutoFillV145;
                }
                catch(System.Runtime.InteropServices.COMException)
                {
                    // Never claim this setting was restored if Excel refuses.
                    // The safety backup and readback remain authoritative.
                }
            }
        }
    }
}
