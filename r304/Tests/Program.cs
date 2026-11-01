using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

internal static class Program
{
    private static readonly Assembly Product=typeof(DeskSpace.WeatherReading).Assembly;
    private static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect native);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }

    private static void Assert(bool yes,string message)
    {if(!yes)throw new InvalidOperationException("R304: "+message);}
    private static void Pump(Window window)
    {
        for(int i=0;i<5;i++)
        {
            window.UpdateLayout();
            var frame=new DispatcherFrame();
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);
        }
    }
    [STAThread]
    private static int Main()
    {
        var app=new System.Windows.Application{
            ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Window? host=null;
        Popup? popup=null;
        Window? foreground=null;
        try
        {
            var work=Forms.Screen.PrimaryScreen?.WorkingArea ??
                Forms.Screen.AllScreens[0].WorkingArea;
            Assert(work.Width>700 && work.Height>500,
                "Windows runner screen too small for native test");
            host=new Window {
                Width=116,Height=116,
                Left=work.Left+Math.Min(130,work.Width-130),
                Top=work.Bottom-150,
                WindowStyle=WindowStyle.None,
                ShowInTaskbar=false,AllowsTransparency=true,
                Background=System.Windows.Media.Brushes.Transparent,
                Topmost=true,ShowActivated=false,
                Content=new Border{
                    Width=110,Height=110,
                    Background=System.Windows.Media.Brushes.DarkSlateBlue,
                    CornerRadius=new CornerRadius(55)
                }
            };
            host.Show();Pump(host);
            var list=new StackPanel();
            list.Children.Add(new TextBlock{Text="Deck · 4 apps",
                Foreground=System.Windows.Media.Brushes.White,Height=28});
            for(int i=0;i<14;i++)
                list.Children.Add(new TextBlock{
                    Text="Running App "+i,Height=26,
                    Foreground=System.Windows.Media.Brushes.White
                });
            var scroll=new ScrollViewer{Content=list,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                MaxHeight=295};
            var panel=new Border{
                Background=System.Windows.Media.Brushes.DarkSlateBlue,
                MinWidth=235,MaxWidth=310,
                BorderThickness=new Thickness(1),
                Child=scroll
            };
            popup=new Popup{Placement=PlacementMode.Custom,
                PlacementTarget=host,Child=panel,
                AllowsTransparency=true,StaysOpen=true};
            var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
            dock.GetMethod("Attach",Flags)!.Invoke(null,
                new object?[]{host,popup,panel});
            var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV153",true)!;
            var constrain=dock.GetMethod("ConstrainBeforePaintV298",Flags)!;
            Func<Rectangle,Rectangle,Rectangle> policy=(candidate,screen)=>
                (Rectangle)constrain.Invoke(null,
                    new object[]{host,candidate,screen})!;
            fence.GetMethod("TrackPopup",Flags)!.Invoke(null,
                new object?[]{host,popup,policy});
            var deck=Product.GetType("DeskSpace.DeskSpaceDeckVisibilityV304",true)!;
            deck.GetMethod("Attach",Flags)!.Invoke(null,
                new object?[]{host,popup,panel});
            popup.IsOpen=true;Pump(host);
            var native=(PresentationSource.FromVisual(panel) as HwndSource)?.Handle
                ??IntPtr.Zero;
            Assert(native!=IntPtr.Zero,"live Deck native popup HWND missing");
            Assert(popup.PopupAnimation==PopupAnimation.None,
                "Deck should not fade/clip during popup positioning");
            Assert(((uint)GetWindowLong(native,-20)&0x8)!=0,
                "Deck popup must be TOPMOST like the owning circle");
            var bars=scroll.Resources[typeof(ScrollBar)] as Style;
            Assert(bars!=null,"Deck should have custom dark scrollbar style");
            Assert(scroll.CanContentScroll==false &&
                   scroll.PanningMode==PanningMode.VerticalOnly,
                "Deck two-finger touchpad scrolling must remain enabled");
            Assert(GetWindowRect(native,out var popupBounds),
                "Cannot read real Deck position");
            var ownerBounds=fence.GetMethod("WindowBounds",Flags)!
                .Invoke(null,new object[]{host}) as Rectangle?;
            Assert(ownerBounds.HasValue,"Cannot read clock native coordinates");
            Assert(popupBounds.Top>=ownerBounds.Value.Bottom+10,
                "Deck still overlaps topmost clock");
            Assert(popupBounds.Left>=work.Left && popupBounds.Right<=work.Right,
                "Deck is outside monitor horizontal bounds");
            Assert(popupBounds.Bottom<=work.Bottom,
                "Deck is hidden below the monitor working area");
            foreground=new Window{
                Title="Simulated Chrome",Width=330,Height=200,
                WindowStyle=WindowStyle.SingleBorderWindow,
                Topmost=false,
                Left=work.Left+300,Top=work.Top+200};
            foreground.Show();foreground.Activate();Pump(host);
            Assert(((uint)GetWindowLong(native,-20)&0x8)!=0,
                "Foreground Chrome-like window demoted Deck behind it");
            Console.WriteLine("R304 PASS: full-height Deck stays on correct monitor, above non-topmost Chrome, below clock, and has touchpad-compatible dark scrollbar.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            if(popup!=null)popup.IsOpen=false;
            foreground?.Close();host?.Close();app.Shutdown();
        }
    }
}