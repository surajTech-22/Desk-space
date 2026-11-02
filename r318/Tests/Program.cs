using System;
using System.Linq;
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
    private static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static void Assert(bool value,string message)
    {
        if(!value)throw new InvalidOperationException("R318 Deck fit-content regression: "+message);
    }
    private static void Pump(Window window)
    {
        for(int i=0;i<8;i++)
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
        Window? host=null;
        Popup? popup=null;
        try
        {
            var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            bool minimized=false;
            host=new Window{
                Width=114,Height=114,
                Left=work.Left+Math.Min(75,Math.Max(0,work.Width-145)),
                Top=work.Top+25,
                WindowStyle=WindowStyle.None,
                ShowInTaskbar=false,AllowsTransparency=true,
                Background=Brushes.Transparent,
                Content=new Border{Width=110,Height=110,Background=Brushes.SlateBlue}
            };
            host.Show();
            Pump(host);
            var items=new StackPanel{Margin=new Thickness(9,8,9,8)};
            items.Children.Add(new Border{
                Height=28,
                Child=new TextBlock{Text="Deck · 4 apps",Foreground=Brushes.White}
            });
            items.Children.Add(new Border{
                Height=28,Margin=new Thickness(1,2,1,2),
                Child=new TextBlock{Text="▸  Running Apps · 4",Foreground=Brushes.White}
            });
            var scroll=new ScrollViewer{
                Content=items,MaxHeight=32,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto
            };
            var panel=new Border{
                MinWidth=180,MaxWidth=310,MaxHeight=34,
                Child=scroll,Background=Brushes.DarkSlateBlue
            };
            popup=new Popup{
                Child=panel,PlacementTarget=host,StaysOpen=true,
                AllowsTransparency=true,Placement=PlacementMode.Custom
            };
            var dock=typeof(DeskSpace.WeatherReading).Assembly.GetType(
                "DeskSpace.DeskSpaceMonitorDockV190",true)!;
            var measure=dock.GetMethod("DesiredDeckHeightV314",Static)!;
            var reflow=dock.GetMethod("RequestReflowV314",Static)!;
            var natural=(double)measure.Invoke(null,new object?[]{items,250.0,false})!;
            Assert(natural>=65 && natural<120,
                $"A two-row hover-only deck should fit contents, not leave blank space; height={natural}");
            var headerOnly=(double)measure.Invoke(null,new object?[]{items,250.0,true})!;
            Assert(headerOnly<=48,"Manual minimize no longer returns compact header");
            dock.GetMethod("RegisterMinimizedV314",Static)!.Invoke(
                null,new object?[]{panel,new Func<bool>(()=>minimized)});
            dock.GetMethod("Attach",Static)!.Invoke(null,new object?[]{host,popup,panel});
            popup.IsOpen=true;
            reflow.Invoke(null,new object?[]{panel,true});
            Pump(host);
            Assert(scroll.MinHeight==0 && panel.MinHeight==0,
                "Sparse deck still inherits a forced blank viewport");
            Assert(scroll.MaxHeight>=65 && scroll.MaxHeight<120,
                $"Sparse Deck scrollbar reserved blank height: {scroll.MaxHeight}");
            Assert(panel.ActualHeight>45 && panel.ActualHeight<125,
                $"Actual visible Deck is not content-fitted: {panel.ActualHeight}");
            Assert(scroll.VerticalOffset==0,"Header scrolled away on deck open");
            double small=panel.ActualHeight;
            for(int i=0;i<14;i++)
                items.Children.Add(new TextBlock{
                    Text="Held file "+i,Height=25,Foreground=Brushes.White});
            reflow.Invoke(null,new object?[]{panel,false});
            Pump(host);
            Assert(panel.ActualHeight>small+100,
                $"Deck did not grow to accommodate newly added items: {panel.ActualHeight} vs {small}");
            Assert(panel.ActualHeight<550,"Deck can escape native monitor height bounds");
            while(items.Children.Count>2)items.Children.RemoveAt(2);
            reflow.Invoke(null,new object?[]{panel,false});
            Pump(host);
            Assert(panel.ActualHeight<125,
                $"Deck did not shrink after last held file was removed: {panel.ActualHeight}");
            minimized=true;
            reflow.Invoke(null,new object?[]{panel,false});
            Pump(host);
            Assert(panel.MinHeight==0 && scroll.MinHeight==0,
                "Explicit minimize must not retain fixed viewport");
            minimized=false;
            reflow.Invoke(null,new object?[]{panel,true});
            Pump(host);
            Assert(panel.ActualHeight<125 && scroll.VerticalOffset==0,
                "Manual re-expand created a blank viewport or stale scroll offset");
            Console.WriteLine(
                "R318 PASS: Running Apps only = compact fit-content Deck; dynamic addition/removal resizes native popup; manual minimize/reexpand and hover-only architecture retained.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(popup!=null)popup.IsOpen=false;host?.Close();app.Shutdown();}
    }
}
