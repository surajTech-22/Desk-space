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
using Application=System.Windows.Application;
using Brushes=System.Windows.Media.Brushes;

internal static class Program
{
    private static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R322 Detached Deck regression: "+message);
    }
    private static Rectangle Place(Type dock,Rectangle owner,Rectangle work,
        int width,int height,int requestedShift)
    {
        var f=dock.GetMethod("CanonicalDeckRectV320",Flags)!;
        return (Rectangle)f.Invoke(null,new object[]{owner,work,width,height,requestedShift})!;
    }
    private static int Overlap(Rectangle a,Rectangle b)=>
        Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left));
    private static void Pump(Window window)
    {
        for(int i=0;i<7;i++)
        {
            window.UpdateLayout();
            var frame=new DispatcherFrame();
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);
        }
    }
    [STAThread] static int Main()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Window? owner=null;
        Popup? popup=null;
        try
        {
            var dock=typeof(DeskSpace.WeatherReading).Assembly.GetType(
                "DeskSpace.DeskSpaceMonitorDockV190",true)!;
            var close=workRect(0,0,1664,910);
            var clock=workRect(1520,115,110,110);
            var center=Place(dock,clock,close,158,75,0);
            var staleLeft=Place(dock,clock,close,158,75,-4096);
            var staleRight=Place(dock,clock,close,158,75,4096);
            Check(!center.IsEmpty && !staleLeft.IsEmpty && !staleRight.IsEmpty,
                "Cannot place Deck near clock");
            Check(staleLeft.Left>clock.Left-200,
                $"Screenshot reproduced: Deck detached to screen-left x={staleLeft.Left} with circle x={clock.Left}");
            Check(Overlap(staleLeft,clock)>=Math.Min(80,clock.Width),
                "Old saved offset detached Deck from its circular widget");
            Check(Overlap(staleRight,clock)>=Math.Min(80,clock.Width),
                "Old opposite saved offset detached Deck from its circular widget");
            Check(staleLeft.Top==clock.Bottom+12 &&
                  staleRight.Top==clock.Bottom+12,
                "Deck no longer sits below circular widget");
            Check(close.Contains(staleLeft) && close.Contains(staleRight),
                "Deck escaped screen or crossed monitor edge");
            var screen2=workRect(-1920,0,1920,1080);
            var clock2=workRect(-1880,105,112,112);
            var bad2=Place(dock,clock2,screen2,220,85,4096);
            Check(screen2.Contains(bad2) &&
                  Overlap(bad2,clock2)>=80,
                "Second monitor's saved offset detached its Deck");

            var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            owner=new Window{
                Width=112,Height=112,Left=work.Right-170,Top=work.Top+70,
                WindowStyle=WindowStyle.None,ShowInTaskbar=false,
                AllowsTransparency=true,Background=Brushes.Transparent,
                Content=new Border{Width=110,Height=110,Background=Brushes.SlateBlue}
            };
            owner.Show();Pump(owner);
            var hwnd=new WindowInteropHelper(owner).Handle;
            Check(GetWindowRect(hwnd,out var actual),
                "Cannot read physical clock HWND");
            var ownerRect=workRect(actual.Left,actual.Top,
                actual.Right-actual.Left,actual.Bottom-actual.Top);
            var unsafeProposal=workRect(work.Left+10,ownerRect.Bottom+12,185,82);
            var beforePaint=dock.GetMethod("ConstrainBeforePaintV298",Flags)!;
            var constrained=(Rectangle)beforePaint.Invoke(null,
                new object[]{owner,unsafeProposal,work})!;
            Check(Overlap(constrained,ownerRect)>=Math.Min(80,ownerRect.Width),
                "First native frame still permits Deck at a second display point");
            Check(constrained.Left>ownerRect.Left-250,
                "Native monitor fence is keeping a detached screen-edge position");

            var items=new StackPanel{Margin=new Thickness(9,8,9,8)};
            items.Children.Add(new TextBlock{Text="Deck · 4 apps",Height=26});
            items.Children.Add(new TextBlock{Text="Running Apps · 4",Height=28});
            var panel=new Border{
                MinWidth=180,MaxWidth=310,
                Child=new ScrollViewer{Content=items},
                Background=Brushes.DarkSlateBlue
            };
            popup=new Popup{PlacementTarget=owner,Child=panel,
                Placement=PlacementMode.Custom,
                StaysOpen=true,AllowsTransparency=true};
            dock.GetMethod("RegisterMinimizedV314",Flags)!.Invoke(null,
                new object[]{panel,new Func<bool>(()=>false)});
            dock.GetMethod("Attach",Flags)!.Invoke(null,
                new object[]{owner,popup,panel});
            popup.IsOpen=true;Pump(owner);
            Check(panel.ActualHeight>30,"Real Deck popup not visible");
            if(PresentationSource.FromVisual(panel) is HwndSource src &&
               GetWindowRect(src.Handle,out var deckBounds))
            {
                var painted=workRect(deckBounds.Left,deckBounds.Top,
                    deckBounds.Right-deckBounds.Left,deckBounds.Bottom-deckBounds.Top);
                Check(Overlap(painted,ownerRect)>=Math.Min(80,ownerRect.Width),
                    "Real native popup was drawn detached from its clock");
            }
            Console.WriteLine("R322 PASS: saved +/-4096px offset, first-paint fence, actual right-edge WPF popup, and independent negative-coordinate monitor remain tethered to their circular widget.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(popup!=null)popup.IsOpen=false;owner?.Close();app.Shutdown();}
    }
    private static Rectangle workRect(int x,int y,int w,int h)=>
        new Rectangle(x,y,w,h);
}
