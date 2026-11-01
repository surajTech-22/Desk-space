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
    public string PreviousRaw {get;set;}="";
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
    public string WorkbookFullName {get;set;}="";
    public string Reason {get;set;}="";
    public bool Verified {get;set;}
    public bool UsedReferenceSignatureV120 {get;set;}
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
    [DllImport("ole32.dll",PreserveSig=true)]
    private static extern int GetRunningObjectTable(int reserved,
        out System.Runtime.InteropServices.ComTypes.IRunningObjectTable table);
    [DllImport("ole32.dll",PreserveSig=true)]
    private static extern int CreateBindCtx(int reserved,
        out System.Runtime.InteropServices.ComTypes.IBindCtx context);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowEnumerator callback,IntPtr arg);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd,
        System.Text.StringBuilder output,int length);
    private delegate bool WindowEnumerator(IntPtr hwnd,IntPtr arg);

    private static dynamic Excel()
    {
        var apps=new Dictionary<long,object>();
        // ROT ProgID is a fallback; multiple Excel instances can run
        // simultaneously and it can resolve an unrelated workbook.
        try
        {
            CLSIDFromProgID("Excel.Application",out var classId);
            GetActiveObject(ref classId,IntPtr.Zero,out var first);
            RegisterExcelApp(first,apps);
        }
        catch(COMException){}
        catch(InvalidOperationException){}
        try
        {
            if(GetRunningObjectTable(0,out var rot)==0 &&
                CreateBindCtx(0,out var ctx)==0)
            {
                rot.EnumRunning(out var enumeration);
                var item=new System.Runtime.InteropServices.ComTypes.IMoniker[1];
                int limit=0;
                while(limit++<300 && enumeration.Next(1,item,IntPtr.Zero)==0)
                {
                    try
                    {
                        item[0].GetDisplayName(ctx,null,out string name);
                        if(!System.Text.RegularExpressions.Regex.IsMatch(name,
                            @"\.(xlsx|xlsm|xlsb|xls)(!.*)?$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                        rot.GetObject(item[0],out object running);
                        dynamic workbook=running;
                        RegisterExcelApp((object)workbook.Application,apps);
                    }
                    catch(COMException){}
                    catch(InvalidCastException){}
                    catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
                }
            }
        }
        catch(COMException){}
        catch(InvalidOperationException){}
        if(apps.Count==0)
            throw new InvalidOperationException(
                "Could not connect to the already opened Excel workbook. "+
                "Open the workbook in Microsoft Excel, select its cells and "+
                "return to DeskSpace. Excel and DeskSpace must run at the "+
                "same Windows privilege level.");

        // EnumWindows enumerates visible top-level windows in Z order.
        // After clicking DeskSpace, the most recently used Excel window is
        // normally the topmost XLMAIN window behind DeskSpace.
        var ranks=new Dictionary<long,int>();
        EnumWindows((hwnd,_)=>{
            var text=new System.Text.StringBuilder(90);
            if(GetClassName(hwnd,text,text.Capacity)>0 &&
                text.ToString()=="XLMAIN")ranks[hwnd.ToInt64()]=ranks.Count;
            return true;
        },IntPtr.Zero);
        foreach(var candidate in apps.OrderBy(x=>ranks.TryGetValue(x.Key,
            out var rank)?rank:int.MaxValue))
        {
            try
            {
                dynamic app=candidate.Value;
                if(app.ActiveWorkbook==null || app.ActiveWindow==null)continue;
                dynamic selection=app.ActiveWindow.RangeSelection;
                if((int)selection.Row<1 || (int)selection.Column<1)continue;
                return app;
            }
            catch(COMException){}
            catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
        }
        throw new InvalidOperationException(
            "Excel is running, but no selectable worksheet range could be "+
            "identified. Click the target worksheet and highlight its cells.");
    }
    private static void RegisterExcelApp(object appObject,
        Dictionary<long,object> apps)
    {
        try
        {
            dynamic app=appObject;
            long hwnd=Convert.ToInt64((object)app.Hwnd,
                CultureInfo.InvariantCulture);
            if(hwnd!=0 && !apps.ContainsKey(hwnd))apps.Add(hwnd,appObject);
        }
        catch(COMException){}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
    }    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    // Reuse the existing multi-instance aware COM resolver on the
    // worker STA; do not launch Excel or change its selected workbook.
    internal static dynamic ExcelForNormalizationV102()=>Excel();

    private static string Text(object? o)=>
        Convert.ToString(o,CultureInfo.InvariantCulture)??"";
    private static string Normalize(string text)=>string.Concat(
        text.Trim().ToUpperInvariant().Where(c=>char.IsLetterOrDigit(c)));
    private static bool Formula(dynamic cell)
    {
        try{return (bool)cell.HasFormula;}
        catch{return false;}
    }
    public static bool ExcelCopyActive()
    {
        try{
            dynamic app=Excel();
            var state=(object?)app.CutCopyMode;
            return state!=null && Convert.ToInt32(state,
                CultureInfo.InvariantCulture)==1;
        }
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
        dynamic range;
        if(string.IsNullOrWhiteSpace(rangeAddress))
            range=app.ActiveWindow.RangeSelection;
        else
            range=app.ActiveSheet.Range[rangeAddress];
        dynamic sheet=range.Worksheet;
        int row=(int)range.Row,column=(int)range.Column;
        int rows=(int)range.Rows.Count,cols=(int)range.Columns.Count;
        if(rows<=0||cols<=0||rows*(long)cols>1800)
            throw new InvalidOperationException("Select 1–1800 cells, not an entire worksheet.");
        var entry=new DeskSpaceExcelTemplateV16 {
            Name=title.Trim(),
            SourceWorkbook=Path.GetFileName(Text(sheet.Parent.Name)),
            SourceSheet=Text(sheet.Name),
            SourceAddress=Text(range.Address),
            SourceRow=row,SourceColumn=column,
            SourceKind="Live Excel",CoordinatesVerified=true,
            RowCount=rows,ColumnCount=cols
        };
        for(int r=1;r<=rows;r++)
            for(int c=1;c<=cols;c++)
            {
                dynamic cell=range.Cells[r,c];
                object? raw;
                try {raw=(object?)cell.Value2;}
                catch(Exception excelError) when(
                    excelError is COMException or ArgumentException or
                        Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){
                    throw new InvalidOperationException(
                        "Excel selection capture failed reading the value at "+
                        "row "+(row+r-1)+", column "+(column+c-1)+
                        " ("+excelError.GetType().Name+", 0x"+
                        excelError.HResult.ToString("X8")+"). "+
                        excelError.Message+". No workbook cells were edited.",
                        excelError);
                }
                bool hasFormula=Formula(cell);
                // Keep blank cells in selected table geometry. They may be
                // destinations for values entered later.
                var record=new DeskSpaceExcelCellV16 {
                    RowOffset=r-1,ColumnOffset=c-1,
                    RawValue=Text(raw),
                    Display=Text(cell.Text),
                    NumberFormat=Text(cell.NumberFormat),
                    CellAddress=Text(cell.Address),
                    AddressVerified=true
                };
                if(hasFormula)
                {
                    record.ValueKind="formula";
                    record.FormulaR1C1=Text(cell.FormulaR1C1);
                }
                else if(raw is bool)record.ValueKind="boolean";
                else if(raw is double || raw is float || raw is decimal || raw is int)
                    record.ValueKind="number";
                else record.ValueKind=raw==null?"blank":"text";
                bool bold=false,merged=false;
                try{bold=Convert.ToBoolean((object)cell.Font.Bold);}catch{}
                try{merged=Convert.ToBoolean((object)cell.MergeCells);}catch{}
                record.IsMerged=merged;
                if(merged)try{record.MergedArea=Text(cell.MergeArea.Address);}catch{}
                record.Role=hasFormula?"Formula":
                    (raw is string && (bold || r==1))?"Reference":"Value";
                record.FixedOrVariable=hasFormula?"Calculated":
                    record.Role=="Reference"?"Fixed label":"Variable input (review)";
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
        // Capture physical Excel styles and formula metadata from the
        // actual range. The visual deliberately stores NO old input values.
        entry.SheetVisualV101=DeskSpaceExcelVisualCaptureV101.Capture(range);
        // A formatting template stores the SELECTED PHYSICAL CELLS, their
        // geometry, dimensions and direct styles, even if their values are
        // all blank. The previous heuristic rejected legitimate empty
        // formats (alignment, underline, row height, or default styles).
        // Never require source values or artificial placeholder records.
        if(entry.SheetVisualV101.Cells.Count!=rows*cols ||
           entry.SheetVisualV101.Rows!=rows ||
           entry.SheetVisualV101.Columns!=cols)
            throw new InvalidDataException(
                "Incomplete formatting-only Excel capture; nothing saved.");
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
    // Header/value maps have a label signature of their own. In-range
    // user-approved Reference cells take precedence over unrelated neighboring
    // labels captured for visual context. Do not apply the formatting template
    // occupancy/type gate here; this is the Values-only mapping pathway.
    private static DeskSpaceExcelAnchorV16[] SignatureAnchorsV120(
        DeskSpaceExcelTemplateV16 entry)
    {
        var references=entry.Cells.Where(c=>
                c.Role=="Reference" && Normalize(c.RawValue).Length>2)
            .Select(c=>new DeskSpaceExcelAnchorV16{
                RowOffset=c.RowOffset,ColumnOffset=c.ColumnOffset,
                Label=c.RawValue
            }).ToArray();
        var surrounding=entry.Anchors.Where(a=>
            Normalize(a.Label).Length>2 &&
            !references.Any(c=>c.RowOffset==a.RowOffset &&
                               c.ColumnOffset==a.ColumnOffset)).ToArray();
        // Two explicit headers already form a deterministic signature.
        // Old mappings with no classified references still work using
        // the neighboring labels originally recorded with the selection.
        var anchors=references.Length>=2 ? references :
            references.Concat(surrounding).ToArray();
        return anchors.GroupBy(a=>
                (a.RowOffset,a.ColumnOffset,Key:Normalize(a.Label)))
            .Select(x=>x.First()).ToArray();
    }
    internal static (int Row,int Col,int Matches,bool Unique)
        MatchHeaderSignatureV120(
            DeskSpaceExcelTemplateV16 entry,
            IEnumerable<(int Row,int Col,string Label)> labels)
    {
        var anchors=SignatureAnchorsV120(entry);
        if(anchors.Length==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var indexed=anchors.GroupBy(a=>Normalize(a.Label))
            .ToDictionary(x=>x.Key,x=>x.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var votes=new Dictionary<(int,int),HashSet<string>>();
        foreach(var (row,col,label) in labels)
        {
            string key=Normalize(label);
            if(!indexed.TryGetValue(key,out var matching))continue;
            foreach(var anchor in matching)
            {
                var origin=(row-anchor.RowOffset,col-anchor.ColumnOffset);
                if(origin.Item1<1||origin.Item2<1)continue;
                if(!votes.TryGetValue(origin,out var found))
                    votes[origin]=found=new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                found.Add(key);
            }
        }
        if(votes.Count==0)return (entry.SourceRow,entry.SourceColumn,0,false);
        var ranked=votes.OrderByDescending(x=>x.Value.Count)
            .ThenBy(x=>Math.Abs(x.Key.Item1-entry.SourceRow)+
                        Math.Abs(x.Key.Item2-entry.SourceColumn)).ToArray();
        var first=ranked[0];
        bool unique=ranked.Length==1 ||
                    first.Value.Count>ranked[1].Value.Count;
        int required=anchors.Select(a=>Normalize(a.Label))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        bool strong=first.Value.Count>=Math.Min(3,Math.Max(2,required)) &&
                    first.Value.Count*2>=required;
        return (first.Key.Item1,first.Key.Item2,first.Value.Count,
            unique&&strong);
    }
    private static (int Row,int Col,int Matches,bool Unique) FindAnchorMatch(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry)=>
        MatchHeaderSignatureV120(entry,SheetLabels((object)sheet));

    // This is intentionally independent from R107's formatting eligibility
    // check. The data mapping is allowed to fill selected EMPTY value cells
    // in a partly populated form while protecting old values and formulas.
    internal static string HeaderValueDecisionV120(bool targetVerified,
        string sourceRole,string sourceKind,bool destinationFormula,
        bool destinationOccupied,bool overwrite,bool sheetProtected)
    {
        if(!targetVerified)return "Unconfirmed target";
        if(sourceRole!="Value" || sourceKind=="formula" ||
           sourceKind=="blank" || sourceKind=="")
            return "Reference or formula (analysis only)";
        if(destinationFormula)return "Existing formula protected";
        if(destinationOccupied&&!overwrite)return "Existing value protected";
        if(sheetProtected)return "Sheet protected";
        return "Ready";
    }
    // The old automatic-matching path demanded two source labels even when
    // the entire destination was empty. A blank worksheet can never contain
    // those labels. Inspect the whole saved rectangle BEFORE approving the
    // selected cell as a new destination anchor.
    private static DeskSpaceExcelFullRangeGateV107.Decision CheckAllCellsV116(
        dynamic sheet,DeskSpaceExcelTemplateV16 entry,int startRow,int startColumn)
    {
        int rows=entry.RowCount,cols=entry.ColumnCount;
        if(rows<1||cols<1||rows*(long)cols>1800 ||
           startRow<1||startColumn<1||
           startRow+rows-1>1048576||startColumn+cols-1>16384)
            throw new InvalidOperationException(
                "The target selection does not fit the complete recorded Excel range.");
        var sourceByCell=entry.Cells
            .GroupBy(x=>(x.RowOffset,x.ColumnOffset))
            .ToDictionary(x=>x.Key,x=>x.First());
        var expected=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        var actual=new List<DeskSpaceExcelFullRangeGateV107.Cell>();
        for(int rr=0;rr<rows;rr++)
        for(int cc=0;cc<cols;cc++)
        {
            sourceByCell.TryGetValue((rr,cc),out var saved);
            bool occupied=saved!=null;
            string sourceType="Blank";
            if(saved!=null)
                sourceType=saved.ValueKind switch
                {
                    "formula" or "captured-formula"=>"Formula",
                    "number"=>"Number","boolean"=>"Boolean",
                    "text"=>"Text",_=>saved.FormulaR1C1.Length>0
                        ?"Formula":"Text"
                };
            expected.Add(new(occupied,sourceType));
            dynamic cell=sheet.Cells[startRow+rr,startColumn+cc];
            bool formula=Formula(cell);
            object? raw=(object?)cell.Value2;
            actual.Add(new(formula||raw!=null,
                DeskSpaceExcelInputTypeV101.Stored(raw,formula)));
        }
        return DeskSpaceExcelFullRangeGateV107.Evaluate(expected,actual);
    }
    private static (int Row,int Column)? SelectedOriginV116(
        dynamic excel,DeskSpaceExcelTemplateV16 entry)
    {
        dynamic selection=excel.ActiveWindow.RangeSelection;
        if((int)selection.Areas.Count!=1)return null;
        int countRows=(int)selection.Rows.Count;
        int countCols=(int)selection.Columns.Count;
        if(!((countRows==1&&countCols==1)||
             (countRows==entry.RowCount&&countCols==entry.ColumnCount)))
            return null;
        return ((int)selection.Row,(int)selection.Column);
    }
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        // R125: information/data is always header-signature matched. The
        // independent format/formula writer uses selected Excel destinations.
        if(useSelection)
            throw new InvalidOperationException(
                "Information paste requires verified matching headers. "+
                "Selected-cell fallback is disabled for data.");
        if(!entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Screenshot text has no verified Excel cell addresses. "+
                "Link the screenshot to a live Excel selection before pasting.");
        if(!entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Screenshot-only cell positions are not confirmed. "+
                "Link this format to the exact live Excel selection first.");
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
            var match=FindAnchorMatch((object)sheet,entry);
            r=match.Row;c=match.Col;
            verified=match.Unique;
            reason=verified
                ? "Matched "+match.Matches+" verified reference labels at "+
                  "their saved relative positions. Only mapped values can be pasted."
                : "No unique reference-label signature matched this worksheet. "+
                  "For a sheet without the saved headers, choose "+
                  "'Paste starting at the selected Excel cell' in More options, "+
                  "then preview again. Nothing has been changed.";
        }
        // IMPORTANT: R107 full-range occupancy is ONLY for formatting.
        // Header/value mappings may target partially populated workbooks.
        // Per-cell protection below decides what can actually be written.
        var preview=new DeskSpaceExcelProposalV16 {
            StartRow=r,StartColumn=c,SheetName=Text(sheet.Name),
            WorkbookFullName=Text(app.ActiveWorkbook.FullName),
            Verified=verified,Reason=reason,
            UsedReferenceSignatureV120=!useSelection
        };
        foreach(var source in entry.Cells)
        {
            int destRow=r+source.RowOffset,destCol=c+source.ColumnOffset;
            dynamic cell=sheet.Cells[destRow,destCol];
            var old=(object?)cell.Value2;
            var oldText=Text(cell.Text);
            bool destinationFormula=Formula(cell);
            string decision=HeaderValueDecisionV120(
                verified,source.Role,source.ValueKind,destinationFormula,
                old!=null,overwrite,(bool)sheet.ProtectContents);
            if(decision=="Ready" && (bool)cell.MergeCells)
            {
                dynamic primary=cell.MergeArea.Cells[1,1];
                if((int)primary.Row!=destRow||(int)primary.Column!=destCol)
                    decision="Merged cell: not top-left";
            }
            var address=Text(cell.Address);
            preview.Changes.Add(new DeskSpaceExcelChangeV16 {
                Row=destRow,Column=destCol,CellAddress=address,
                Previous=oldText,PreviousRaw=Text(old),NewValue=DeskSpaceExcelActionsV46.PreviewText(source,DateTime.Now),
                Decision=decision,Source=source
            });
        }
        return preview;
    }
    private static object? TypedValue(DeskSpaceExcelCellV16 cell)
    {
        if(DeskSpaceExcelActionsV46.IsNativeShortcut(cell.ValueKind))
            throw new InvalidOperationException(
                "Excel shortcuts must execute in Excel, never be converted to values.");
        if(cell.ValueKind=="boolean")
            return string.Equals(cell.RawValue,"True",StringComparison.OrdinalIgnoreCase);
        if(cell.ValueKind=="number")
        {
            if(!double.TryParse(cell.RawValue,NumberStyles.Any,
                CultureInfo.InvariantCulture,out var value))
                throw new InvalidDataException("Invalid saved Excel number.");
            return value;
        }
        // Force a saved literal that resembles an Excel formula to stay text.
        // Formulas are written separately only when expressly captured as formulas.
        return cell.RawValue.StartsWith("=",StringComparison.Ordinal)
            ? "'"+cell.RawValue : cell.RawValue;
    }
    public static string Apply(DeskSpaceExcelProposalV16 preview,
        DeskSpaceExcelTemplateV16 entry,bool overwrite)
    {
        if(!preview.Verified || !entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Target cells are not verified. Link a live Excel range and preview again.");
        if(!preview.Changes.Any(c=>c.CanApply &&
               c.Source.Role=="Value" &&
               c.Source.ValueKind!="formula" &&
               c.Source.ValueKind!="blank"))
            throw new InvalidOperationException(
                "No verified input cells are safe to paste. Nothing was changed.");
        // Re-preview immediately before writing to prevent a stale preview
        // being applied after switching workbooks or sheets.
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        if(!string.Equals(Text(sheet.Name),preview.SheetName,StringComparison.Ordinal) ||
           !string.Equals(Text(app.ActiveWorkbook.FullName),preview.WorkbookFullName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active Excel workbook or sheet changed. Preview again.");
        if((bool)sheet.ProtectContents)
            throw new InvalidOperationException("The destination worksheet is protected.");
        if(preview.UsedReferenceSignatureV120)
        {
            var matched=FindAnchorMatch((object)sheet,entry);
            if(!matched.Unique || matched.Row!=preview.StartRow ||
               matched.Col!=preview.StartColumn)
                throw new InvalidOperationException(
                    "Header/reference signature changed since preview. "+
                    "No Excel cells changed. Preview the value mapping again.");
        }

        // Detect modifications made since the preview before touching the workbook.
        foreach(var change in preview.Changes.Where(x=>x.CanApply))
        {
            dynamic destination=sheet.Cells[change.Row,change.Column];
            if(Formula(destination) ||
               !string.Equals(Text((object?)destination.Value2),change.PreviousRaw,StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Destination "+change.CellAddress+" changed since preview. Preview again.");
        }
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
            foreach(var change in preview.Changes.Where(x=>x.CanApply &&
                x.Source.Role=="Value" &&
                x.Source.ValueKind!="formula" &&
                x.Source.ValueKind!="blank"))
            {
                dynamic cell=sheet.Cells[change.Row,change.Column];
                var previous=(object?)cell.Value2;
                var hadFormula=Formula(cell);
                if(hadFormula)continue;
                if(previous!=null&&!overwrite)continue;
                rollback.Add((cell,previous,hadFormula,hadFormula?Text(cell.FormulaR1C1):""));
                // Paste Special Values semantics only. Never replay formulas.
                if(DeskSpaceExcelActionsV46.IsFormulaAction(change.Source.ValueKind))
                {
                    // The formula itself goes to Excel, not its calculated
                    // result. Formula accepts native A1 references.
                    if(change.Source.ValueKind==DeskSpaceExcelActionsV46.CapturedFormula)
                        cell.FormulaR1C1=DeskSpaceExcelActionsV46.Formula(change.Source);
                    else cell.Formula=DeskSpaceExcelActionsV46.Formula(change.Source);
                }
                else if(DeskSpaceExcelActionsV46.IsNativeShortcut(change.Source.ValueKind))
                {
                    // Excel executes its OWN keyboard command. Verify that
                    // Windows foreground is Excel, never send to another app.
                    dynamic excelWindow=app.ActiveWindow;
                    excelWindow.Activate();
                    IntPtr excelHwnd=new IntPtr((int)app.Hwnd);
                    SetForegroundWindow(excelHwnd);
                    if(GetForegroundWindow()!=excelHwnd)
                        throw new InvalidOperationException(
                            "Activate Excel on this PC before using keyboard "+
                            "mappings. No keys were sent elsewhere.");
                    cell.Select();
                    if(!string.Equals(Text(app.ActiveCell.Address),
                          change.CellAddress,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Excel active cell differs from verified target.");
                    app.SendKeys(DeskSpaceExcelActionsV46.NativeSendKeys(
                        change.Source.ValueKind),true);
                }
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
