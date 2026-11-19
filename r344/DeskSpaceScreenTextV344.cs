using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R329-08: explicit, local-only screen-region OCR via Windows' existing OCR
// engine (no web API, no clipboard listener, no disk screenshot, no Tesseract).
// Text is an UNVERIFIED draft and must be reviewed before manual Copy.
internal static class DeskSpaceScreenTextV344
{
    internal const int MaximumPngBytes=12_000_000;
    internal const long MaximumPixels=24_000_000;

    internal static async Task<string> RecognizeAsync(byte[] png,
        CancellationToken cancellation=default)
    {
        if(png==null || png.Length==0 || png.Length>MaximumPngBytes)
            throw new InvalidDataException("Screen image must be between 1 and 12 MB.");
        cancellation.ThrowIfCancellationRequested();
        using var stream=new InMemoryRandomAccessStream();
        using(var writer=new DataWriter(stream))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        if((long)decoder.PixelWidth*decoder.PixelHeight>MaximumPixels)
            throw new InvalidDataException("Selected region exceeds 24 million pixels.");
        cancellation.ThrowIfCancellationRequested();
        using var bitmap=await decoder.GetSoftwareBitmapAsync();
        var engine=OcrEngine.TryCreateFromUserProfileLanguages()
            ??throw new InvalidOperationException(
                "Windows OCR language unavailable. Install an OCR-compatible Windows language.");
        var result=await engine.RecognizeAsync(bitmap);
        cancellation.ThrowIfCancellationRequested();
        // No screenshot bytes, raw coordinates or OCR output are persisted.
        return string.Join(Environment.NewLine,result.Lines
            .Select(x=>x.Text.Trim()).Where(x=>x.Length>0));
    }

    internal static byte[] CaptureRegion(Drawing.Rectangle rectangle)
    {
        if(rectangle.Width<5||rectangle.Height<5 ||
            rectangle.Width*(long)rectangle.Height>MaximumPixels)
            throw new ArgumentOutOfRangeException(nameof(rectangle),
                "Select a region between 5 pixels and 24 million pixels.");
        // A region outside all displays cannot be silently captured.
        if(!Forms.Screen.AllScreens.Any(screen=>
            screen.Bounds.Contains(rectangle)))
            throw new ArgumentOutOfRangeException(nameof(rectangle),
                "Selection must stay entirely inside one physical monitor.");
        using var bitmap=new Drawing.Bitmap(rectangle.Width,rectangle.Height,
            Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var graphics=Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(rectangle.Location,Drawing.Point.Empty,
                rectangle.Size,Drawing.CopyPixelOperation.SourceCopy);
        using var memory=new MemoryStream();
        bitmap.Save(memory,Drawing.Imaging.ImageFormat.Png);
        if(memory.Length>MaximumPngBytes)
            throw new InvalidDataException("Captured image exceeds 12 MB.");
        return memory.ToArray();
    }

    // Translucent overlay only on the monitor the user explicitly chooses.
    // The host window is not moved. Escape or closing cancels without capture.
    internal static Task<Drawing.Rectangle?> SelectRegionAsync(
        Forms.Screen monitor)
    {
        var signal=new TaskCompletionSource<Drawing.Rectangle?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay=new Window{
            Title="DeskSpace · Select screen text",
            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
            ShowInTaskbar=false,Topmost=true,AllowsTransparency=true,
            Background=new SolidColorBrush(Color.FromArgb(70,0,0,0)),
            Left=monitor.Bounds.Left,Top=monitor.Bounds.Top,
            Width=monitor.Bounds.Width,Height=monitor.Bounds.Height,
            Cursor=Cursors.Cross};
        var canvas=new Canvas{Background=Brushes.Transparent};
        overlay.Content=canvas;
        var outline=new System.Windows.Shapes.Rectangle{
            Stroke=Brushes.DodgerBlue,StrokeThickness=2,
            Fill=new SolidColorBrush(Color.FromArgb(38,65,147,255)),
            Visibility=Visibility.Collapsed};
        canvas.Children.Add(outline);
        var start=new Point();
        bool selecting=false,answered=false;
        void Finish(Drawing.Rectangle? selection)
        {
            if(answered)return;
            answered=true;
            overlay.Close();
            signal.TrySetResult(selection);
        }
        overlay.PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){Finish(null);e.Handled=true;}
        };
        overlay.MouseLeftButtonDown+=(_,e)=>{
            selecting=true;start=e.GetPosition(canvas);
            canvas.CaptureMouse();e.Handled=true;
        };
        overlay.MouseMove+=(_,e)=>{
            if(!selecting)return;
            var p=e.GetPosition(canvas);
            double x=Math.Min(start.X,p.X),y=Math.Min(start.Y,p.Y);
            Canvas.SetLeft(outline,x);Canvas.SetTop(outline,y);
            outline.Width=Math.Abs(start.X-p.X);
            outline.Height=Math.Abs(start.Y-p.Y);
            outline.Visibility=Visibility.Visible;
        };
        overlay.MouseLeftButtonUp+=(_,e)=>{
            if(!selecting)return;
            selecting=false;canvas.ReleaseMouseCapture();
            var p=e.GetPosition(canvas);
            var first=overlay.PointToScreen(start);
            var last=overlay.PointToScreen(p);
            int left=(int)Math.Floor(Math.Min(first.X,last.X));
            int top=(int)Math.Floor(Math.Min(first.Y,last.Y));
            int right=(int)Math.Ceiling(Math.Max(first.X,last.X));
            int bottom=(int)Math.Ceiling(Math.Max(first.Y,last.Y));
            var chosen=Drawing.Rectangle.Intersect(monitor.Bounds,
                Drawing.Rectangle.FromLTRB(left,top,right,bottom));
            Finish(chosen.Width>=5&&chosen.Height>=5?chosen:null);
            e.Handled=true;
        };
        overlay.Closed+=(_,_)=>{
            if(!answered){answered=true;signal.TrySetResult(null);}
        };
        overlay.Show();
        overlay.Activate();
        overlay.Focus();
        return signal.Task;
    }

    internal static Window Create(Window? owner=null)
    {
        var win=new Window{Title="DeskSpace · Screen text extraction",
            Width=720,Height=540,MinWidth=550,MinHeight=390,
            ShowInTaskbar=false,Background=Brushes.White,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null&&owner.IsVisible)win.Owner=owner;
        var panel=new DockPanel{Margin=new Thickness(17)};win.Content=panel;
        var top=new StackPanel();
        top.Children.Add(new TextBlock{Text="Extract screen text",FontSize=21,
            FontWeight=FontWeights.SemiBold});
        top.Children.Add(new TextBlock{
            Text="Choose a monitor, then drag a rectangle. Windows OCR runs offline. Review the unverified text before copying it. No automatic capture or saving.",
            Margin=new Thickness(0,5,0,9),TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray});
        var controls=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,0,0,10)};
        var monitors=new ComboBox{MinWidth=200,Margin=new Thickness(0,0,8,0),
            DisplayMemberPath="DeviceName"};
        monitors.ItemsSource=Forms.Screen.AllScreens;
        monitors.SelectedItem=Forms.Screen.PrimaryScreen;
        var capture=new Button{Content="Select screen region",
            Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,8,0)};
        var copy=new Button{Content="Copy reviewed text",
            Padding=new Thickness(12,6,12,6),IsEnabled=false};
        controls.Children.Add(monitors);controls.Children.Add(capture);
        controls.Children.Add(copy);top.Children.Add(controls);
        DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
        var status=new TextBlock{Text="Ready — no screen captured",
            Foreground=Brushes.DimGray,Margin=new Thickness(0,7,0,0),
            TextWrapping=TextWrapping.Wrap};
        DockPanel.SetDock(status,Dock.Bottom);panel.Children.Add(status);
        var text=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Padding=new Thickness(8),FontSize=13,
            ToolTip="Unverified OCR draft. Correct before copying."};
        text.TextChanged+=(_,_)=>copy.IsEnabled=!string.IsNullOrWhiteSpace(text.Text);
        panel.Children.Add(text);
        CancellationTokenSource? current=null;
        bool busy=false;
        capture.Click+=async (_,_)=>{
            if(busy||monitors.SelectedItem is not Forms.Screen screen)return;
            busy=true;capture.IsEnabled=false;
            current=new CancellationTokenSource();
            try
            {
                text.Clear();status.Text="Select a region; press Esc to cancel.";
                var rectangle=await SelectRegionAsync(screen);
                if(rectangle==null){status.Text="Selection cancelled.";return;}
                // Wait until the translucent selection overlay is fully
                // removed before copying actual screen pixels.
                await Task.Delay(180,current.Token);
                byte[] image=CaptureRegion(rectangle.Value);
                try
                {
                    status.Text="Recognizing text locally with Windows OCR…";
                    text.Text=await RecognizeAsync(image,current.Token);
                }
                finally
                {
                    // OCR may throw or be cancelled: always erase the raw
                    // screenshot buffer, not only after a successful read.
                    Array.Clear(image,0,image.Length);
                }
                status.Text=text.Text.Length>0?
                    "Unverified text ready. Correct it before copying.":
                    "No recognizable text in the selected region.";
            }
            catch(OperationCanceledException){status.Text="Cancelled";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException
                or System.Runtime.InteropServices.COMException)
            {status.Text="OCR unavailable: "+ex.Message;}
            finally{
                busy=false;capture.IsEnabled=true;
                current?.Dispose();current=null;
            }
        };
        copy.Click+=(_,_)=>{
            if(!string.IsNullOrWhiteSpace(text.Text))
            {Clipboard.SetText(text.Text);status.Text="Reviewed text copied by request.";}
        };
        win.Closed+=(_,_)=>{current?.Cancel();text.Clear();};
        return win;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
