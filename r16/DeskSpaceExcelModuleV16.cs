using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace DeskSpace;

// Embedded DeskSpace MainWindow tab, not a separate application.
// The image markup is deliberately visual-only until the user confirms an
// exact workbook cell range, which Excel COM reads without OCR guessing.
internal sealed class DeskSpaceExcelModuleV16
{
    private readonly TextBox source=new();
    private readonly TextBox sheet=new();
    private readonly TextBox range=new();
    private readonly TextBox name=new();
    private readonly TextBox description=new();
    private readonly TextBox target=new();
    private readonly TextBox targetSheet=new();
    private readonly TextBox destinationStart=new();
    private readonly CheckBox overwrite=new();
    private readonly TextBlock status=new();
    private readonly TextBlock captureStatus=new();
    private readonly ComboBox saved=new();
    private readonly DataGrid cells=new();
    private readonly DataGrid preview=new();
    private readonly Image referenceImage=new();
    private readonly Canvas markings=new();
    private readonly Rectangle highlight=new();
    private readonly ObservableCollection<DeskSpaceExcelCellV16> captured=new();
    private DeskSpaceExcelProfileV16? current;
    private DeskSpaceExcelPlanV16? lastPlan;
    private Point start;
    private bool marking;
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(36,44,73));
    private static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(94,104,126));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(89,76,177));
    private static readonly Brush Light=new SolidColorBrush(Color.FromRgb(247,248,252));
    private static TextBlock Label(string text,int size=13)=>new(){
        Text=text,FontSize=size,Foreground=Ink,TextWrapping=TextWrapping.Wrap,
        Margin=new Thickness(0,5,0,4)
    };
    private static TextBox Field(TextBox box,string hint)
    {
        box.MinHeight=32;box.FontSize=13;box.Padding=new Thickness(8,5,8,5);
        box.Background=Brushes.White;
        box.BorderBrush=new SolidColorBrush(Color.FromRgb(196,200,222));
        box.Margin=new Thickness(0,2,7,7);
        box.ToolTip=hint;
        return box;
    }
    private static Button Action(string title,Action act)
    {
        var button=new Button{
            Content=title,MinHeight=32,Padding=new Thickness(10,5,10,5),
            Margin=new Thickness(0,3,8,4),Background=Brushes.White,
            Foreground=Ink,BorderBrush=new SolidColorBrush(Color.FromRgb(194,198,220))
        };
        button.Click+=(_,_)=>act();
        return button;
    }
    private static DataGrid Grid()
    {
        return new DataGrid{
            AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,
            IsReadOnly=true,MinHeight=115,MaxHeight=230,
            HeadersVisibility=DataGridHeadersVisibility.Column,
            GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,
            BorderBrush=new SolidColorBrush(Color.FromRgb(220,225,237)),
            Background=Brushes.White,AlternatingRowBackground=Light,
            Margin=new Thickness(0,4,0,9)
        };
    }
    private static DataGridTextColumn Column(string title,string path,double width=120)=>
        new(){
            Header=title,Binding=new Binding(path),
            Width=new DataGridLength(width)
        };
    private static FrameworkElement Panel(FrameworkElement child)
    {
        return new Border{Background=Light,
            BorderBrush=new SolidColorBrush(Color.FromRgb(226,227,238)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),
            Padding=new Thickness(14),Margin=new Thickness(0,0,0,14),Child=child};
    }
    private static WrapPanel Buttons()=>new(){Orientation=Orientation.Horizontal};
    private static void AddField(StackPanel body,string caption,TextBox box,string hint)
    {body.Children.Add(Label(caption));body.Children.Add(Field(box,hint));}
    private void Report(string message,bool error=false)
    {status.Text=message;status.Foreground=error?Brushes.Firebrick:Muted;}
    private static void Guard(Action action)
    {
        try{action();}
        catch(Exception ex){
            MessageBox.Show(ex.Message,"DeskSpace Excel",MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    private static void Pick(TextBox box,bool image=false)
    {
        var dialog=new OpenFileDialog {
            Filter=image?"Pictures|*.png;*.jpg;*.jpeg;*.bmp":
                "Excel workbooks|*.xlsx;*.xlsm;*.xls;*.xlsb",
            CheckFileExists=true
        };
        if(dialog.ShowDialog()==true)box.Text=dialog.FileName;
    }
    private void ResetPreview()
    {
        lastPlan=null;
        preview.ItemsSource=null;
    }
    private void ChooseScreenshot()
    {
        var dlg=new OpenFileDialog{
            Filter="Invoice or Excel screenshots|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists=true
        };
        if(dlg.ShowDialog()!=true)return;
        var bitmap=new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption=BitmapCacheOption.OnLoad;
        bitmap.UriSource=new Uri(dlg.FileName);
        bitmap.EndInit();
        bitmap.Freeze();
        referenceImage.Source=bitmap;
        highlight.Visibility=Visibility.Collapsed;
        Report("Screenshot loaded. Draw a visual area, then select the corresponding actual Excel range.");
    }
    private void StartMark(object? sender,MouseButtonEventArgs e)
    {
        if(referenceImage.Source==null)return;
        marking=true;start=e.GetPosition(markings);
        Canvas.SetLeft(highlight,start.X);Canvas.SetTop(highlight,start.Y);
        highlight.Width=0;highlight.Height=0;highlight.Visibility=Visibility.Visible;
        markings.CaptureMouse();e.Handled=true;
    }
    private void MoveMark(object? sender,MouseEventArgs e)
    {
        if(!marking)return;
        var p=e.GetPosition(markings);
        Canvas.SetLeft(highlight,Math.Min(start.X,p.X));
        Canvas.SetTop(highlight,Math.Min(start.Y,p.Y));
        highlight.Width=Math.Abs(p.X-start.X);
        highlight.Height=Math.Abs(p.Y-start.Y);
    }
    private void EndMark(object? sender,MouseButtonEventArgs e)
    {
        if(!marking)return;
        MoveMark(sender,e);
        marking=false;markings.ReleaseMouseCapture();
        Report("Visual area marked. Confirm the matching workbook cell range before saving.");
        e.Handled=true;
    }
    private void ReadSelection()
    {
        Guard(()=>{
            var data=DeskSpaceExcelEngineV16.ActiveSelection();
            source.Text=data.Path;sheet.Text=data.Sheet;range.Text=data.Range;
            Report("Excel selection found: "+data.Sheet+"!"+data.Range+". Choose what to save.");
        });
    }
    private void Capture()
    {
        Guard(()=>{
            if(!File.Exists(source.Text))throw new InvalidOperationException(
                "Choose a real Excel workbook, not only a screenshot.");
            current=DeskSpaceExcelEngineV16.Capture(
                source.Text,sheet.Text,range.Text,name.Text,description.Text);
            captured.Clear();
            foreach(var item in current.Cells)captured.Add(item);
            captureStatus.Text=captured.Count+" populated cells found · "+
                captured.Count(x=>x.Include)+" selected for saving. "+
                "Formula cells are excluded by default.";
            Report("Captured exact sheet, row, column, and typed cell values. Review checkboxes, then save.");
            ResetPreview();
        });
    }
    private void Save()
    {
        Guard(()=>{
            if(current==null)throw new InvalidOperationException("Capture an Excel range first.");
            current.Name=name.Text.Trim();
            current.Description=description.Text.Trim();
            if(current.Cells.Count(x=>x.Include&&!x.IsFormula)==0)
                throw new InvalidOperationException("Select at least one non-formula cell.");
            if(current.Cells.Any(x=>x.Include&&x.IsFormula))
                throw new InvalidOperationException(
                    "Formula values cannot be included in this value-only template. Untick formula cells.");
            DeskSpaceExcelStorageV16.Save(current);
            LoadProfiles(current.Id);
            Report("Saved "+current.Cells.Count(x=>x.Include)+" cells with exact addresses, "+
                current.Anchors.Count+" format headings and Windows user encryption.");
        });
    }
    private void LoadProfiles(Guid? selected=null)
    {
        var records=DeskSpaceExcelStorageV16.Load();
        saved.ItemsSource=records;
        saved.SelectedItem=records.FirstOrDefault(p=>p.Id==selected)??records.FirstOrDefault();
    }
    private void OnSavedSelection()
    {
        if(saved.SelectedItem is not DeskSpaceExcelProfileV16 profile)return;
        current=profile;
        name.Text=profile.Name;
        description.Text=profile.Description;
        sheet.Text=profile.Sheet;
        range.Text=profile.SourceRange;
        captured.Clear();
        foreach(var item in profile.Cells)captured.Add(item);
        captureStatus.Text=profile.Cells.Count+" recorded cells in saved profile.";
        targetSheet.Text=profile.Sheet;
        destinationStart.Text=DeskSpaceExcelEngineV16.Address(profile.FirstRow,profile.FirstColumn);
        ResetPreview();
    }
    private void Preview()
    {
        Guard(()=>{
            if(current==null)throw new InvalidOperationException("Select or capture a saved profile.");
            if(!File.Exists(target.Text))throw new InvalidOperationException("Choose a target workbook.");
            lastPlan=DeskSpaceExcelEngineV16.Preview(
                target.Text,current,targetSheet.Text,destinationStart.Text,overwrite.IsChecked==true);
            preview.ItemsSource=lastPlan.Entries;
            if(lastPlan.Issues.Count>0)
                Report("Not safe to apply: "+string.Join(" · ",lastPlan.Issues.Take(3)),true);
            else Report("Preview ready: "+lastPlan.Entries.Count+
                " mapped cells. The source and target files have not been changed.");
        });
    }
    private void Apply()
    {
        Guard(()=>{
            if(current==null||lastPlan==null||!lastPlan.CanApply)
                throw new InvalidOperationException("Run a matching, error-free preview first.");
            // Re-preview immediately before copying so a stale UI is not trusted.
            var latest=DeskSpaceExcelEngineV16.Preview(
                target.Text,current,targetSheet.Text,destinationStart.Text,overwrite.IsChecked==true);
            if(!latest.CanApply)
                throw new InvalidOperationException("Workbook changed since preview; run Preview again.");
            var confirm=MessageBox.Show(
                "Fill "+latest.Entries.Count(x=>x.Status is "Fill" or "Replace")+
                " cells in a NEW COPY of the workbook?\n\n"+
                "The original will remain unchanged. Existing formulas are never replaced. "+
                "The output is saved to Downloads.",
                "Confirm Excel autofill",MessageBoxButton.YesNo,MessageBoxImage.Question);
            if(confirm!=MessageBoxResult.Yes)return;
            var output=DeskSpaceExcelEngineV16.ApplyCopy(
                target.Text,current,targetSheet.Text,destinationStart.Text,overwrite.IsChecked==true);
            ResetPreview();
            Report("Created verified workbook copy in Downloads: "+output);
            MessageBox.Show("Saved completed Excel copy:\n"+output,
                "DeskSpace Excel",MessageBoxButton.OK,MessageBoxImage.Information);
        });
    }
    public FrameworkElement Create()
    {
        var outer=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            Background=Brushes.White
        };
        var stack=new StackPanel{Margin=new Thickness(18,14,18,28),MaxWidth=1060};
        outer.Content=stack;
        stack.Children.Add(new TextBlock{
            Text="Excel · Capture & Autofill",FontSize=25,
            FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(0,0,0,5)
        });
        stack.Children.Add(new TextBlock{
            Text="Mark the screenshot or select cells in Excel, choose exactly what to keep, "+
                 "then preview and fill matching workbooks safely.",
            FontSize=13,Foreground=Muted,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,13)
        });

        var screenshotPanel=new StackPanel();
        screenshotPanel.Children.Add(Label("1 · Screenshot reference (optional)",16));
        var imageButtons=Buttons();
        imageButtons.Children.Add(Action("Upload screenshot",()=>Guard(ChooseScreenshot)));
        imageButtons.Children.Add(Label("Drag across the picture to mark the intended area."));
        screenshotPanel.Children.Add(imageButtons);
        var imageFrame=new Grid{Height=230,MaxWidth=780,
            Background=new SolidColorBrush(Color.FromRgb(232,237,245)),
            ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left};
        imageFrame.Width=780;
        referenceImage.Stretch=Stretch.Uniform;
        imageFrame.Children.Add(referenceImage);
        markings.Background=Brushes.Transparent;
        markings.MouseLeftButtonDown+=StartMark;
        markings.MouseMove+=MoveMark;
        markings.MouseLeftButtonUp+=EndMark;
        highlight.Stroke=new SolidColorBrush(Color.FromRgb(135,72,232));
        highlight.StrokeThickness=2;
        highlight.Fill=new SolidColorBrush(Color.FromArgb(45,135,72,232));
        highlight.Visibility=Visibility.Collapsed;
        highlight.IsHitTestVisible=false;
        markings.Children.Add(highlight);
        imageFrame.Children.Add(markings);
        screenshotPanel.Children.Add(imageFrame);
        screenshotPanel.Children.Add(new TextBlock{
            Text="Screenshots guide your selection. Only the actual Excel workbook can supply "+
                 "exact cell addresses, merged cells, typed values and formulas.",
            Foreground=Muted,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,7,0,0)
        });
        stack.Children.Add(Panel(screenshotPanel));

        var capturePanel=new StackPanel();
        capturePanel.Children.Add(Label("2 · Select the exact data to save",16));
        AddField(capturePanel,"Template name / what to save",name,"Example: Audit signatory details");
        AddField(capturePanel,"Description (optional)",description,
            "Example: Entity signatory, auditor and partner-share information");
        capturePanel.Children.Add(Label("Source Excel workbook"));
        var sourceRow=Buttons();
        source.Width=590;sourceRow.Children.Add(Field(source,"Workbook path"));
        sourceRow.Children.Add(Action("Choose Excel file",()=>Guard(()=>Pick(source))));
        capturePanel.Children.Add(sourceRow);
        var addresses=Buttons();
        var sheetStack=new StackPanel{Width=220};AddField(sheetStack,"Worksheet",sheet,"Basic Info");
        var rangeStack=new StackPanel{Width=200};AddField(rangeStack,"Exact cells",range,"B21:C21 or B21:C45");
        addresses.Children.Add(sheetStack);addresses.Children.Add(rangeStack);
        addresses.Children.Add(Action("Use selected cells in Excel",ReadSelection));
        capturePanel.Children.Add(addresses);
        capturePanel.Children.Add(Action("Read cell data",Capture));
        cells.Columns.Add(new DataGridCheckBoxColumn{
            Header="Save",Binding=new Binding("Include"){Mode=BindingMode.TwoWay},
            Width=new DataGridLength(58),IsReadOnly=false
        });
        cells.Columns.Add(Column("Cell","Address",100));
        cells.Columns.Add(Column("Value as displayed","Caption",310));
        cells.Columns.Add(Column("Data type","Type",100));
        cells.ItemsSource=captured;
        capturePanel.Children.Add(cells);
        captureStatus.Foreground=Muted;
        capturePanel.Children.Add(captureStatus);
        capturePanel.Children.Add(Action("Save selected cells as reusable template",Save));
        stack.Children.Add(Panel(capturePanel));

        var targetPanel=new StackPanel();
        targetPanel.Children.Add(Label("3 · Fill a similar Excel workbook",16));
        targetPanel.Children.Add(Label("Saved templates"));
        saved.MinWidth=320;saved.MinHeight=33;
        saved.Margin=new Thickness(0,4,0,7);
        saved.SelectionChanged+=(_,_)=>OnSavedSelection();
        targetPanel.Children.Add(saved);
        targetPanel.Children.Add(Label("New workbook to fill"));
        var targetRow=Buttons();
        target.Width=590;targetRow.Children.Add(Field(target,"Select a matching workbook"));
        targetRow.Children.Add(Action("Choose target",()=>Guard(()=>Pick(target))));
        targetPanel.Children.Add(targetRow);
        var placement=Buttons();
        var tSheet=new StackPanel{Width=220};
        AddField(tSheet,"Target worksheet",targetSheet,"Keep source sheet or enter matching sheet");
        var tStart=new StackPanel{Width=200};
        AddField(tStart,"Target top-left cell",destinationStart,"Example B21");
        placement.Children.Add(tSheet);placement.Children.Add(tStart);
        targetPanel.Children.Add(placement);
        overwrite.Content="Replace existing non-formula values after preview";
        overwrite.Foreground=Ink;
        overwrite.Margin=new Thickness(0,3,0,10);
        overwrite.Checked+=(_,_)=>ResetPreview();
        overwrite.Unchecked+=(_,_)=>ResetPreview();
        targetPanel.Children.Add(overwrite);
        var actions=Buttons();
        actions.Children.Add(Action("Preview cell mapping",Preview));
        actions.Children.Add(Action("Autofill a new copy",Apply));
        targetPanel.Children.Add(actions);
        preview.Columns.Add(Column("Cell","Cell",95));
        preview.Columns.Add(Column("Saved value","SavedValue",265));
        preview.Columns.Add(Column("Existing value","ExistingValue",265));
        preview.Columns.Add(Column("Action","Status",140));
        targetPanel.Children.Add(preview);
        targetPanel.Children.Add(new TextBlock{
            Text="Safety: headings must match, formula cells are protected, and saved output "+
                 "is a separate workbook in Downloads. The source and target originals stay unchanged.",
            Foreground=Muted,TextWrapping=TextWrapping.Wrap
        });
        stack.Children.Add(Panel(targetPanel));
        status.Text="Ready. Choose your Excel workbook or use the selected cells in Excel.";
        status.TextWrapping=TextWrapping.Wrap;
        status.FontSize=13;
        status.Foreground=Muted;
        stack.Children.Add(status);
        Guard(()=>LoadProfiles());
        return outer;
    }
}
