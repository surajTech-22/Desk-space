using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Reuses the existing V85 content preview. Anchored to the DECK's persistent
// Border, not a recreated file row: refreshing Recent Files never resizes
// the deck or destroys a currently open preview popup.
internal sealed class DeskSpaceDeckPreviewV99 : IDisposable
{
    private readonly Popup preview;
    private readonly FrameworkElement deck;
    private readonly DispatcherTimer hoverOpen=new(){
        Interval=TimeSpan.FromMilliseconds(650)
    };
    private readonly DispatcherTimer hoverClose=new(){
        Interval=TimeSpan.FromMilliseconds(500)
    };
    private bool disposed;
    private string current="";
    private Border? pendingRow;
    private string pendingPath="";
    private Func<bool>? pendingMode;
    private Point pendingPosition;
    private bool manualPreview;
    internal static bool Stationary(Point previous,Point current,double tolerance=2)=>
        Math.Abs(previous.X-current.X)<=tolerance &&
        Math.Abs(previous.Y-current.Y)<=tolerance;
    internal bool IsOpen=>preview.IsOpen;
    internal string CurrentPath=>current;
    internal DeskSpaceDeckPreviewV99(FrameworkElement deck)
    {
        this.deck=deck;
        preview=new Popup{
            PlacementTarget=deck,Placement=PlacementMode.Right,
            HorizontalOffset=5,StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.Fade
        };
        hoverOpen.Tick+=(_,_)=>{
            hoverOpen.Stop();
            var row=pendingRow;
            if(row!=null && row.IsLoaded && row.IsMouseOver &&
               pendingMode?.Invoke()==true &&
               Stationary(pendingPosition,Mouse.GetPosition(row)))
                Show(pendingPath,row);
        };
        hoverClose.Tick+=(_,_)=>{
            if(preview.Child is FrameworkElement p && p.IsMouseOver)
            {
                hoverClose.Stop();
                return;
            }
            Close();
        };
    }

    internal void Close()
    {
        hoverOpen.Stop();
        pendingRow=null;
        pendingMode=null;
        manualPreview=false;
        hoverClose.Stop();
        preview.IsOpen=false;
        preview.Child=null;
        current="";
    }

    internal void CloseIfMissing()
    {
        if(current.Length>0 && !File.Exists(current) && !Directory.Exists(current))
            Close();
    }

    internal void Show(string path,FrameworkElement source,
        bool explicitClick=false)
    {
        if(disposed)return;
        if(!File.Exists(path)&&!Directory.Exists(path)){Close();return;}
        hoverOpen.Stop();
        hoverClose.Stop();
        if(preview.IsOpen && string.Equals(current,path,
            StringComparison.OrdinalIgnoreCase))return;
        Close();
        current=path;
        manualPreview=explicitClick;
        try
        {
            var content=Directory.Exists(path)?
                CreateFolderPreview(path):DeskSpaceFilePreviewV85.Create(path);
            content.MouseEnter+=(_,_)=>hoverClose.Stop();
            content.MouseLeave+=(_,_)=>ScheduleClose();
            preview.Child=content;
            // Anchor stays attached to the main deck. Only the vertical
            // position changes to correspond to the hovered file.
            try
            {
                var offset=source.TranslatePoint(new Point(0,0),deck).Y;
                preview.VerticalOffset=Math.Max(0,offset);
            }
            catch(InvalidOperationException){preview.VerticalOffset=0;}
            preview.IsOpen=true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine("DeskSpace preview unavailable: "+ex.Message);
            Close();
        }
    }

    private void ScheduleClose()
    {
        hoverClose.Stop();
        hoverClose.Start();
    }

    internal void Bind(Button button,Border row,string path,Func<bool> hoverEnabled)
    {
        button.Click+=(_,e)=>{
            e.Handled=true;
            if(preview.IsOpen && string.Equals(current,path,
                StringComparison.OrdinalIgnoreCase))
                Close();
            else Show(path,row,true);
        };
        row.MouseEnter+=(_,_)=>{
            hoverClose.Stop();
            if(!hoverEnabled())return;
            hoverOpen.Stop();
            pendingRow=row;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=Mouse.GetPosition(row);
            hoverOpen.Start();
        };
        row.MouseMove+=(_,e)=>{
            var at=e.GetPosition(row);
            if(Stationary(pendingPosition,at))return;
            hoverOpen.Stop();
            // Automatically shown previews vanish on movement. Explicit
            // eye-button previews stay interactive until dismissed.
            if(preview.IsOpen && !manualPreview)Close();
            if(preview.IsOpen && manualPreview)return;
            if(!hoverEnabled())return;
            pendingRow=row;
            pendingPath=path;
            pendingMode=hoverEnabled;
            pendingPosition=at;
            hoverOpen.Start();
        };
        row.MouseLeave+=(_,_)=>{
            hoverOpen.Stop();
            if(preview.IsOpen && !manualPreview)Close();
            if(preview.IsOpen)ScheduleClose();
        };
        row.Unloaded+=(_,_)=>{
            if(ReferenceEquals(pendingRow,row))
            {
                hoverOpen.Stop();
                pendingRow=null;
                pendingMode=null;
            }
        };
    }

    private static FrameworkElement CreateFolderPreview(string path)
    {
        var content=new StackPanel{Margin=new Thickness(12)};
        content.Children.Add(new TextBlock{
            Text="Folder preview · "+Path.GetFileName(path),
            FontSize=13,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,
            MaxWidth=270
        });
        content.Children.Add(new TextBlock{
            Text="You can open this folder or reveal its location. "+
                "Its contents are not scanned in the background.",
            MaxWidth=270,Margin=new Thickness(0,7,0,8),
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.LightSteelBlue
        });
        var open=new Button{Content="Open folder",
            HorizontalAlignment=HorizontalAlignment.Right,
            Padding=new Thickness(8,5,8,5)};
        open.Click+=(_,_)=>{
            try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
            catch(Exception ex) when(ex is IOException or
                System.ComponentModel.Win32Exception or
                InvalidOperationException)
            {MessageBox.Show(ex.Message,"DeskSpace preview");}
        };
        content.Children.Add(open);
        return new Border{
            Child=content,Background=new SolidColorBrush(Color.FromRgb(24,32,52)),
            BorderBrush=Brushes.SlateBlue,
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(12)
        };
    }

    internal static void OpenContainingFolder(string path)
    {
        try
        {
            if(!File.Exists(path)&&!Directory.Exists(path))return;
            var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};
            info.ArgumentList.Add("/select,"+path);
            Process.Start(info);
        }
        catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception
            or InvalidOperationException or UnauthorizedAccessException)
        {MessageBox.Show("Could not locate file: "+ex.Message,"DeskSpace");}
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        Close();
    }
}
