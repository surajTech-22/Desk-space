using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskSpace;

// A value-free, read-only snapshot of Excel's actual selected-cell styling.
// Numeric/text input data is intentionally NEVER copied into this model.
internal sealed class DeskSpaceExcelSheetVisualV101
{
    public int Rows {get;set;}
    public int Columns {get;set;}
    public List<double> RowHeights {get;set;}=new();
    public List<double> ColumnWidths {get;set;}=new();
    public List<double> NativeRowHeights {get;set;}=new();
    public List<double> NativeColumnWidths {get;set;}=new();
    public List<DeskSpaceExcelStyledCellV101> Cells {get;set;}=new();
    public DeskSpaceExcelFeatureReportV106? FeaturesV106 {get;set;}
}
internal sealed class DeskSpaceExcelStyledCellV101
{
    public int Row {get;set;}
    public int Column {get;set;}
    public int MergeRows {get;set;}=1;
    public int MergeColumns {get;set;}=1;
    public string FormulaA1 {get;set;}="";
    public string FormulaR1C1 {get;set;}="";
    public string NumberFormat {get;set;}="";
    public string StoredType {get;set;}="Unknown";
    public string ExpectedType {get;set;}="Unknown";
    public string FontName {get;set;}="Calibri";
    public double FontSize {get;set;}=11;
    public bool Bold {get;set;}
    public bool Italic {get;set;}
    public bool Underline {get;set;}
    public bool StrikeThrough {get;set;}
    public int IndentLevel {get;set;}
    public int TextOrientation {get;set;}
    public bool ShrinkToFit {get;set;}
    public bool WrapText {get;set;}
    public int HorizontalAlignment {get;set;}
    public int VerticalAlignment {get;set;}
    public int FillBgr {get;set;}=0xFFFFFF;
    public int FontBgr {get;set;}=0x000000;
    // Direct pattern/color metadata MUST be separate from the style visible
    // through a ListObject. None=-4142, Solid=1. Other patterns are blocked
    // by the format application preflight until fully implemented.
    public int DirectFillPattern {get;set;}=int.MinValue;
    public int DirectFontColorIndex {get;set;}=int.MinValue;
    public int DirectFontThemeColor {get;set;}
    public int DirectFillThemeColor {get;set;}
    // Excel TableStyle and conditional formatting may affect the displayed
    // appearance without changing Range.Interior/Font direct properties.
    // Keep both layers; never overwrite the source's direct per-cell style.
    public bool DisplayStyleAvailable {get;set;}
    public int DisplayFillBgr {get;set;}=0xFFFFFF;
    public int DisplayFontBgr {get;set;}=0;
    public bool DisplayBold {get;set;}
    public bool DisplayItalic {get;set;}
    public bool BorderTop {get;set;}
    public bool BorderBottom {get;set;}
    public bool BorderLeft {get;set;}
    public bool BorderRight {get;set;}
    public int BorderLeftColor {get;set;}
    public int BorderTopColor {get;set;}
    public int BorderRightColor {get;set;}
    public int BorderBottomColor {get;set;}
    public int BorderLeftWeight {get;set;}
    public int BorderTopWeight {get;set;}
    public int BorderRightWeight {get;set;}
    public int BorderBottomWeight {get;set;}
    public bool ConditionalAppearance {get;set;}
    public string Address {get;set;}="";
    internal DeskSpaceExcelStyledCellV101 OffsetCopy(int row,int column)
    {
        var copy=(DeskSpaceExcelStyledCellV101)MemberwiseClone();
        copy.Row+=row;copy.Column+=column;
        return copy;
    }
}

// Excel COM lives on the dedicated STA capture worker. Never run on WPF UI.
internal static class DeskSpaceExcelVisualCaptureV101
{
    private static string Text(object? item)=>
        Convert.ToString(item,CultureInfo.InvariantCulture)??"";
    private static bool Flag(Func<object?> read)
    {
        try {return Convert.ToBoolean(read(),CultureInfo.InvariantCulture);}
        catch(COMException){return false;}
        catch(InvalidCastException){return false;}
        catch(FormatException){return false;}
        catch(ArgumentException){return false;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return false;}
    }
    private static int Number(Func<object?> read,int fallback=0)
    {
        try {return Convert.ToInt32(read(),CultureInfo.InvariantCulture);}
        catch(COMException){return fallback;}
        catch(InvalidCastException){return fallback;}
        catch(FormatException){return fallback;}
        catch(OverflowException){return fallback;}
        catch(ArgumentException){return fallback;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return fallback;}
    }
    private static double Measure(Func<object?> read,double fallback)
    {
        try{return Convert.ToDouble(read(),CultureInfo.InvariantCulture);}
        catch(COMException){return fallback;}
        catch(InvalidCastException){return fallback;}
        catch(FormatException){return fallback;}
        catch(OverflowException){return fallback;}
        catch(ArgumentException){return fallback;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return fallback;}
    }
    private static double RequiredMeasure(Func<object?> read,string label)
    {
        try
        {
            var value=Convert.ToDouble(read(),CultureInfo.InvariantCulture);
            if(double.IsFinite(value) && value>0)return value;
        }
        catch(COMException){}
        catch(InvalidCastException){}
        catch(FormatException){}
        catch(ArgumentException){}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
        throw new InvalidDataException("Excel did not provide an exact "+label+
            ". Source formatting was NOT captured.");
    }
    private static string Format(Func<object?> read,string fallback="")
    {
        try{return Text(read());}
        catch(COMException){return fallback;}
        catch(InvalidCastException){return fallback;}
        catch(ArgumentException){return fallback;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return fallback;}
    }
    private static bool VisibleBorder(dynamic cell,int edge)
    {
        try
        {
            dynamic border=cell.Borders[edge];
            return Convert.ToInt32((object)border.LineStyle,
                CultureInfo.InvariantCulture)!=-4142; // xlLineStyleNone
        }
        catch(COMException){return false;}
        catch(InvalidCastException){return false;}
        catch(ArgumentException){return false;}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return false;}
    }
    private static int BorderColor(dynamic cell,int edge)=>
        Number(()=>(object?)cell.Borders[edge].Color,0x222222);
    private static int BorderWeight(dynamic cell,int edge)=>
        Number(()=>(object?)cell.Borders[edge].Weight,2);
    private static bool HasConditionalAppearance(dynamic cell)
    {
        try{return (int)cell.FormatConditions.Count>0;}
        catch(COMException){return false;}
        catch(ArgumentException){return false;}
    }
    internal static DeskSpaceExcelSheetVisualV101 Capture(dynamic range)
    {
        string phase="reading selected range dimensions";
        string currentCell="selection";
        try
        {
        int rows=(int)range.Rows.Count,columns=(int)range.Columns.Count;
        if(rows<1 || columns<1 || rows*(long)columns>1800)
            throw new InvalidOperationException(
                "Select 1–1800 Excel cells to inspect the sheet layout.");
        int startRow=(int)range.Row,startCol=(int)range.Column;
        var view=new DeskSpaceExcelSheetVisualV101{Rows=rows,Columns=columns};
        phase="inspecting genuine Excel Tables / ListObjects";
        view.FeaturesV106=DeskSpaceExcelSemanticV106.Start(
            range,startRow,startCol,rows,columns);
        phase="reading exact Excel row and column dimensions";
        for(int r=1;r<=rows;r++)
        {
            double height=RequiredMeasure(
                ()=> (object?)range.Rows[r].RowHeight,"row height");
            view.NativeRowHeights.Add(height);
            view.RowHeights.Add(Math.Clamp(height*96/72,18,220));
        }
        for(int c=1;c<=columns;c++)
        {
            view.NativeColumnWidths.Add(RequiredMeasure(
                ()=> (object?)range.Columns[c].ColumnWidth,"column width"));
            view.ColumnWidths.Add(Math.Clamp(
                Measure(()=> (object?)range.Columns[c].Width,75)*96/72,38,440));
        }
        // A merged region has several physical Excel cells, each with
        // independently capturable formatting. No cell is skipped in the model.
        var covered=new HashSet<(int,int)>();
        for(int r=1;r<=rows;r++)
        for(int c=1;c<=columns;c++)
        {
            phase="reading individual cell formats, formulas and table appearance";
            currentCell="row "+(startRow+r-1)+", column "+(startCol+c-1);
            dynamic cell=range.Cells[r,c];
            var style=new DeskSpaceExcelStyledCellV101{
                Row=r-1,Column=c-1,
                Address=Format(()=> (object?)cell.Address),
                NumberFormat=Format(()=> (object?)cell.NumberFormat,"General"),
                FontName=Format(()=> (object?)cell.Font.Name,"Calibri"),
                FontSize=Math.Clamp(Measure(()=> (object?)cell.Font.Size,11),7,60),
                Bold=Flag(()=> (object?)cell.Font.Bold),
                Italic=Flag(()=> (object?)cell.Font.Italic),
                Underline=Number(()=> (object?)cell.Font.Underline,-4142)!=-4142,
                StrikeThrough=Flag(()=> (object?)cell.Font.Strikethrough),
                IndentLevel=Math.Clamp(Number(()=> (object?)cell.IndentLevel),0,15),
                TextOrientation=Number(()=> (object?)cell.Orientation),
                ShrinkToFit=Flag(()=> (object?)cell.ShrinkToFit),
                WrapText=Flag(()=> (object?)cell.WrapText),
                HorizontalAlignment=Number(()=> (object?)cell.HorizontalAlignment),
                VerticalAlignment=Number(()=> (object?)cell.VerticalAlignment),
                FillBgr=Number(()=> (object?)cell.Interior.Color,0xFFFFFF),
                FontBgr=Number(()=> (object?)cell.Font.Color,0),
                DirectFillPattern=Number(()=> (object?)cell.Interior.Pattern,int.MinValue),
                DirectFontColorIndex=Number(()=> (object?)cell.Font.ColorIndex,int.MinValue),
                DirectFontThemeColor=Number(()=> (object?)cell.Font.ThemeColor,0),
                DirectFillThemeColor=Number(()=> (object?)cell.Interior.ThemeColor,0),
                BorderLeft=VisibleBorder(cell,7),   // xlEdgeLeft
                BorderTop=VisibleBorder(cell,8),    // xlEdgeTop
                BorderBottom=VisibleBorder(cell,9), // xlEdgeBottom
                BorderRight=VisibleBorder(cell,10), // xlEdgeRight
                BorderLeftColor=BorderColor(cell,7),
                BorderTopColor=BorderColor(cell,8),
                BorderBottomColor=BorderColor(cell,9),
                BorderRightColor=BorderColor(cell,10),
                BorderLeftWeight=BorderWeight(cell,7),
                BorderTopWeight=BorderWeight(cell,8),
                BorderBottomWeight=BorderWeight(cell,9),
                BorderRightWeight=BorderWeight(cell,10)
            };
            style.ConditionalAppearance=HasConditionalAppearance(cell);
            // Read DISPLAY formatting for every physical cell, not only
            // conditional-format rules. TableStyleMedium2 etc. can apply
            // colours/fonts without any direct Font/Interior assignment.
            try
            {
                dynamic shown=cell.DisplayFormat;
                style.DisplayFillBgr=Number(()=> (object?)shown.Interior.Color,
                    style.FillBgr);
                style.DisplayFontBgr=Number(()=> (object?)shown.Font.Color,
                    style.FontBgr);
                style.DisplayBold=Flag(()=> (object?)shown.Font.Bold);
                style.DisplayItalic=Flag(()=> (object?)shown.Font.Italic);
                style.DisplayStyleAvailable=true;
            }
            catch(COMException){style.DisplayStyleAvailable=false;}
            catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {style.DisplayStyleAvailable=false;}
            catch(ArgumentException)
            {style.DisplayStyleAvailable=false;}
            bool formula=Flag(()=> (object?)cell.HasFormula);
            if(formula)
            {
                style.FormulaA1=Format(()=> (object?)cell.Formula);
                style.FormulaR1C1=Format(()=> (object?)cell.FormulaR1C1);
            }
            object? raw;
            try{raw=(object?)cell.Value2;}
            catch(COMException){raw=null;}
            catch(ArgumentException){raw=null;}
            style.StoredType=DeskSpaceExcelInputTypeV101.Stored(raw,formula);
            style.ExpectedType=DeskSpaceExcelInputTypeV101.Expected(
                style.NumberFormat,style.StoredType,formula);
            DeskSpaceExcelSemanticV106.CaptureCell(view.FeaturesV106!,
                cell,r-1,c-1,raw,style.StoredType,style.ExpectedType,formula);
            if(Flag(()=> (object?)cell.MergeCells))
            {
                try
                {
                    dynamic area=cell.MergeArea;
                    int top=(int)area.Row-startRow+1,left=(int)area.Column-startCol+1;
                    int height=(int)area.Rows.Count,width=(int)area.Columns.Count;
                    if(top<1 || left<1 || top+height-1>rows ||
                       left+width-1>columns)
                        throw new InvalidOperationException(
                            "The selected range cuts through a merged Excel cell. "+
                            "Include the complete merged area before capturing.");
                    if(top==r && left==c && height<=rows-r+1 && width<=columns-c+1)
                    {
                        style.MergeRows=height;style.MergeColumns=width;
                        // Merged cells may define bottom/right edges on
                        // the final underlying cell, not on the anchor.
                        dynamic last=range.Cells[r+height-1,c+width-1];
                        style.BorderBottom=VisibleBorder(last,9) || style.BorderBottom;
                        style.BorderRight=VisibleBorder(last,10) || style.BorderRight;
                        if(VisibleBorder(last,9))
                            style.BorderBottomColor=BorderColor(last,9);
                        if(VisibleBorder(last,10))
                            style.BorderRightColor=BorderColor(last,10);
                        for(int rr=r;rr<r+height;rr++)
                            for(int cc=c;cc<c+width;cc++)
                                if(rr!=r || cc!=c)covered.Add((rr,cc));
                    }
                    // If a merge extends beyond the selected area, represent
                    // only the selected part, do not invent its other cells.
                }
                catch(COMException){}
                catch(ArgumentException){}
            }
            view.Cells.Add(style);
        }
        if(view.Cells.Count!=rows*columns ||
           view.Cells.Select(x=>(x.Row,x.Column)).Distinct().Count()!=rows*columns)
            throw new InvalidDataException(
                "Incomplete per-cell Excel formatting capture; no styles saved.");
        return view;
        }
        catch(Exception error) when(
            error is COMException or ArgumentException or InvalidOperationException
                or InvalidCastException or OverflowException or FormatException
                or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            throw new InvalidOperationException(
                "Excel capture failed while "+phase+" at "+currentCell+
                " ("+error.GetType().Name+", 0x"+
                error.HResult.ToString("X8")+"). "+error.Message+
                " No workbook cells were changed.",error);
        }
    }
}

// Rebuild only the DISPLAY of an Excel range. Never render stored RawValue or
// Display from mappings; show formulas, types and actual styling/geometry.
internal static class DeskSpaceExcelSheetViewV101
{
    internal static Color ExcelColor(int bgr)
    {
        int value=bgr&0xFFFFFF;
        return Color.FromRgb((byte)(value&255),(byte)((value>>8)&255),
            (byte)((value>>16)&255));
    }
    internal static string ColumnLabel(int column)
    {
        string label="";
        while(column>0){column--;label=(char)('A'+column%26)+label;column/=26;}
        return label;
    }

    internal static FrameworkElement Create(DeskSpaceExcelTemplateV16 entry)
    {
        var visual=entry.SheetVisualV101;
        if(visual==null)
        {
            // Older mappings have no style snapshot. Show transparent legacy
            // representation, NEVER pretend unrecorded formatting was read.
            visual=new DeskSpaceExcelSheetVisualV101{
                Rows=Math.Min(Math.Max(entry.RowCount,1),80),
                Columns=Math.Min(Math.Max(entry.ColumnCount,1),25)
            };
            foreach(var c in entry.Cells.Where(c=>c.RowOffset>=0 && c.ColumnOffset>=0))
                if(c.RowOffset<visual.Rows && c.ColumnOffset<visual.Columns)
                    visual.Cells.Add(new DeskSpaceExcelStyledCellV101{
                        Row=c.RowOffset,Column=c.ColumnOffset,
                        FormulaR1C1=c.ValueKind=="formula"?c.FormulaR1C1:"",
                        NumberFormat=c.NumberFormat
                    });
        }
        int rows=Math.Clamp(visual.Rows,1,1800);
        int cols=Math.Clamp(visual.Columns,1,1800);
        int recorded=visual.Cells.Where(c=>c.Row>=0 && c.Row<rows &&
            c.Column>=0 && c.Column<cols).Select(c=>(c.Row,c.Column)).Distinct().Count();
        bool incomplete=entry.SheetVisualV101!=null && recorded<rows*cols;
        var report=visual.FeaturesV106;
        var metadata=(report?.Cells??new List<DeskSpaceExcelCellFeatureV106>())
            .GroupBy(x=>(x.Row,x.Column)).ToDictionary(g=>g.Key,g=>g.First());
        var layout=new StackPanel{Background=Brushes.White};
        // All formatting is indexed by each physical cell address;
        // we never use a range-wide style for this view.
        // Zoom the whole physical sheet uniformly; never stretch columns independently.
        var head=new TextBlock{
            Text=entry.SourceSheet+"  ·  "+entry.SourceAddress+
                "  ·  "+recorded+"/"+(rows*cols)+" individually captured cell styles"+
                (incomplete?" · INCOMPLETE: recapture the source range":"")+
                (report!=null?" · "+report.Tables.Count+" Excel table(s) · "+
                    report.Cells.Count(x=>x.UnderlineStyle!=-4142)+" underlined":" · legacy style snapshot"),
            Foreground=new SolidColorBrush(Color.FromRgb(43,64,94)),
            FontWeight=FontWeights.SemiBold,FontSize=12,
            Margin=new Thickness(10,9,4,6)
        };
        layout.Children.Add(head);
        if(report!=null)
        {
            if(report.Tables.Count>0)
                layout.Children.Add(new TextBlock{
                    Text="Excel tables: "+string.Join(" | ",report.Tables.Select(t=>
                        t.Name+" · "+t.Address+" · "+t.TableStyle+
                        (t.HasHeaders?" · header":"")+
                        (t.HasTotals?" · totals":"")+
                        (t.FullySelected?"":" · PARTIAL"))),
                    Margin=new Thickness(10,1,5,6),FontSize=11,
                    TextWrapping=TextWrapping.Wrap,
                    Foreground=new SolidColorBrush(Color.FromRgb(37,75,115))
                });
            layout.Children.Add(new TextBlock{
                Text="Macro-like template coverage: fonts, cell number formats, "+
                    "formulas and table identities are captured. NOT VBA action recording. "+
                    "Not replayable: "+string.Join(" | ",report.Limitations),
                Margin=new Thickness(10,1,5,7),FontSize=10,
                TextWrapping=TextWrapping.Wrap,
                Foreground=new SolidColorBrush(Color.FromRgb(132,72,37))
            });
        }
        var detail=new TextBlock{
            Text=entry.SheetVisualV101==null?
                "Older mapping: no cell-style capture; select the original Excel range and capture again.":
                incomplete?
                "Some older cells were not captured individually. Recapture the original selected range before trusting the preview.":
                "Select any cell to inspect its OWN number format, font, fill, borders and formula.",
            Foreground=new SolidColorBrush(Color.FromRgb(72,84,112)),
            FontSize=11,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(10,4,6,8)
        };
        var grid=new Grid{Background=Brushes.White};
        grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42)});
        for(int c=0;c<cols;c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition{
                Width=new GridLength(c<visual.ColumnWidths.Count?
                    Math.Clamp(visual.ColumnWidths[c],38,440):105)});
        grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(27)});
        for(int r=0;r<rows;r++)
            grid.RowDefinitions.Add(new RowDefinition{
                Height=new GridLength(r<visual.RowHeights.Count?
                    Math.Clamp(visual.RowHeights[r],18,220):26)});
        for(int c=0;c<cols;c++)
        {
            var label=Header(ColumnLabel(entry.SourceColumn+c));
            Grid.SetColumn(label,c+1);Grid.SetRow(label,0);
            grid.Children.Add(label);
        }
        for(int r=0;r<rows;r++)
        {
            var label=Header((entry.SourceRow+r).ToString(CultureInfo.InvariantCulture));
            Grid.SetColumn(label,0);Grid.SetRow(label,r+1);
            grid.Children.Add(label);
        }
        var index=visual.Cells.Where(c=>c.Row<rows && c.Column<cols &&
            c.Row>=0 && c.Column>=0).
            GroupBy(c=>(c.Row,c.Column)).ToDictionary(g=>g.Key,g=>g.First());
        var occupied=new HashSet<(int,int)>();
        for(int r=0;r<rows;r++)
        for(int c=0;c<cols;c++)
        {
            if(occupied.Contains((r,c)))continue;
            index.TryGetValue((r,c),out var item);
            metadata.TryGetValue((r,c),out var feature);
            int rowSpan=Math.Min(item?.MergeRows??1,rows-r);
            int colSpan=Math.Min(item?.MergeColumns??1,cols-c);
            var isFormula=item!=null &&
                (!string.IsNullOrEmpty(item.FormulaR1C1) ||
                 !string.IsNullOrEmpty(item.FormulaA1));
            var border=new Border{
                Background=new SolidColorBrush(
                    ExcelColor(item?.DisplayStyleAvailable==true?
                        item.DisplayFillBgr:item?.FillBgr??0xFFFFFF)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(210,218,230)),
                BorderThickness=new Thickness(0.4),
                ToolTip=(item?.Address??"")+
                    (isFormula?" · "+item!.FormulaA1:" · "+
                    (item?.NumberFormat??"General")+
                    " · stored: "+(item?.StoredType??"Unknown")+
                    " · expected: "+(item?.ExpectedType??"Unknown")+
                    (feature==null?"":" · shape: "+feature.InputShape+
                        " · underline: "+UnderlineName(feature.UnderlineStyle)+
                        (feature.TableName.Length>0?" · "+feature.TableName+
                            " ("+feature.TableRole+")":""))+
                    " · current data intentionally hidden")
            };
            var text=new TextBlock{
                // Raw cell values and long formula expressions never spill over
                // neighboring cells; exact formula remains in tooltip/details.
                Text=isFormula?"ƒx":FeaturePlaceholder(item,feature),
                FontFamily=new FontFamily(item?.FontName is {Length:>0} family?
                    family:"Calibri"),
                TextTrimming=TextTrimming.CharacterEllipsis,
                Foreground=new SolidColorBrush(
                    ExcelColor(item?.DisplayStyleAvailable==true?
                        item.DisplayFontBgr:item?.FontBgr??0)),
                FontWeight=(item?.DisplayStyleAvailable==true?
                    item.DisplayBold:item?.Bold)==true?
                    FontWeights.Bold:FontWeights.Normal,
                FontStyle=(item?.DisplayStyleAvailable==true?
                    item.DisplayItalic:item?.Italic)==true?
                    FontStyles.Italic:FontStyles.Normal,
                // A 60 pt Excel font must not draw across tiny row boundaries.
                // Its true size is retained in the saved cell and inspector.
                FontSize=Math.Min(Math.Clamp(item?.FontSize??11,7,60),
                    Math.Max(9,(r<visual.RowHeights.Count?
                        visual.RowHeights[r]:26)*Math.Max(1,rowSpan)*0.60)),
                VerticalAlignment=item?.VerticalAlignment switch{
                    -4160=>VerticalAlignment.Top,  // xlVAlignTop
                    -4107=>VerticalAlignment.Bottom, // xlVAlignBottom
                    _=>VerticalAlignment.Center
                },
                HorizontalAlignment=item?.HorizontalAlignment switch
                {
                    -4108=>HorizontalAlignment.Center, // xlHAlignCenter
                    -4152=>HorizontalAlignment.Right,  // xlHAlignRight
                    _=>HorizontalAlignment.Left
                },
                Margin=new Thickness(4+Math.Clamp(item?.IndentLevel??0,0,15)*9,
                    1,4,1),
                TextWrapping=item?.WrapText==true && rowSpan>1?
                    TextWrapping.Wrap:TextWrapping.NoWrap
            };
            if(feature?.UnderlineStyle!=-4142 && feature!=null ||
               item?.Underline==true)
                text.TextDecorations=TextDecorations.Underline;
            if(item?.StrikeThrough==true)
            {
                var marks=new TextDecorationCollection();
                if(item.Underline)
                    foreach(var mark in TextDecorations.Underline)marks.Add(mark.Clone());
                foreach(var mark in TextDecorations.Strikethrough)marks.Add(mark.Clone());
                text.TextDecorations=marks;
            }
            // Each visible cell owns independent fill/font/border layers.
            // Missing adjacent cells are not assigned styles by inference.
            var surface=new Grid{ClipToBounds=true};
            if(item!=null)
            {
                AddBorderLine(surface,item.BorderTop,item.BorderTopColor,
                    0,0,1,0,item.BorderTopWeight);
                AddBorderLine(surface,item.BorderBottom,item.BorderBottomColor,
                    0,1,1,1,item.BorderBottomWeight);
                AddBorderLine(surface,item.BorderLeft,item.BorderLeftColor,
                    0,0,0,1,item.BorderLeftWeight);
                AddBorderLine(surface,item.BorderRight,item.BorderRightColor,
                    1,0,1,1,item.BorderRightWeight);
            }
            // Draw Excel borders behind text, not across its glyphs.
            surface.Children.Add(text);
            border.Child=surface;
            int localRow=r,localCol=c;
            border.MouseLeftButtonDown+=(_,_)=>{
                var address=item?.Address??(
                    ColumnLabel(entry.SourceColumn+localCol)+
                    (entry.SourceRow+localRow).ToString(CultureInfo.InvariantCulture));
                detail.Text=address+" · "+(item?.NumberFormat??"General")+
                    (item==null?" · No captured style":
                     " · "+item.FontName+" "+item.FontSize.ToString("0.#",
                        CultureInfo.InvariantCulture)+" pt"+
                     (item.Bold?" · Bold":"")+
                     (item.Italic?" · Italic":"")+
                     (item.MergeColumns>1 || item.MergeRows>1?
                        " · Merged "+item.MergeRows+"×"+item.MergeColumns:""))+
                    (feature==null?"":
                        " · Input "+feature.InputShape+
                        " · Underline "+UnderlineName(feature.UnderlineStyle)+
                        (feature.Superscript?" · Superscript":"")+
                        (feature.Subscript?" · Subscript":"")+
                        " · Named style "+feature.StyleName+
                        (feature.ValidationType<0?"":" · Validation "+feature.ValidationType+
                            " ("+feature.ValidationFormula1+")")+
                        (feature.ConditionalRuleCount>0?
                            " · "+feature.ConditionalRuleCount+" conditional rule(s)":"")+
                        (feature.TableName.Length>0?" · Table "+feature.TableName+
                            " "+feature.TableRole:"")+
                        (feature.Formula2.Length>0?" · Formula2 "+feature.Formula2:""))+
                    (item==null?"":
                        (item.DisplayStyleAvailable?
                            " · Display fill #"+(item.DisplayFillBgr&0xFFFFFF).ToString("X6")+
                            " · Display font #"+(item.DisplayFontBgr&0xFFFFFF).ToString("X6"):"")+
                        " · Direct pattern "+item.DirectFillPattern+
                        " · Font colour index "+item.DirectFontColorIndex+
                        (item.DirectFontThemeColor>0?
                            " · Font theme "+item.DirectFontThemeColor:"")+
                        " · Direct fill #"+(item.FillBgr&0xFFFFFF).ToString("X6")+
                        " · Font color #"+(item.FontBgr&0xFFFFFF).ToString("X6")+
                        " · Edges "+(item.BorderLeft?"L":"")+(item.BorderTop?"T":"")+
                            (item.BorderRight?"R":"")+(item.BorderBottom?"B":"")+
                        " · Alignment "+item.HorizontalAlignment+"/"+item.VerticalAlignment+
                        (item.WrapText?" · Wrap":"")+
                        (item.ConditionalAppearance?" · Conditional appearance":""))+
                    (isFormula?"  |  "+(item!.FormulaA1.Length>0?
                        item.FormulaA1:item.FormulaR1C1):"  |  Data hidden")+
                    "  |  Stored: "+(item?.StoredType??"Unknown")+
                    "  |  Expected: "+(item?.ExpectedType??"Unknown")+
                    (item?.StoredType=="Text" &&
                     item?.ExpectedType is "Number" or "Date" or "DateTime"?
                        "  |  Type mismatch: conversion requires validation":"");
            };
            Grid.SetColumn(border,c+1);Grid.SetRow(border,r+1);
            if(rowSpan>1)Grid.SetRowSpan(border,rowSpan);
            if(colSpan>1)Grid.SetColumnSpan(border,colSpan);
            grid.Children.Add(border);
            for(int rr=r;rr<r+rowSpan;rr++)
                for(int cc=c;cc<c+colSpan;cc++)
                    if(rr!=r || cc!=c)occupied.Add((rr,cc));
        }
        layout.Children.Add(grid);
        layout.Children.Add(detail);
        var preview=new DockPanel{Background=Brushes.White,LastChildFill=true};
        var zoomBar=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(10,4,8,5)};
        zoomBar.Children.Add(new TextBlock{Text="Sheet zoom",
            VerticalAlignment=VerticalAlignment.Center,
            Foreground=new SolidColorBrush(Color.FromRgb(60,76,97)),
            Margin=new Thickness(0,0,8,0)});
        var zoom=new ComboBox{Width=122,Height=28};
        foreach(var setting in new[]{"Fit width","100%","150%","200%","300%"})
            zoom.Items.Add(setting);
        zoomBar.Children.Add(zoom);
        zoomBar.Children.Add(new TextBlock{
            Text="  ·  Select a cell for complete formatting and formula details",
            VerticalAlignment=VerticalAlignment.Center,
            Foreground=new SolidColorBrush(Color.FromRgb(91,108,125)),
            FontSize=11});
        DockPanel.SetDock(zoomBar,Dock.Top);
        preview.Children.Add(zoomBar);
        var scale=new ScaleTransform(1,1);
        // Scale the Excel grid only: keep title, inspector and toolbar legible.
        grid.LayoutTransform=scale;
        var scroll=new ScrollViewer{
            Background=Brushes.White,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=layout
        };
        void UpdateZoom()
        {
            string selected=zoom.SelectedItem as string??"Fit width";
            var width=42+Enumerable.Range(0,cols).Sum(c=>
                c<visual.ColumnWidths.Count?
                Math.Clamp(visual.ColumnWidths[c],38,440):105);
            double factor=selected switch {
                "100%"=>1.0,"150%"=>1.5,"200%"=>2.0,"300%"=>3.0,
                _=>Math.Clamp((scroll.ActualWidth-36)/Math.Max(width,1),1.0,4.5)
            };
            scale.ScaleX=factor;scale.ScaleY=factor;
        }
        zoom.SelectionChanged+=(_,_)=>UpdateZoom();
        scroll.SizeChanged+=(_,_)=>{
            if((zoom.SelectedItem as string)=="Fit width")UpdateZoom();
        };
        zoom.SelectedIndex=0;
        preview.Children.Add(scroll);
        return preview;
    }
    internal static DeskSpaceExcelSheetVisualV101? MergeCapturedVisuals(
        DeskSpaceExcelSheetVisualV101? prior,int priorRowShift,int priorColumnShift,
        DeskSpaceExcelSheetVisualV101? addition,int newRowShift,int newColumnShift,
        int rows,int columns)
    {
        if(prior==null && addition==null)return null;
        // Never silently retain just the earlier half of a larger capture.
        if(rows<1 || columns<1 || rows*(long)columns>1800)
            throw new InvalidOperationException(
                "Combined captured area exceeds 1800 cells. Select smaller ranges.");
        var merged=new DeskSpaceExcelSheetVisualV101{
            Rows=rows,Columns=columns,
            RowHeights=Enumerable.Repeat(26d,rows).ToList(),
            ColumnWidths=Enumerable.Repeat(105d,columns).ToList(),
            NativeRowHeights=Enumerable.Repeat(15d,rows).ToList(),
            NativeColumnWidths=Enumerable.Repeat(8.43d,columns).ToList()
        };
        void Layer(DeskSpaceExcelSheetVisualV101 layer,int rowBase,int colBase)
        {
            for(int i=0;i<layer.RowHeights.Count;i++)
                if(i+rowBase>=0&&i+rowBase<rows)
                    merged.RowHeights[i+rowBase]=layer.RowHeights[i];
            for(int i=0;i<layer.NativeRowHeights.Count;i++)
                if(i+rowBase>=0 && i+rowBase<rows)
                    merged.NativeRowHeights[i+rowBase]=layer.NativeRowHeights[i];
            for(int i=0;i<layer.ColumnWidths.Count;i++)
                if(i+colBase>=0&&i+colBase<columns)
                    merged.ColumnWidths[i+colBase]=layer.ColumnWidths[i];
            for(int i=0;i<layer.NativeColumnWidths.Count;i++)
                if(i+colBase>=0 && i+colBase<columns)
                    merged.NativeColumnWidths[i+colBase]=layer.NativeColumnWidths[i];
        }
        if(prior!=null)Layer(prior,priorRowShift,priorColumnShift);
        if(addition!=null)Layer(addition,newRowShift,newColumnShift);
        var cells=new Dictionary<(int,int),DeskSpaceExcelStyledCellV101>();
        foreach(var (snapshot,dr,dc) in new[]{
            (prior,priorRowShift,priorColumnShift),
            (addition,newRowShift,newColumnShift)})
            foreach(var cell in snapshot?.Cells??Enumerable.Empty<DeskSpaceExcelStyledCellV101>())
            {
                var mapped=cell.OffsetCopy(dr,dc);
                if(mapped.Row>=0&&mapped.Row<rows&&
                   mapped.Column>=0&&mapped.Column<columns)
                    cells[(mapped.Row,mapped.Column)]=mapped;
            }
        merged.Cells=cells.Values.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToList();
        merged.FeaturesV106=DeskSpaceExcelSemanticV106.Merge(
            prior?.FeaturesV106,priorRowShift,priorColumnShift,
            addition?.FeaturesV106,newRowShift,newColumnShift);
        return merged;
    }

    internal static string UnderlineName(int style)=>style switch{
        -4142=>"None",2=>"Single",-4119=>"Double",4=>"Single accounting",
        5=>"Double accounting",_=>"Excel style "+style};
    internal static string FeaturePlaceholder(
        DeskSpaceExcelStyledCellV101? cell,DeskSpaceExcelCellFeatureV106? feature)
    {
        if(cell==null)return "";
        if(feature==null)return Placeholder(cell);
        if(cell.StoredType=="Blank")return cell.Underline||cell.Bold||cell.Italic?
            "Aa":"";
        return feature.InputShape switch {
            "Alphabet"=>"ABC","Words"=>"Words",
            "Alphanumeric"=>"A1b","Digits stored as text"=>"00123",
            "Date/time text"=>"date?",
            "Date"=>"25 Oct","DateTime"=>"25 Oct 10:30","Time"=>"10:30",
            "Percentage"=>"12%","Number"=>"123","Boolean"=>"TRUE",
            "Formula"=>"ƒx","Excel error"=>"#ERR",_=>Placeholder(cell)
        };
    }
    internal static string Placeholder(DeskSpaceExcelStyledCellV101? cell)
    {
        if(cell==null)return "";
        // Deliberately use samples so the old input data never appears.
        if(cell.StoredType=="Blank")
            return cell.Bold||cell.Italic||cell.Underline||cell.StrikeThrough?
                "Aa":""; // Only formatting needs a visible text sample.
        return cell.ExpectedType switch
        {
            "Date"=>"dd/mm",
            "DateTime"=>"date",
            "Time"=>"time",
            "Percentage"=>"%",
            "Number"=>"123",
            "Boolean"=>"TRUE",
            "Text"=>"Aa",
            _=>cell.StoredType=="Text"?"Aa":""
        };
    }

    // Keep true independent Excel edge colours instead of simplifying all
    // four sides to one generic WPF BorderBrush.
    private static void AddBorderLine(Grid surface,bool present,int bgr,
        double x1,double y1,double x2,double y2,int weight)
    {
        if(!present)return;
        // The previous stretched 0..1 WPF Line can render through the
        // MIDDLE of a cell after layout/zoom. Edge-aligned overlays never
        // cross neighboring text, even with different border colors.
        double thick=weight>=4?2.5:weight>=3?1.7:1.1;
        var edge=new Border{
            Background=new SolidColorBrush(ExcelColor(bgr)),
            IsHitTestVisible=false,
            SnapsToDevicePixels=true
        };
        if(y1==y2)
        {
            edge.Height=thick;
            edge.VerticalAlignment=y1==0?VerticalAlignment.Top:
                VerticalAlignment.Bottom;
            edge.HorizontalAlignment=HorizontalAlignment.Stretch;
        }
        else
        {
            edge.Width=thick;
            edge.HorizontalAlignment=x1==0?HorizontalAlignment.Left:
                HorizontalAlignment.Right;
            edge.VerticalAlignment=VerticalAlignment.Stretch;
        }
        surface.Children.Add(edge);
    }

    private static Border Header(string content)=>new(){
        Child=new TextBlock{
            Text=content,TextAlignment=TextAlignment.Center,
            VerticalAlignment=VerticalAlignment.Center,FontSize=10,
            Foreground=new SolidColorBrush(Color.FromRgb(58,72,96))
        },
        Background=new SolidColorBrush(Color.FromRgb(235,241,248)),
        BorderBrush=new SolidColorBrush(Color.FromRgb(205,214,226)),
        BorderThickness=new Thickness(0.5)
    };
}
