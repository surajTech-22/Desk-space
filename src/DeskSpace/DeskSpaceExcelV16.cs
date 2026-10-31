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
internal sealed partial class DeskSpaceExcelV16 : IDisposable
{
    private readonly Action back;
    private readonly bool inWorkspace;
    private readonly List<DeskSpaceExcelTemplateV16> saved;
    private readonly Brush ink=new SolidColorBrush(Color.FromRgb(35,43,70));
    private readonly Brush muted=new SolidColorBrush(Color.FromRgb(99,108,132));
    private ListBox templates=null!,previewRows=null!,selectedCells=null!;
    private TextBlock cellSelectionInfo=null!;
    private DeskSpaceExcelTemplateV16? pendingSelection;
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

    public DeskSpaceExcelV16(Action exit):this(exit,false) {}
    public DeskSpaceExcelV16(Action exit,bool inMainWorkspace)
    {
        back=exit;
        inWorkspace=inMainWorkspace;
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
    private async void OpenScreenshot()
    {
        try
        {
            var dialog=new OpenFileDialog{
                Filter="Screenshots|*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists=true,Title="Choose an Excel table screenshot"
            };
            if(dialog.ShowDialog()!=true)return;
            var bytes=File.ReadAllBytes(dialog.FileName);
            if(bytes.Length>12000000)
                throw new InvalidOperationException(
                    "Use a screenshot no larger than 12 MB.");
            ShowScreenshot(bytes,"");
            status.Text="Screenshot loaded. Detecting the visible table and text…";
            await AnalyzeScreenshotAsync();
        }
        catch(Exception ex){status.Text="Screenshot: "+ex.Message;}
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
        DrawMove(sender,e);marking=false;drawing.ReleaseMouseCapture();
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
    private static string CellReference(int row,int column)
    {
        var letters="";
        while(column>0)
        {
            var digit=(column-1)%26;
            letters=(char)('A'+digit)+letters;
            column=(column-1)/26;
        }
        return letters+row.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    private void ToggleCaptured(bool value)
    {
        if(selectedCells==null)return;
        foreach(var checkbox in selectedCells.Items.OfType<CheckBox>())
            checkbox.IsChecked=value;
        if(pendingSelection!=null)
            cellSelectionInfo.Text=selectedCells.Items.OfType<CheckBox>()
                .Count(x=>x.IsChecked==true)+" of "+
                pendingSelection.Cells.Count+" captured cells selected.";
    }
    private async void Capture()
    {
        try
        {
            status.Text="Reading selected cells from the already open Excel workbook…";
            var candidate=await DeskSpaceExcelWorkerV41.CaptureAsync(
                name.Text,range.Text,formulas.IsChecked==true);
            if(screenshotBytes!=null)
            {
                candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
                candidate.ScreenshotSelection=Region();
            }
            await PresentCandidateAsync(candidate);
        ShowCapturedV42(candidate);
            status.Text="Captured "+candidate.Cells.Count+" cells at "+
                candidate.SourceWorkbook+" / "+candidate.SourceSheet+" / "+
                candidate.SourceAddress+". Review headers and values, then Save.";
        }
        catch(Exception ex)
        {
            status.Text="Excel selection unavailable: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel selection",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

    private void AnalyzeScreenshot()
    {
        _=AnalyzeScreenshotAsync();
    }
    private async System.Threading.Tasks.Task AnalyzeScreenshotAsync()
    {
        try
        {
            if(screenshotBytes==null)
                throw new InvalidOperationException("Choose a screenshot first.");
            status.Text="Reading visible screenshot text using Windows OCR…";
            var candidate=await DeskSpaceExcelScreenshotV39.AnalyzeAsync(
                screenshotBytes,name.Text,Region());
            if(candidate.Cells.Count==0)
                throw new InvalidOperationException(
                    "No readable text found in the marked screenshot area.");
            await PresentCandidateAsync(candidate);
        ShowCapturedV42(candidate);
            status.Text="Recognized "+candidate.Cells.Count+
                " source text lines. Cell addresses are NOT verified from an image. "+
                "Review the raw text and roles; link an Excel selection to verify cell destinations.";
        }
        catch(Exception ex){status.Text="Screenshot analysis: "+ex.Message;}
    }
    private async void LinkScreenshotToExcel()
    {
        if(screenshotBytes==null)
        {
            status.Text="Upload a screenshot before linking it to Excel.";
            return;
        }
        try
        {
            status.Text="Verifying against the selected live Excel range…";
            var candidate=await DeskSpaceExcelWorkerV41.CaptureAsync(
                name.Text,range.Text,formulas.IsChecked==true);
            candidate.SourceKind="Screenshot + live Excel";
            candidate.CoordinatesVerified=true;
            candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
            candidate.ScreenshotSelection=Region();
            await PresentCandidateAsync(candidate);
        ShowCapturedV42(candidate);
            status.Text="Verified live workbook "+candidate.SourceWorkbook+
                ", sheet "+candidate.SourceSheet+", cells "+
                candidate.SourceAddress+".";
        }
        catch(Exception ex)
        {
            status.Text="Unable to link screenshot: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel verification",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private async System.Threading.Tasks.Task PresentCandidateAsync(DeskSpaceExcelTemplateV16 candidate)
    {
        pendingSelection=candidate;
        selectedCells.Items.Clear();
        int rendered=0;
        foreach(var cell in candidate.Cells)
        {
            var actual=candidate.CoordinatesVerified ?
                (string.IsNullOrEmpty(cell.CellAddress)
                  ?CellReference(candidate.SourceRow+cell.RowOffset,
                        candidate.SourceColumn+cell.ColumnOffset):cell.CellAddress)
                : "Image row "+(cell.RowOffset+1)+", item "+(cell.ColumnOffset+1);
            var checkbox=new CheckBox{
                Tag=cell,IsChecked=cell.ValueKind!="formula" && cell.ValueKind!="blank",
                FontSize=12,Foreground=ink,Margin=new Thickness(4,3,4,4),
                ToolTip="Select to save exactly this text and its classification"
            };
            var contents=new StackPanel{Margin=new Thickness(1)};
            contents.Children.Add(new TextBlock{
                Text=actual+"  ·  "+cell.Display+
                    (cell.ValueKind=="formula"?"  [formula: "+cell.FormulaR1C1+"]":""),
                FontSize=12,Foreground=ink,TextWrapping=TextWrapping.Wrap
            });
            var editor=new WrapPanel{Margin=new Thickness(0,3,0,0)};
            var role=new ComboBox{Width=110,Margin=new Thickness(0,1,5,1),
                ToolTip="Reference Cell = header; Value Cell = input"};
            foreach(string option in new[]{"Reference","Value","Formula","Ignore"})
                role.Items.Add(option);
            role.SelectedItem=cell.Role;
            role.SelectionChanged+=(_,_)=>{
                cell.Role=role.SelectedItem?.ToString()??"Ignore";
                cell.FixedOrVariable=cell.Role=="Reference"?"Fixed label":
                    cell.Role=="Formula"?"Calculated":"Variable input (review)";
            };
            editor.Children.Add(role);
            var category=new ComboBox{Width=135,Margin=new Thickness(0,1,5,1),
                ToolTip="Fixed labels / variable inputs / calculated fields"};
            foreach(string option in new[]{"Fixed label","Variable input (review)",
                "Calculated","Review"})category.Items.Add(option);
            category.SelectedItem=cell.FixedOrVariable;
            category.SelectionChanged+=(_,_)=>{
                cell.FixedOrVariable=category.SelectedItem?.ToString()??"Review";
            };
            editor.Children.Add(category);
            contents.Children.Add(editor);
            if(cell.ValueKind!="formula")
            {
                var actualText=new TextBox{
                    Text=cell.RawValue,Margin=new Thickness(0,2,2,1),
                    MinWidth=160,MaxWidth=440,
                    ToolTip="Edit transcription only if the source text is wrong"};
                actualText.TextChanged+=(_,_)=>{
                    cell.RawValue=actualText.Text;
                    cell.Display=actualText.Text;
                };
                contents.Children.Add(actualText);
            }
            checkbox.Content=contents;
            checkbox.Checked+=(_,_)=>UpdateSelectionCount();
            checkbox.Unchecked+=(_,_)=>UpdateSelectionCount();
            selectedCells.Items.Add(checkbox);
            // Let wheel, click, typing and window painting run between
            // small batches, rather than freezing on a large Excel range.
            if(++rendered%32==0)
                await System.Windows.Threading.Dispatcher.Yield(
                    System.Windows.Threading.DispatcherPriority.Background);
        }
        UpdateSelectionCount();
        status.Text="Analyzed "+candidate.Cells.Count+" source entries, "+
            candidate.RowCount+" rows × "+candidate.ColumnCount+" columns. "+
            (candidate.CoordinatesVerified?"Exact Excel cell positions verified.":
            "Screenshot OCR coordinates only; destination addresses unverified.")+
            " Choose Reference, Value, Formula or Ignore before saving.";
    }
    private void UpdateSelectionCount()
    {
        if(pendingSelection==null||cellSelectionInfo==null)return;
        cellSelectionInfo.Text=selectedCells.Items.OfType<CheckBox>()
            .Count(x=>x.IsChecked==true)+" of "+
            pendingSelection.Cells.Count+" entries selected for saving.";
    }
    private void SaveCheckedCells()
    {
        if(pendingSelection!=null &&
           selectedCells.Items.Count<pendingSelection.Cells.Count)
            throw new InvalidOperationException(
                "Excel range preview is still rendering. Complete the review before saving.");
        if(pendingSelection==null)
            throw new InvalidOperationException(
                "Analyze live Excel cells or a screenshot before saving.");
        var chosen=selectedCells.Items.OfType<CheckBox>()
            .Where(x=>x.IsChecked==true)
            .Select(x=>x.Tag).OfType<DeskSpaceExcelCellV16>()
            .Where(c=>c.Role!="Ignore").ToList();
        if(chosen.Count==0)
            throw new InvalidOperationException("Select at least one data entry.");
        pendingSelection.Cells=chosen;
        var existing=pendingSelection.Anchors.Select(a=>
            a.RowOffset+":"+a.ColumnOffset).ToHashSet();
        foreach(var cell in chosen.Where(x=>x.Role=="Reference"))
        {
            if(string.IsNullOrWhiteSpace(cell.RawValue))continue;
            if(!existing.Add(cell.RowOffset+":"+cell.ColumnOffset))continue;
            pendingSelection.Anchors.Add(new DeskSpaceExcelAnchorV16{
                RowOffset=cell.RowOffset,ColumnOffset=cell.ColumnOffset,
                Label=cell.RawValue
            });
        }
        foreach(var cell in chosen.Where(x=>x.Role=="Value"))
        {
            var header=chosen.Where(c=>c.Role=="Reference" &&
                ((c.ColumnOffset==cell.ColumnOffset && c.RowOffset<cell.RowOffset)||
                 (c.RowOffset==cell.RowOffset && c.ColumnOffset<cell.ColumnOffset)))
                .OrderBy(c=>Math.Abs(c.RowOffset-cell.RowOffset)+
                    Math.Abs(c.ColumnOffset-cell.ColumnOffset)).FirstOrDefault();
            if(header!=null)cell.HeaderCellAddress=header.CellAddress;
        }
        saved.Add(pendingSelection);
        DeskSpaceExcelStoreV16.Save(saved);
        var added=pendingSelection;
        pendingSelection=null;
        selectedCells.Items.Clear();
        cellSelectionInfo.Text="Saved. Capture another selection to add information.";
        Refresh(added.Id);
        status.Text="Saved "+chosen.Count+" approved entries. "+
            (added.CoordinatesVerified?
                "Exact Excel destination addresses preserved.":
                "Screenshot text preserved, but addresses remain unverified; autofill is blocked until linked.")+
            " Existing templates were kept.";
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
    private async void Preview()
    {
        try
        {
        var entry=Selected;
        status.Text="Comparing workbook labels and destination formulas…";
        latest=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
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
        catch(Exception ex)
        {
            status.Text="Excel preview unavailable: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel Preview",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private async void Apply()
    {
        try
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
        status.Text="Creating workbook backup and pasting values into verified cells…";
        var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
            latest,entry,overwrite.IsChecked==true);
        status.Text="Autofill sent to Excel. Backup: "+backup+
            ". Review and save your workbook in Excel.";
        latest=null;
        }
        catch(Exception ex)
        {
            status.Text="Excel autofill stopped: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel autofill",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    public void Dispose(){disposed=true;screenshotBytes=null;}
}
