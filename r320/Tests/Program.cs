using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Application=System.Windows.Application;
using Brushes=System.Windows.Media.Brushes;

internal static class Program
{
    static readonly BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static;
    static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R320 single display point: "+message);
    }
    static Rectangle Plan(Type type,Rectangle widget,Rectangle work,int w,int h,int x)
    {
        var method=type.GetMethod("CanonicalDeckRectV320",Static)
            ??throw new Exception("Canonical Deck position method absent");
        return (Rectangle)method.Invoke(null,new object[]{widget,work,w,h,x})!;
    }
    static void Pump(Window host)
    {
        for(int i=0;i<6;i++)
        {
            host.UpdateLayout();
            var frame=new DispatcherFrame();
            host.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);
        }
    }
    [STAThread] static int Main()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Window? host=null;
        Popup? popup=null;
        try
        {
            var d=typeof(DeskSpace.WeatherReading).Assembly.GetType(
                "DeskSpace.DeskSpaceMonitorDockV190",true)!;
            var left=new Rectangle(0,0,1920,1080);
            var right=new Rectangle(1920,0,2560,1440);
            var leftWidget=new Rectangle(400,200,112,112);
            var rightWidget=new Rectangle(2400,350,112,112);
            var centerLeft=Plan(d,leftWidget,left,220,85,0);
            var shiftedLeft=Plan(d,leftWidget,left,220,85,67);
            var shiftedLeftAgain=Plan(d,leftWidget,left,220,85,67);
            Check(!centerLeft.IsEmpty && centerLeft.Y==leftWidget.Bottom+12,
                "Deck must remain below the source widget");
            Check(shiftedLeft.X-centerLeft.X==67,
                "manual horizontal drag was discarded by native positioning");
            Check(shiftedLeft==shiftedLeftAgain,
                "the same display has two competing placement results");
            var shiftedRight=Plan(d,rightWidget,right,220,85,-90);
            Check(right.Contains(shiftedRight),
                "second monitor Deck escaped its own display");
            Check(shiftedRight.X>=right.Left && shiftedRight.Left!=shiftedLeft.Left,
                "two physical monitors shared a display placement point");
            var seamRight=Plan(d,new Rectangle(1930,250,112,112),
                right,260,80,-1000);
            Check(seamRight.X==right.Left && right.Contains(seamRight),
                "negative saved X offset crossed monitor seam");
            var seamLeft=Plan(d,new Rectangle(1790,250,112,112),
                left,260,80,1000);
            Check(seamLeft.Right==left.Right && left.Contains(seamLeft),
                "positive saved X offset crossed monitor seam");

            var screen=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            host=new Window{
                Width=114,Height=114,
                Left=screen.Left+Math.Max(50,screen.Width/3),
                Top=screen.Top+80,
                WindowStyle=WindowStyle.None,ShowInTaskbar=false,
                AllowsTransparency=true,Background=Brushes.Transparent,
                Content=new Border{Width=110,Height=110,Background=Brushes.SlateBlue}
            };
            host.Show();Pump(host);
            var items=new StackPanel{Margin=new Thickness(8)};
            items.Children.Add(new TextBlock{Text="Deck · 4 apps",Height=27});
            items.Children.Add(new TextBlock{Text="Running Apps · 4",Height=28});
            var border=new Border{
                MinWidth=180,MaxWidth=310,
                Background=Brushes.DarkSlateBlue,
                Child=new ScrollViewer{Content=items}
            };
            CustomPopupPlacementCallback legacy=(size,target,offset)=>
                new[]{new CustomPopupPlacement(new System.Windows.Point(0,0),
                    PopupPrimaryAxis.None)};
            popup=new Popup{
                PlacementTarget=host,Child=border,
                Placement=PlacementMode.Custom,
                AllowsTransparency=true,StaysOpen=true
            };
            popup.CustomPopupPlacementCallback=legacy;
            d.GetMethod("RegisterMinimizedV314",Static)!.Invoke(null,
                new object[]{border,new Func<bool>(()=>false)});
            d.GetMethod("Attach",Static)!.Invoke(null,new object[]{host,popup,border});
            Check(!ReferenceEquals(legacy,popup.CustomPopupPlacementCallback),
                "obsolete first placement callback was not replaced");
            Check(ReferenceEquals(host,popup.PlacementTarget),
                "Deck placement target detached from its own widget");
            var candidate=popup.CustomPopupPlacementCallback!(
                new System.Windows.Size(220,90),new System.Windows.Size(114,114),
                new System.Windows.Point());
            Check(candidate.Length==1 && candidate[0].Point.Y>=110,
                "Dock produced multiple display points or positioned above widget");
            popup.IsOpen=true;Pump(host);
            Check(border.ActualHeight>35,"canonical Deck never displayed");
            Console.WriteLine(
                "R320 PASS: one canonical Deck position, offsets retained, monitor-seam isolation, WPF placement callback supersedes legacy and native popup opens.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(popup!=null)popup.IsOpen=false;host?.Close();app.Shutdown();}
    }
}
