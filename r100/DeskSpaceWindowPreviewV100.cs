using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeskSpace;

// Preview a real app WINDOW only after the mouse has been stationary.
// We never screenshot the desktop, activate the target, or save captures.
// PrintWindow is best-effort: accelerated/protected windows can refuse
// screenshots, in which case a truthful title/metadata card appears.
internal sealed class DeskSpaceWindowPreviewV100 : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out WinRect rect);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
    [DllImport("gdi32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr bitmap);

    internal static bool Stationary(Point before,Point after,double tolerance=2)=>
        Math.Abs(before.X-after.X)<=tolerance &&
        Math.Abs(before.Y-after.Y)<=tolerance;

    private readonly Popup popup;
    private readonly DispatcherTimer delay=new(){
        Interval=TimeSpan.FromMilliseconds(650)
    };
    private FrameworkElement? pending;
    private FrameworkElement? pendingPanel;
    private DeskSpaceRunningAppsV88.Entry? target;
    private Point position;
    private long generation;
    private bool disposed;

    internal bool IsOpen=>popup.IsOpen;
    internal DeskSpaceWindowPreviewV100()
    {
        popup=new Popup{
            Placement=PlacementMode.Right,HorizontalOffset=5,
            AllowsTransparency=true,StaysOpen=true,
            PopupAnimation=PopupAnimation.None
        };
        delay.Tick+=(_,_)=>{
            delay.Stop();
            var item=pending;
            var app=target;
            var panel=pendingPanel;
            if(disposed||item==null||app==null||panel==null||
                !item.IsMouseOver || !DeskSpaceRunningAppsV88.IsActiveWindow(app.Handle))
                return;
            long version=++generation;
            var card=CreateCard(app,null);
            var work=SystemParameters.WorkArea;
            try
            {
                var left=panel.PointToScreen(new Point(0,0));
                var right=panel.PointToScreen(new Point(panel.ActualWidth,0));
                bool showRight=work.Right-right.X>=318 ||
                    work.Right-right.X >= left.X-work.Left;
                popup.Placement=showRight?PlacementMode.Right:PlacementMode.Left;
                popup.HorizontalOffset=showRight?5:-5;
                popup.PlacementTarget=panel;
                popup.VerticalOffset=Math.Max(0,item.TranslatePoint(new Point(0,0),panel).Y);
            }
            catch(InvalidOperationException){popup.PlacementTarget=panel;}
            popup.Child=card;
            popup.IsOpen=true;
            // Only capture after the actual 650-ms stationary hover.
            // Never block the WPF hover/menu thread on another process.
            _=Task.Run(()=>Capture(app.Handle)).ContinueWith(task=>{
                if(disposed||version!=generation||!popup.IsOpen||
                   !ReferenceEquals(target,app))return;
                if(task.Status==TaskStatus.RanToCompletion &&
                   task.Result is ImageSource preview &&
                   card.Child is StackPanel column)
                    column.Children.Insert(1,new Image{
                        Source=preview,MaxWidth=285,MaxHeight=190,
                        Stretch=Stretch.Uniform,
                        Margin=new Thickness(0,7,0,6)
                    });
            },System.Threading.CancellationToken.None,
                TaskContinuationOptions.None,TaskScheduler.FromCurrentSynchronizationContext());
        };
    }

    private static Border CreateCard(DeskSpaceRunningAppsV88.Entry app,
        ImageSource? screenshot)
    {
        var stack=new StackPanel{Margin=new Thickness(11),MaxWidth=300};
        stack.Children.Add(new TextBlock{
            Text=app.Name,FontSize=12,Foreground=Brushes.White,
            FontWeight=FontWeights.SemiBold
        });
        if(screenshot!=null)stack.Children.Add(new Image{
            Source=screenshot,MaxWidth=285,MaxHeight=190,
            Stretch=Stretch.Uniform,Margin=new Thickness(0,7,0,6)
        });
        stack.Children.Add(new TextBlock{
            Text=app.WindowTitle,FontSize=11,MaxWidth=275,
            Foreground=Brushes.LightSteelBlue,
            TextWrapping=TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock{
            Text="Window preview · move mouse to dismiss",
            Margin=new Thickness(0,6,0,0),FontSize=9,
            Foreground=Brushes.LightSteelBlue
        });
        return new Border{
            Child=stack,Background=new SolidColorBrush(Color.FromRgb(25,34,55)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(79,98,145)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),
            IsHitTestVisible=false,MaxWidth=315
        };
    }

    private static BitmapSource? Capture(IntPtr hwnd)
    {
        try
        {
            if(!GetWindowRect(hwnd,out var rect))return null;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
            if(width<50||height<50||width>2500||height>1800)return null;
            using var bitmap=new Bitmap(width,height);
            using(var gfx=Graphics.FromImage(bitmap))
            {
                IntPtr hdc=gfx.GetHdc();
                bool ok;
                try{ok=PrintWindow(hwnd,hdc,2);}
                finally{gfx.ReleaseHdc(hdc);}
                if(!ok)return null;
            }
            IntPtr hbitmap=bitmap.GetHbitmap();
            try
            {
                var source=Imaging.CreateBitmapSourceFromHBitmap(hbitmap,
                    IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally{DeleteObject(hbitmap);}
        }
        catch(Exception ex) when(ex is Win32Exception or
            ExternalException or ArgumentException or OutOfMemoryException)
        {return null;}
    }

    internal void Bind(FrameworkElement tile,FrameworkElement panel,
        Func<DeskSpaceRunningAppsV88.Entry?> resolve)
    {
        tile.MouseEnter+=(_,e)=>Start(tile,panel,resolve,e.GetPosition(tile));
        tile.MouseMove+=(_,e)=>{
            Point now=e.GetPosition(tile);
            if(ReferenceEquals(pending,tile) && !Stationary(position,now))
            {
                Close();
                Start(tile,panel,resolve,now);
            }
        };
        tile.MouseLeave+=(_,_)=>Close();
        tile.Unloaded+=(_,_)=>{
            if(ReferenceEquals(pending,tile))Close();
        };
    }

    private void Start(FrameworkElement tile,FrameworkElement panel,
        Func<DeskSpaceRunningAppsV88.Entry?> resolve,Point at)
    {
        if(disposed)return;
        var window=resolve();
        if(window==null || !DeskSpaceRunningAppsV88.IsActiveWindow(window.Handle))
            return;
        pending=tile;pendingPanel=panel;target=window;position=at;
        delay.Stop();delay.Start();
    }

    internal void Close()
    {
        delay.Stop();
        ++generation;
        popup.IsOpen=false;
        popup.Child=null;
        pending=null;pendingPanel=null;target=null;
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        Close();
    }
}
