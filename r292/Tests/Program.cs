using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

internal static class Program
{
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr insertAfter,
        int x,int y,int width,int height,uint flags);
    private const uint MoveOnly=0x0001|0x0004|0x0010;

    private static void Assert(bool pass,string reason)
    { if(!pass)throw new Exception("R292 FAIL: "+reason); }
    private static readonly Assembly Product=typeof(DeskSpace.WeatherReading).Assembly;
    private static readonly Type Dock=Product.GetType(
        "DeskSpace.DeskSpaceMonitorDockV190",true)!;
    private static readonly Type Fence=Product.GetType(
        "DeskSpace.DeskSpaceMonitorFenceV153",true)!;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static object Call(string method,params object[] args)=>
        Dock.GetMethod(method,Static)!.Invoke(null,args)!;
    private static Rectangle Bounds(string name,object argument)=>
        (Rectangle?)Fence.GetMethod(name,Static)!.Invoke(null,new[]{argument})
        ??throw new Exception("HWND unavailable for "+name);
    private static void Pump(Window owner)
    {
        // Drain WPF Loaded, Render and deferred popup native positioning.
        for(int i=0;i<4;i++)
        {
            owner.UpdateLayout();
            var frame=new DispatcherFrame();
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);
        }
    }
    private static void CheckPlan(Rectangle work,int widgetX,int widgetY)
    {
        var widget=new Rectangle(widgetX,widgetY,124,124);
        var plan=Call("FitBelowV292",widget,work,235,268,12);
        var type=plan.GetType();
        var owner=(Rectangle)type.GetProperty("Widget")!.GetValue(plan)!;
        var deck=(Rectangle)type.GetProperty("Deck")!.GetValue(plan)!;
        Assert((bool)type.GetProperty("Fits")!.GetValue(plan)!,
            "desktop-size monitor must support a below-only Deck");
        Assert(work.Contains(deck) && deck.Top>=owner.Bottom+12 &&
            !deck.IntersectsWith(owner),"Deck must not intersect its clock");
        var repaired=(Rectangle)Call("NativeCorrectionV292",
            new Rectangle(widget.Left,widget.Top,235,268),owner,work,12);
        Assert(repaired.Top>=owner.Bottom+12 && work.Contains(repaired),
            "native post-paint correction must keep the Deck below its widget");
    }
    [STAThread]
    private static int Main()
    {
        var app=new System.Windows.Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        Window? owner=null;
        Popup? popup=null;
        try
        {
            CheckPlan(new Rectangle(0,0,1920,1080),120,900);
            CheckPlan(new Rectangle(-1920,-400,1920,1080),-1850,570);
            CheckPlan(new Rectangle(1920,0,1280,720),2000,590);
            CheckPlan(new Rectangle(0,0,540,340),80,220);
            var impossible=Call("FitBelowV292",
                new Rectangle(0,0,180,180),new Rectangle(0,0,250,190),
                230,180,12);
            Assert(!(bool)impossible.GetType().GetProperty("Fits")!
                .GetValue(impossible)!,"impossible tiny display must fail closed");
            var screen=Forms.Screen.PrimaryScreen?.WorkingArea ??
                Forms.Screen.AllScreens[0].WorkingArea;
            if(screen.Height<400)throw new Exception("Runner work area too small");
            owner=new Window
            {
                Width=124,Height=124,
                Left=screen.Left+Math.Min(160,screen.Width-130),
                Top=screen.Bottom-140,
                ShowInTaskbar=false,WindowStyle=WindowStyle.None,
                ResizeMode=ResizeMode.NoResize,
                Content=new Border {
                    Width=110,Height=110,
                    Background=System.Windows.Media.Brushes.DodgerBlue,
                    CornerRadius=new CornerRadius(55)
                }
            };
            owner.Show();
            Pump(owner);
            var stack=new StackPanel();
            stack.Children.Add(new TextBlock{Text="Deck · 3 apps"});
            for(int i=0;i<12;i++)
                stack.Children.Add(new TextBlock {
                    Text="Running App "+i,Height=24
                });
            var border=new Border {
                MinWidth=180,MaxWidth=310,Background=System.Windows.Media.Brushes.DarkSlateBlue,
                BorderThickness=new Thickness(1),
                Child=stack
            };
            popup=new Popup {
                PlacementTarget=owner,Placement=PlacementMode.Custom,
                AllowsTransparency=true,StaysOpen=true,
                Child=border
            };
            Dock.GetMethod("Attach",Static)!.Invoke(
                null,new object[]{owner,popup,border});
            popup.IsOpen=true;
            Pump(owner);
            Assert(popup.IsOpen,"popup should open on actual Windows runner");
            var ownerRect=Bounds("WindowBounds",owner);
            var deckRect=Bounds("PopupBounds",popup);
            Assert(screen.Contains(deckRect) &&
                deckRect.Top>=ownerRect.Bottom+10 &&
                !deckRect.IntersectsWith(ownerRect),
                $"real native HWND overlap after open: clock={ownerRect}, deck={deckRect}");
            // Reflow after another content expansion and a small move.
            stack.Children.Add(new TextBlock {Text="Another app",Height=28});
            border.InvalidateMeasure();
            Pump(owner);
            ownerRect=Bounds("WindowBounds",owner);
            deckRect=Bounds("PopupBounds",popup);
            Assert(screen.Contains(deckRect) &&
                deckRect.Top>=ownerRect.Bottom+10 &&
                !deckRect.IntersectsWith(ownerRect),
                $"real native HWND overlap after reflow: clock={ownerRect}, deck={deckRect}");
            // R295: force an external native popup reposition that does not
            // fire owner.LocationChanged or popup.SizeChanged. The hook MUST
            // detect it and restore the minimum gap without user interaction.
            var popupHwnd=(PresentationSource.FromVisual(border) as
                System.Windows.Interop.HwndSource)?.Handle??IntPtr.Zero;
            Assert(popupHwnd!=IntPtr.Zero,"R295 HWND observer target not created");
            for(int trial=0;trial<2;trial++)
            {
                ownerRect=Bounds("WindowBounds",owner);
                deckRect=Bounds("PopupBounds",popup);
                var forcedY=ownerRect.Top+trial*4;
                Assert(SetWindowPos(popupHwnd,IntPtr.Zero,
                    ownerRect.Left,forcedY,0,0,MoveOnly),
                    "R295 could not force a native popup move");
                Pump(owner);
                deckRect=Bounds("PopupBounds",popup);
                ownerRect=Bounds("WindowBounds",owner);
                Assert(screen.Contains(deckRect) &&
                    deckRect.Top>=ownerRect.Bottom+10 &&
                    !deckRect.IntersectsWith(ownerRect),
                    $"R295 HWND mover bypassed below-only repair: clock={ownerRect}, deck={deckRect}");
            }
            Console.WriteLine("R295 PASS: forced native Deck/window overlap repaired on two external HWND moves.");
            Console.WriteLine("R292 PASS: below-only Deck after actual WPF popup Opened and content resize; 4 synthetic monitor topologies including negative origins; 12px visual gap; tiny-display fail-closed.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);return 1;
        }
        finally
        {
            if(popup!=null)popup.IsOpen=false;
            owner?.Close();app.Shutdown();
        }
    }
}