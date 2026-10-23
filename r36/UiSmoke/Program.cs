using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskSpace;

internal static class Program
{
    private const BindingFlags Any = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static string outdir = "";
    private static readonly Assembly Product = typeof(WeatherReading).Assembly;
    [STAThread]
    private static int Main(string[] args)
    {
        outdir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"deskspace-r36-ui");
        Directory.CreateDirectory(outdir);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            TestWeather();
            TestCalendar();
            TestCalculator();
            TestMediaPill();
            TestNewFileNotifications();
            TestConnectedChildDonut();
            TestExcelPasteTimeDate();
            TestSubArcScreenFit();
            Console.WriteLine("R36 WPF visual smoke PASS: weather full disk, calendar navigation/event +, internal calculator 1+2=3, media pill controls and screenshots");
            app.Shutdown();
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine("R36 WPF visual smoke FAILED: "+ex);
            app.Shutdown();
            return 1;
        }
    }
    private static void TestNewFileNotifications()
    {
        string folder=Path.Combine(Path.GetTempPath(),
            "DeskSpace-R53-Watch-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var window=new Window{Width=250,Height=250,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false};
        var found=new List<string>();
        var watch=Make("DeskSpace.DeskSpaceNewFileWatchV53",
            window,(Action<string>)(p=>found.Add(p)),
            (IEnumerable<string>)new[]{folder});
        try
        {
            Call(watch,"Start");
            int count=(int)watch.GetType().GetProperty("WatchCount",Any)!
                .GetValue(watch)!;
            Assert(count==1,"R53 test directory watcher did not start");
            var names=new[]{"new.pdf","new.xlsx","new.jpg","new.bin","extensionless"};
            foreach(string name in names)
                File.WriteAllText(Path.Combine(folder,name),"synthetic source");
            var until=DateTime.UtcNow.AddSeconds(8);
            while(found.Distinct(StringComparer.OrdinalIgnoreCase).Count()<names.Length &&
                  DateTime.UtcNow<until)
            {
                var frame=new DispatcherFrame();
                var timer=new DispatcherTimer{
                    Interval=TimeSpan.FromMilliseconds(110)
                };
                timer.Tick+=(_,_)=>{
                    timer.Stop();
                    frame.Continue=false;
                };
                timer.Start();
                Dispatcher.PushFrame(frame);
            }
            foreach(string name in names)
                Assert(found.Any(p=>string.Equals(Path.GetFileName(p),name,
                    StringComparison.OrdinalIgnoreCase)),
                    "R53 new file not detected: "+name);
            var original=Path.Combine(folder,"new.xlsx");
            var shelf=Make("DeskSpace.DeskSpaceDropShelfV15",window);
            try
            {
                Call(shelf,"OfferNewFile",original);
                var detected=(System.Collections.IList)Private<object>(
                    shelf,"detected");
                Assert(detected.Count==1,
                    "R53 discovered file not rendered in shared shelf");
                var item=detected[0]!;
                Call(shelf,"Remove",item);
                Assert(File.Exists(original),
                    "Dismissing a new file deleted its original");
                Assert(detected.Count==0,
                    "R53 dismissed new item still displayed");
            }
            finally { Call(shelf,"Dispose"); }
            Console.WriteLine("R53 all extensions / rename-safe discovery / shelf original-file safety PASS.");
        }
        finally
        {
            Call(watch,"Dispose");
            window.Close();
            try { Directory.Delete(folder,true); }
            catch(IOException) {}
        }
    }

    private static void TestSubArcScreenFit()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSubArcGeometryV48",true)!;
        var angleAt=type.GetMethod("FitAngleAt",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var outer=type.GetMethod("Outer",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var inner=type.GetMethod("Inner",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        double firstOuter=(double)outer.Invoke(null,new object[]{0})!;
        double nextInner=(double)inner.Invoke(null,new object[]{1})!;
        Assert(Math.Abs(nextInner-firstOuter-2)<.001,
            "Excel Information sub-arcs are no longer attached with a 2-DIP gap");
        var center=new Point(1380,310);
        var desktop=new Rect(0,0,1650,880);
        const double half=24;
        double chosen=(double)angleAt.Invoke(null,new object[]{
            3,205.0,half,center,desktop,1.0})!;
        double radius=(double)outer.Invoke(null,new object[]{3})!;
        for(int n=0;n<=16;n++)
        {
            double rad=(chosen-half+n*half*2/16)*Math.PI/180;
            double x=center.X+radius*Math.Cos(rad);
            double y=center.Y+radius*Math.Sin(rad);
            Assert(x>=11&&x<=1639&&y>=11&&y<=869,
                "Excel child sectors clip against the upper or lateral screen edge");
        }
        Console.WriteLine("R48 Information-reference contiguous arcs and screen edge checks PASS.");
    }
    private static void TestExcelPasteTimeDate()
    {
        var actions=Product.GetType("DeskSpace.DeskSpaceExcelActionsV46",true)!;
        var cell=Make("DeskSpace.DeskSpaceExcelCellV16");
        cell.GetType().GetProperty("ValueKind")!.SetValue(cell,"action_static_date");
        var keyMethod=actions.GetMethod("NativeSendKeys",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var keyboard=(string)keyMethod.Invoke(null,new object[]{
            "action_static_date"})!;
        Assert(keyboard.StartsWith("^;"),
            "Ctrl+; should execute in Excel, not be computed by DeskSpace");
        var timeKeys=(string)keyMethod.Invoke(null,new object[]{
            "action_static_time"})!;
        Assert(timeKeys.StartsWith("^+;"),
            "Ctrl+Shift+; must execute as Excel's native keyboard action");
        var label=actions.GetMethod("FromShortcut",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert((string)label.Invoke(null,new object[]{"Ctrl+;"})==
            "action_static_date","Excel Ctrl+; mapping not recognised");
        Console.WriteLine("R51 Excel-native keyboard and formula dispatch PASS.");
    }
    private static void TestConnectedChildDonut()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDonutGeometryV43",true)!;
        var center=type.GetMethod("OuterSectorCenter",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var scale=type.GetMethod("Scale",BindingFlags.Static|
            BindingFlags.NonPublic,null,new[]{typeof(double)},null)!;
        double angle=(double)center.Invoke(null,new object[]{6,7})!;
        double ratio=(double)scale.Invoke(null,new object[]{420.0})!;
        Assert(Math.Abs(angle-244.2857142857143)<.0001,
            "Excel child is not aligned with outer parent sector centre");
        Assert(Math.Abs(ratio-1)<.0001,
            "Child popup has wrong parent-ring coordinate scaling");
        Console.WriteLine("R43 WPF connected sub-donut geometry PASS.");
    }
    private static object Make(string fullName,params object?[] args)
    {
        var type=Product.GetType(fullName,true)!;
        return Activator.CreateInstance(type,Any,null,args,null)
            ??throw new Exception("Unable to create "+fullName);
    }
    private static object? Call(object target,string method,params object?[] args) =>
        target.GetType().GetMethod(method,Any)?.Invoke(target,args)
        ??(method=="Create"?throw new InvalidOperationException("Create returned null"):null);
    private static T Private<T>(object target,string field) =>
        (T)(target.GetType().GetField(field,Any)?.GetValue(target)
            ??throw new Exception("No "+field));
    private static void Set(object target,string name,object value) =>
        (target.GetType().GetField(name,Any)
          ??throw new Exception("Missing "+name)).SetValue(target,value);
    private static void Assert(bool ok,string reason)
    {
        if(!ok)throw new Exception(reason);
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        int n=VisualTreeHelper.GetChildrenCount(root);
        for(int i=0;i<n;i++)
            foreach(var child in Walk(VisualTreeHelper.GetChild(root,i)))
                yield return child;
    }
    private static Button ButtonWith(DependencyObject root,string label) =>
        Walk(root).OfType<Button>().FirstOrDefault(x=>x.Content is string s&&s==label)
        ??throw new Exception("Missing button "+label);
    private static string Render(FrameworkElement visual,int width,int height,string name)
    {
        visual.Measure(new Size(width,height));
        visual.Arrange(new Rect(0,0,width,height));
        visual.UpdateLayout();
        var pixels=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        pixels.Render(visual);
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(pixels));
        string path=Path.Combine(outdir,name+".png");
        using(var stream=File.Create(path))encoder.Save(stream);
        Assert(new FileInfo(path).Length>2500,"Blank/near-empty render: "+name);
        return path;
    }
    private static void TestWeather()
    {
        var samples=new[]{
            ("clear-day",new WeatherReading("Bengaluru",26,0,true,DateTime.UtcNow)),
            ("rain",new WeatherReading("Bengaluru",22,63,true,DateTime.UtcNow)),
            ("night",new WeatherReading("Bengaluru",21,2,false,DateTime.UtcNow))
        };
        foreach(var (name,reading) in samples)
        {
            var scene=(FrameworkElement)Make("DeskSpace.DeskSpaceWeatherSceneV11",
                reading,272.0);
            Assert(scene.Width==272 && scene.Height==272,"Weather scene does not fill circle");
            var ellipse=scene.Clip as EllipseGeometry;
            Assert(ellipse!=null&&ellipse.RadiusX>=130&&ellipse.RadiusY>=130,
                "Weather scene still clipped to a semicircle");
            Render(scene,272,272,"weather-"+name);
            Call(scene,"StopMotion");
        }
    }
    private static void TestCalendar()
    {
        int events=0,expand=0,close=0;
        var calendar=Make("DeskSpace.DeskSpaceCompactCalendarV36");
        var ui=(FrameworkElement)(Call(calendar,"Create",
            (Action)(()=>expand++),(Action)(()=>close++),
            (Action)(()=>events++))??throw new Exception("Calendar did not render"));
        Assert(ui.Width==342 && ui.Height==342,"Calendar still uses oversized circle");
        Render(ui,342,342,"calendar-compact");
        ButtonWith(ui,"+").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(events==1 && expand==0,"Calendar + reopened planner instead of new event");
        ButtonWith(ui,"Expand ↗").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(expand==1,"Calendar expand stopped working");
        ButtonWith(ui,"‹").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ButtonWith(ui,"›").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ButtonWith(ui,"Today").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ButtonWith(ui,"←").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(close==1,"Calendar exit stopped working");
    }
    private static void TestCalculator()
    {
        var calculator=Make("DeskSpace.DeskSpaceCalculatorV22");
        var ui=(FrameworkElement)(Call(calculator,"Create")
            ??throw new Exception("In-app calculator unavailable"));
        foreach(var key in new[]{"1","+","2","="})
            ButtonWith(ui,key).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var display=Private<TextBlock>(calculator,"display");
        Assert(decimal.TryParse(display.Text,out decimal value) && value==3m,
            "Embedded calculator 1+2 did not equal 3");
        Render(ui,324,395,"calculator-internal");
    }
    private static void TestMediaPill()
    {
        var player=Make("DeskSpace.DeskSpaceMediaV18");
        var host=new Window{Width=116,Height=116,Left=100,Top=100,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None,Opacity=0};
        try
        {
            Set(player,"visible",true);
            Call(player,"ShowBelow",host,(Func<string>)(()=>"Clock"));
            Call(player,"TogglePanel");
            var frame=Private<Border>(player,"frame");
            Assert(Math.Abs(frame.Width-298)<.01,"Minimized player still wrong width");
            Assert(Private<StackPanel>(player,"pillContent").Visibility==Visibility.Visible,
                "Minimized media controls not rendered");
            Assert(Private<Slider>(player,"pillSeek")!=null,"Minimized seek bar absent");
            var buttons=Walk(frame).OfType<Button>().Select(b=>b.ToolTip?.ToString())
                .Where(x=>!string.IsNullOrEmpty(x)).ToArray();
            foreach(string name in new[]{"Previous track","Play or pause",
                "Next track","Restore full media player"})
                Assert(buttons.Contains(name),"Missing minimized media control: "+name);
            Render(frame,298,76,"media-minimized");
            Call(player,"TogglePanel");
            Assert(frame.Width<298,"Restored player did not return to compact width");
        }
        finally
        {
            Call(player,"Dispose");
            host.Close();
        }
    }
}