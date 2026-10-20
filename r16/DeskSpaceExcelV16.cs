using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace DeskSpace;

// One in-widget workflow: image region mark -> exact Excel range capture ->
// encrypted reusable mapping -> anchor matching -> preview -> confirmed autofill.
// A screenshot alone cannot supply authoritative cell values or formulas.
internal sealed class DeskSpaceExcelV16 : IDisposable
{
    private readonly Action back;
    private readonly List<DeskSpaceExcelTemplateV16> saved;
    private readonly Brush ink=new SolidColorBrush(Color.FromRgb(35,43,70));
    private readonly Brush muted=new SolidColorBrush(Color.FromRgb(99,108,132));
    private ListBox templates=null!,previewRows=null!;
    private TextBox name=null!,range=null!;
    private TextBlock status=null!,matchSummary=null!;
    private CheckBox formulas=null!,overwrite=null!,manual=null!;
    private Image picture=null!;
    private Canvas drawing=null!;
    private Rectangle mark=null!;
    private DeskSpaceExcelProposalV16? latest;
    private Guid previewTemplateId;
    private byte[]? screenshotBytes;
    private double markX,markY,markW,markH;
    private Point mouseDown;
    private bool marking;
    private bool disposed;

    public DeskSpaceExcelV16(Action exit)
    {
        back=exit;
        saved=DeskSpaceExcelStoreV16.Load();
    }
    private static Button ActionButton(string text,Action run)
    {
        var b=new Button{
            Content=text,MinHeight=30,Padding=new Thickness(10,4,10,4),
            Margin=new Thickness(2),FontSize=12,
            Background=Brushes.White,
            BorderBrush=new SolidColorBrush(Color.FromRgb(192,197,220)),
            Cursor=Cursors.Hand
        };
        b.Click+=(_,_)=>run();
        return b;
    }
    private TextBlock Caption(string text,int size=13,bool bold=false)=>
        new(){Text=text,FontSize=size,Foreground=ink,
            Margin=new Thickness(2,5,2,3),
            TextWrapping=TextWrapping.Wrap,
            FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};
    private TextBox Field(string hint,string value="")
        =>new(){Text=value,ToolTip=hint,FontSize=13,
            Padding=new Thickness(8,5,8,5),Margin=new Thickness(2),
            Background=Brushes.White,
            BorderBrush=new SolidColorBrush(Color.FromRgb(194,198,218))};
    private static WrapPanel Buttons(params Button[] controls)
    {
        var wrap=new WrapPanel {Margin=new Thickness(0,3,0,5)};
        foreach(var b in controls)wrap.Children.Add(b);
        return wrap;
    }
    private void Run(Action work)
    {
        try{work();}
        catch(Exception ex){status.Text="Unable to continue: "+ex.Message;}
    }
    private DeskSpaceExcelTemplateV16 Selected=>
        templates.SelectedItem as DeskSpaceExcelTemplateV16
        ??throw new InvalidOperationException("Select a saved Excel mapping first.");
    public FrameworkElement Create()
    {
        var surface=new DockPanel{
            Background=new SolidColorBrush(Color.FromRgb(247,249,254)),
            LastChildFill=true
        };
        var top=new DockPanel{
            Background=new SolidColorBrush(Color.FromRgb(34,45,84)),
            Margin=new Thickness(0),LastChildFill=true
        };
        var done=ActionButton("← Circle",back);
        DockPanel.SetDock(done,Dock.Right);top.Children.Add(done);
        var title=new TextBlock{
            Text="▦  Excel · Reusable data mapping",FontSize=18,
            Foreground=Brushes.White,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(13,12,3,11),
            VerticalAlignment=VerticalAlignment.Center
        };
        top.Children.Add(title);
        DockPanel.SetDock(top,Dock.Top);surface.Children.Add(top);
        var scroll=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled
        };
        var body=new StackPanel{Margin=new Thickness(13,8,13,16)};
        scroll.Content=body;surface.Children.Add(scroll);
        body.Children.Add(Caption("1 · Choose your source",15,true));
        body.Children.Add(Caption(
            "Open an Excel workbook and highlight the exact cells. Optionally load a screenshot and draw a rectangle to remember the visual region.",12));
        body.Children.Add(Buttons(
            ActionButton("Open workbook…",()=>Run(OpenWorkbook)),
            ActionButton("Add screenshot…",()=>Run(OpenScreenshot))
        ));
        var frame=new Border{
            BorderBrush=new SolidColorBrush(Color.FromRgb(197,201,219)),
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(9),
            Background=Brushes.White,
            Margin=new Thickness(2,5,2,8)
        };
        var visualScroll=new ScrollViewer{
            Height=176,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto
        };
        drawing=new Canvas{Width=250,Height=150,Background=Brushes.WhiteSmoke};
        picture=new Image{Stretch=Stretch.Fill};
        drawing.Children.Add(picture);
        mark=new Rectangle{
            Visibility=Visibility.Collapsed,
            Stroke=new SolidColorBrush(Color.FromRgb(54,119,230)),
            StrokeThickness=2,
            Fill=new SolidColorBrush(Color.FromArgb(48,73,155,241)),
            IsHitTestVisible=false
        };
        drawing.Children.Add(mark);
        drawing.MouseLeftButtonDown+=DrawStart;
        drawing.MouseMove+=DrawMove;
        drawing.MouseLeftButtonUp+=DrawEnd;
        visualScroll.Content=drawing;frame.Child=visualScroll;
        body.Children.Add(frame);
        name=Field("Name this information","Signatory details");
        range=Field("Optional Excel range, e.g. B20:D25; empty means current selection","");
        body.Children.Add(Caption("What information should DeskSpace save?",13,true));
        body.Children.Add(name);
        body.Children.Add(Caption("Exact Excel range (optional)",12));
        body.Children.Add(range);
        formulas=new CheckBox{
            Content="Save formulas as formulas (otherwise save their displayed values)",
            IsChecked=false,Margin=new Thickness(5,5,0,6),FontSize=12
        };
        body.Children.Add(formulas);
        body.Children.Add(Buttons(
            ActionButton("Capture selected Excel cells",()=>Run(Capture))
        ));
        body.Children.Add(Caption("2 · Saved reusable information",15,true));
        templates=new ListBox{
            Height=105,
            DisplayMemberPath="Name",
            Margin=new Thickness(2,2,2,5),
            BorderBrush=new SolidColorBrush(Color.FromRgb(197,201,219))
        };
        templates.SelectionChanged+=(_,_)=>{
            latest=null;matchSummary.Text="Select a mapping and preview the matching destination.";
            previewRows.ItemsSource=null;
            if(templates.SelectedItem is DeskSpaceExcelTemplateV16 selected)
            {
                matchSummary.Text=selected.Name+" · "+selected.SourceSheet+
                    " · "+selected.SourceAddress+" · "+selected.Cells.Count+" saved values";
                if(!string.IsNullOrEmpty(selected.ScreenshotBase64))
                    ShowScreenshot(Convert.FromBase64String(selected.ScreenshotBase64),
                        selected.ScreenshotSelection);
            }
        };
        body.Children.Add(templates);
        body.Children.Add(Buttons(
            ActionButton("Delete saved mapping",()=>Run(Delete))
        ));
        body.Children.Add(Caption("3 · Match and autofill another workbook",15,true));
        body.Children.Add(Caption(
            "Open the destination in Excel. Use structural labels to locate corresponding cells, or select the first target cell yourself.",12));
        manual=new CheckBox{
            Content="Use the currently selected Excel cell as the destination starting cell",
            IsChecked=false,Margin=new Thickness(4,4,0,4),FontSize=12};
        overwrite=new CheckBox{
            Content="Replace existing non-formula values (normally leave existing cells untouched)",
            IsChecked=false,Margin=new Thickness(4,3,0,5),FontSize=12};
        manual.Checked+=(_,_)=>latest=null;
        manual.Unchecked+=(_,_)=>latest=null;
        overwrite.Checked+=(_,_)=>latest=null;
        overwrite.Unchecked+=(_,_)=>latest=null;
        body.Children.Add(manual);body.Children.Add(overwrite);
        body.Children.Add(Buttons(
            ActionButton("Preview matching cells",()=>Run(Preview)),
            ActionButton("Apply autofill",()=>Run(Apply))
        ));
        matchSummary=Caption("No destination preview yet.",12);
        matchSummary.Foreground=muted;body.Children.Add(matchSummary);
        previewRows=new ListBox{
            Height=118,FontSize=11,Margin=new Thickness(2,4,2,5),
            BorderBrush=new SolidColorBrush(Color.FromRgb(197,201,219))
        };
        body.Children.Add(previewRows);
        status=new TextBlock{
            Text="Cell data is encrypted for this Windows account. Destination formatting and formulas are protected by default.",
            Foreground=muted,FontSize=12,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(3,9,2,4)
        };
        body.Children.Add(status);
        Refresh();
        return surface;
    }
    private void Refresh(Guid? id=null)
    {
        templates.ItemsSource=null;
        templates.ItemsSource=saved.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        if(id.HasValue)
            templates.SelectedItem=saved.FirstOrDefault(x=>x.Id==id.Value);
    }
    private void OpenWorkbook()
    {
        var dialog=new OpenFileDialog{
            Filter="Excel workbooks|*.xlsx;*.xlsm;*.xls;*.xlsb",
            CheckFileExists=true,Title="Open source or destination workbook"
        };
        if(dialog.ShowDialog()!=true)return;
        DeskSpaceExcelBridgeV16.OpenWorkbook(dialog.FileName);
        status.Text="Workbook opened in Excel. Highlight the cells to capture (or enter their range).";
    }
    private void OpenScreenshot()
    {
        var dialog=new OpenFileDialog{
            Filter="Screenshots|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists=true,Title="Choose an Excel layout screenshot"
        };
        if(dialog.ShowDialog()!=true)return;
        var bytes=File.ReadAllBytes(dialog.FileName);
        if(bytes.Length>5000000)throw new InvalidOperationException(
            "Use a screenshot smaller than 5 MB.");
        ShowScreenshot(bytes,"");
        status.Text="Drag a rectangle over the visual region, then capture its exact cells from Excel.";
    }
    private void ShowScreenshot(byte[] bytes,string region)
    {
        screenshotBytes=bytes;
        var bmp=new BitmapImage();
        using(var memory=new MemoryStream(bytes,false))
        {
            bmp.BeginInit();
            bmp.CacheOption=BitmapCacheOption.OnLoad;
            bmp.StreamSource=memory;
            bmp.EndInit();
        }
        bmp.Freeze();
        drawing.Width=bmp.PixelWidth;
        drawing.Height=bmp.PixelHeight;
        picture.Source=bmp;
        picture.Width=bmp.PixelWidth;
        picture.Height=bmp.PixelHeight;
        mark.Visibility=Visibility.Collapsed;
        markW=markH=0;
        if(string.IsNullOrWhiteSpace(region))return;
        var parts=region.Split(',');
        if(parts.Length!=4)return;
        if(double.TryParse(parts[0],System.Globalization.NumberStyles.Float,
             System.Globalization.CultureInfo.InvariantCulture,out var x) &&
           double.TryParse(parts[1],System.Globalization.NumberStyles.Float,
             System.Globalization.CultureInfo.InvariantCulture,out var y) &&
           double.TryParse(parts[2],System.Globalization.NumberStyles.Float,
             System.Globalization.CultureInfo.InvariantCulture,out var w) &&
           double.TryParse(parts[3],System.Globalization.NumberStyles.Float,
             System.Globalization.CultureInfo.InvariantCulture,out var h))
        {
            markX=x*drawing.Width;markY=y*drawing.Height;
            markW=w*drawing.Width;markH=h*drawing.Height;PaintMark();
        }
    }
    private Point Limit(Point p)=>new(
        Math.Clamp(p.X,0,drawing.Width),Math.Clamp(p.Y,0,drawing.Height));
    private void DrawStart(object sender,MouseButtonEventArgs e)
    {
        if(screenshotBytes==null)return;
        marking=true;mouseDown=Limit(e.GetPosition(drawing));
        drawing.CaptureMouse();e.Handled=true;
    }
    private void DrawMove(object sender,MouseEventArgs e)
    {
        if(!marking)return;
        var p=Limit(e.GetPosition(drawing));
        markX=Math.Min(mouseDown.X,p.X);markY=Math.Min(mouseDown.Y,p.Y);
        markW=Math.Abs(mouseDown.X-p.X);markH=Math.Abs(mouseDown.Y-p.Y);
        PaintMark();
    }
    private void DrawEnd(object sender,MouseButtonEventArgs e)
    {
        if(!marking)return;
        marking=false;drawing.ReleaseMouseCapture();DrawMove(sender,e);
        e.Handled=true;
    }
    private void PaintMark()
    {
        mark.Visibility=markW>3&&markH>3?Visibility.Visible:Visibility.Collapsed;
        mark.Width=markW;mark.Height=markH;
        Canvas.SetLeft(mark,markX);Canvas.SetTop(mark,markY);
    }
    private string Region()
    {
        if(screenshotBytes==null||markW<=3||markH<=3)return "";
        return string.Join(",",new[]{markX/drawing.Width,markY/drawing.Height,
            markW/drawing.Width,markH/drawing.Height}
            .Select(x=>x.ToString("G17",System.Globalization.CultureInfo.InvariantCulture)));
    }
    private void Capture()
    {
        var entry=DeskSpaceExcelBridgeV16.Capture(name.Text,range.Text,
            formulas.IsChecked==true);
        if(screenshotBytes!=null)
        {
            entry.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
            entry.ScreenshotSelection=Region();
        }
        saved.Add(entry);
        DeskSpaceExcelStoreV16.Save(saved);
        Refresh(entry.Id);
        status.Text="Saved "+entry.Cells.Count+" exact cells from "+
            entry.SourceSheet+" "+entry.SourceAddress+
            ". Existing templates were kept.";
    }
    private void Delete()
    {
        var entry=Selected;
        if(MessageBox.Show("Delete mapping '"+entry.Name+"'?\n"+
            "This does not change any Excel workbook.",
            "DeskSpace",MessageBoxButton.YesNo,MessageBoxImage.Question)
            !=MessageBoxResult.Yes)return;
        saved.Remove(entry);DeskSpaceExcelStoreV16.Save(saved);
        latest=null;Refresh();
        status.Text="Mapping deleted. Excel workbooks were not changed.";
    }
    private void Preview()
    {
        var entry=Selected;
        latest=DeskSpaceExcelBridgeV16.Preview(entry,
            manual.IsChecked==true,overwrite.IsChecked==true);
        previewTemplateId=entry.Id;
        matchSummary.Text=latest.SheetName+" · "+latest.Reason+
            "\n"+latest.Writable+" writeable values out of "+
            latest.Changes.Count+" captured values";
        previewRows.ItemsSource=latest.Changes.Take(250).ToArray();
        status.Text=latest.Verified?
            "Review the proposed changes. Existing formulas and formatting will be preserved.":
            "No safe automatic position found. Select the first target cell in Excel, tick manual target, and preview.";
    }
    private void Apply()
    {
        var entry=Selected;
        if(latest==null||previewTemplateId!=entry.Id)
            throw new InvalidOperationException("Preview this mapping first.");
        if(!latest.Verified||latest.Writable==0)
            throw new InvalidOperationException("Nothing is approved to autofill.");
        var choice=MessageBox.Show(
            "Write "+latest.Writable+" cells to '"+latest.SheetName+"'?\n"+
            "A backup will be created first. The live Excel workbook will not be auto-saved.\n"+
            "Existing formulas will not be replaced.",
            "Confirm Excel autofill",MessageBoxButton.YesNo,MessageBoxImage.Question);
        if(choice!=MessageBoxResult.Yes)return;
        var backup=DeskSpaceExcelBridgeV16.Apply(latest,entry,overwrite.IsChecked==true);
        status.Text="Autofill sent to Excel. Backup: "+backup+
            ". Review and save your workbook in Excel.";
        latest=null;
    }
    public void Dispose(){disposed=true;screenshotBytes=null;}
}
