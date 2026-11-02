using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

internal static class Program
{
    static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    static void Assert(bool condition,string message)
    {
        if(!condition)throw new Exception("R314: "+message);
    }
    static void Pump(Window window)
    {
        for(var i=0;i<6;i++)
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
        Window? host=null; Popup? popup=null;
        try
        {
            var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            var min=false;
            host=new Window{
                Width=114,Height=114,
                Left=work.Left+Math.Min(90,work.Width-140),
                Top=work.Bottom-164,
                WindowStyle=WindowStyle.None,
                ShowInTaskbar=false,AllowsTransparency=true,
                Background=Brushes.Transparent,
                Content=new Border{Width=110,Height=110,Background=Brushes.SlateBlue}
            };
            host.Show();Pump(host);
            var items=new StackPanel{Margin=new Thickness(9,8,9,8)};
            items.Children.Add(new TextBlock{Text="Deck · 4 apps",
                Height=27,Foreground=Brushes.White});
            items.Children.Add(new TextBlock{Text="Running Apps · 4",
                Height=28,Foreground=Brushes.White});
            var scroll=new ScrollViewer{Content=items,MaxHeight=32,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var panel=new Border{
                MinWidth=180,MaxWidth=310,MaxHeight=34,
                Child=scroll,Background=Brushes.DarkSlateBlue
            };
            popup=new Popup{Child=panel,PlacementTarget=host,
                Placement=PlacementMode.Custom,
                StaysOpen=true,AllowsTransparency=true};
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var dock=asm.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
            var attach=dock.GetMethod("Attach",Static)!;
            attach.Invoke(null,new object?[]{host,popup,panel,
                new Func<bool>(()=>min)});
            var refresh=dock.GetMethod("RequestReflowV314",Static)!;
            var measure=dock.GetMethod("DesiredDeckHeightV314",Static)!;
            var desired=(double)measure.Invoke(null,
                new object?[]{items,250.0,false})!;
            Assert(desired>=156,"natural content must overcome stale 34px MaxHeight");
            popup.IsOpen=true;
            refresh.Invoke(null,new object?[]{panel,true});
            Pump(host);
            Assert(scroll.MaxHeight>=120,
                $"Standard view remained clipped: scroll MaxHeight={scroll.MaxHeight}");
            Assert(panel.MinHeight>=100,
                $"Expanded Deck has no visible viewport: MinHeight={panel.MinHeight}");
            Assert(scroll.VerticalOffset==0,
                "Freshly opened Deck title was scrolled out of view");
            for(int i=0;i<16;i++)items.Children.Add(new TextBlock{
                Text="Held item "+i,Height=24,Foreground=Brushes.White});
            refresh.Invoke(null,new object?[]{panel,false});
            Pump(host);
            Assert(scroll.MaxHeight>=120,
                "Adding full Deck items did not expand from stale narrow strip");
            min=true;
            refresh.Invoke(null,new object?[]{panel,false});
            Pump(host);
            Assert(panel.MinHeight==0,
                "Explicit minimize should still shrink to the header");
            min=false;
            refresh.Invoke(null,new object?[]{panel,true});
            Pump(host);
            Assert(panel.MinHeight>=100 && scroll.VerticalOffset==0,
                "Manually expanding did not restore full visible Deck header");
            Console.WriteLine("R314 PASS: clipped 34px Deck restores full viewport and header; live new content, manual minimize/expand and below-monitor sizing coexist.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(popup!=null)popup.IsOpen=false;host?.Close();app.Shutdown();}
    }
}