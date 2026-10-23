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
            TestApplicationSaveEvents();
            TestRecentFileStartupRecovery();
            TestFileClassification();
            TestStandardSubDonutScroll();
            TestApplicationsArcAndPinSearch();
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
    private static void TestFileClassification()
    {
        string folder=Path.Combine(Path.GetTempPath(),
            "DeskSpace-R58-Policy-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var type=Product.GetType("DeskSpace.DeskSpaceNewFileWatchV53")!;
        var classifier=type.GetMethod("IsOrdinaryUserFile",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Allowed(string path)=>(bool)classifier.Invoke(null,new object[]{path,true})!;
        string Create(string name,string? baseDir=null)
        {
            string path=Path.Combine(baseDir??folder,name);
            File.WriteAllText(path,"synthetic content");
            return path;
        }
        try
        {
            foreach(var name in new[]{"invoice.pdf","report.xlsx","note.txt",
                "image.jpg","archive.zip","extensionless","custom.xyz"})
                Assert(Allowed(Create(name)),"R58 rejected legitimate file "+name);
            foreach(var name in new[]{"download.crdownload","office.tmp",
                "thumbs.db",".hidden.pdf","~$office.xlsx","debug.dmp"})
                Assert(!Allowed(Create(name)),"R58 accepted generated file "+name);
            string hidden=Create("hidden-attribute.xlsx");
            File.SetAttributes(hidden,File.GetAttributes(hidden)|FileAttributes.Hidden);
            Assert(!Allowed(hidden),"R58 accepted hidden-attribute file");
            string cache=Path.Combine(folder,"GPUCache");
            Directory.CreateDirectory(cache);
            Assert(!Allowed(Create("cached.pdf",cache)),"R58 accepted cache-folder content");
            string hiddenDir=Path.Combine(folder,".cache");
            Directory.CreateDirectory(hiddenDir);
            Assert(!Allowed(Create("invisible.xlsx",hiddenDir)),
                "R58 accepted hidden-folder content");
            Console.WriteLine("R58 PASS: normal files accepted, caches and hidden files excluded.");
        }
        finally
        {
            try
            {
                foreach(var item in Directory.EnumerateFiles(folder,"*",
                    SearchOption.AllDirectories))
                    File.SetAttributes(item,FileAttributes.Normal);
                Directory.Delete(folder,true);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }
    private static void TestRecentFileStartupRecovery()
    {
        // File existed before the widget was started. The fast initial
        // reconciliation must still recover it, including unusual types.
        string folder=Path.Combine(Path.GetTempPath(),
            "DeskSpace-R59-Recovery-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,"program-saved.custom");
        File.WriteAllText(target,"finished before launch");
        var window=new Window{Width=240,Height=240,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false};
        var found=new List<string>();
        var watch=Make("DeskSpace.DeskSpaceNewFileWatchV53",
            window,(Action<string>)(p=>found.Add(p)),
            (IEnumerable<string>)new[]{folder});
        try
        {
            Call(watch,"Start");
            var deadline=DateTime.UtcNow.AddSeconds(9);
            while(!found.Any(p=>string.Equals(p,target,
                    StringComparison.OrdinalIgnoreCase)) &&
                  DateTime.UtcNow<deadline)
            {
                var frame=new DispatcherFrame();
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(110)};
                timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
                timer.Start();Dispatcher.PushFrame(frame);
            }
            Assert(found.Any(p=>string.Equals(p,target,
                StringComparison.OrdinalIgnoreCase)),
                "R59 app-saved file not recovered when Desktop widget launched afterwards");
            Console.WriteLine("R59 PASS: already-saved file recovered promptly at widget startup.");
        }
        finally
        {
            ((IDisposable)watch).Dispose();
            try{Directory.Delete(folder,true);}catch(IOException){}
            window.Close();
        }
    }
    private static void TestApplicationSaveEvents()
    {
        // Save patterns used by editors: update an existing workbook,
        // atomically rename a temporary export, then delete the original.
        string folder=Path.Combine(Path.GetTempPath(),
            "DeskSpace-AppSave-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string workbook=Path.Combine(folder,"existing.xlsx");
        File.WriteAllText(workbook,"original");
        var window=new Window{Width=250,Height=250,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false};
        var saved=new List<string>();
        var deleted=new List<string>();
        var watch=Make("DeskSpace.DeskSpaceNewFileWatchV53",
            window,(Action<string>)(p=>saved.Add(p)),
            (IEnumerable<string>)new[]{folder},
            (Action<string>)(p=>deleted.Add(p)));
        void Pump()
        {
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer{
                Interval=TimeSpan.FromMilliseconds(100)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
        try
        {
            Call(watch,"Start");
            File.AppendAllText(workbook," plus application changes");
            string temp=Path.Combine(folder,"save.tmp");
            string final=Path.Combine(folder,"saved-document.pdf");
            File.WriteAllText(temp,"complete content");
            File.Move(temp,final);
            var end=DateTime.UtcNow.AddSeconds(10);
            while(DateTime.UtcNow<end &&
                  (!saved.Any(p=>p.Equals(workbook,StringComparison.OrdinalIgnoreCase)) ||
                   !saved.Any(p=>p.Equals(final,StringComparison.OrdinalIgnoreCase))))
                Pump();
            Assert(saved.Any(p=>p.Equals(workbook,StringComparison.OrdinalIgnoreCase)),
                "R57 application saving existing Excel was not detected");
            Assert(saved.Any(p=>p.Equals(final,StringComparison.OrdinalIgnoreCase)),
                "R57 renamed application PDF was not detected");
            Assert(File.ReadAllText(workbook)=="original plus application changes",
                "R57 original workbook was corrupted");
            File.Delete(workbook);
            end=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<end &&
                  !deleted.Any(p=>p.Equals(workbook,StringComparison.OrdinalIgnoreCase)))
                Pump();
            Assert(deleted.Any(p=>p.Equals(workbook,StringComparison.OrdinalIgnoreCase)),
                "R57 file deletion was not reflected");
            Console.WriteLine("R57 PASS: application-created/saved Excel, renamed PDF, deleted file, and original integrity.");
        }
        finally
        {
            ((IDisposable)watch).Dispose();
            try{Directory.Delete(folder,true);}catch(IOException){}
            window.Close();
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
    private static void TestStandardSubDonutScroll()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSubDonutStandardV62",true)!;
        var plan=type.GetMethod("Plan",BindingFlags.Static|BindingFlags.NonPublic)!;
        object View(int total,int start,double center)=>
            plan.Invoke(null,new object[]{total,start,center,360.0/7.0})!;
        static int Num(object x,string name)=>(int)x.GetType()
            .GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|
                BindingFlags.Instance)!.GetValue(x)!;
        static double Real(object x,string name)=>(double)x.GetType()
            .GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|
                BindingFlags.Instance)!.GetValue(x)!;
        var first=View(12,0,-90);
        Assert(Num(first,"Count")==5 && Num(first,"HiddenLeft")==0 &&
            Num(first,"HiddenRight")==7,
            "Shared sub-donut must expose exactly five with seven hidden right");
        var middle=View(12,4,-90);
        Assert(Num(middle,"HiddenLeft")==4 && Num(middle,"HiddenRight")==3,
            "Shared sub-donut must show hidden counts on both sides");
        Assert(Real(middle,"Half")<=360.0/7.0+1e-8,
            "Sub-donut exceeded adjacent half-sector allowance");
        var advance=type.GetMethod("Advance",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        Assert((int)advance.Invoke(null,new object[]{middle,1})!=5,
            "Sub-donut should move one item per wheel step");
        var wheelType=type.GetNestedType("Wheel",BindingFlags.NonPublic)!;
        var wheel=Activator.CreateInstance(wheelType,true)!;
        var step=wheelType.GetMethod("Step",BindingFlags.Instance|
            BindingFlags.NonPublic)!;
        Assert((int)step.Invoke(wheel,new object[]{60})!=0,
            "Fine-scroll first 60 delta must accumulate");
        Assert((int)step.Invoke(wheel,new object[]{60})!=-1,
            "Fine-scroll next 60 delta must produce one item");
        Assert((int)step.Invoke(wheel,new object[]{-120})!=1,
            "Reverse mouse wheel must shift one item backward");
        var board=new Canvas{Width=240,Height=100};
        var indicators=type.GetMethod("DrawHiddenCounters",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        int shifted=0;
        indicators.Invoke(null,new object[]{board,middle,50.0,
            (Action<int>)(d=>shifted=d)});
        var buttons=board.Children.OfType<Button>().ToArray();
        Assert(buttons.Length==2 && (string)buttons[0].Content=="‹ 4" &&
            (string)buttons[1].Content=="3 ›",
            "Hidden counters missing on left or right of standard arc");
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent,buttons[0]));
        Assert(shifted==-1,"Clicking hidden-left counter failed to scroll");
        var animate=type.GetMethod("Animate",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        animate.Invoke(null,new object[]{board,1,-90.0});
        Assert(board.RenderTransform is TranslateTransform,
            "Smooth sub-donut animation not using a shared transform");
        Console.WriteLine("R62 WPF PASS: five visible, half-neighbor bounds, both hidden counts, "+
            "mouse wheel/touchpad stepping and smooth animation.");
    }
    private static void TestApplicationsArcAndPinSearch()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceApplicationsV55",true)!;
        var centreProperty=type.GetProperty("ApplicationsParentCenter",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        double centre=(double)centreProperty.GetValue(null)!;
        Assert(Math.Abs(centre-(-90+4*360.0/7))<.001,
            "Applications sector midpoint differs from actual parent anchor");
        var window=new Window{Width=420,Height=420,Left=360,Top=160,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None,Opacity=.5};
        var pins=new List<string>();
        int searchOpened=0;
        var apps=Make("DeskSpace.DeskSpaceApplicationsV55",
            window,(Func<IReadOnlyList<string>>)(()=>pins.ToArray()),
            (Action<string>)(p=>pins.Add(p)),
            (Action<string>)(p=>pins.Remove(p)),
            (Action)(()=>searchOpened++));
        try
        {
            window.Show();
            Call(apps,"Show");
            var canvas=Private<Canvas>(apps,"canvas");
            var bounds=Private<Rect>(apps,"bounds");
            Assert(canvas.Width<420 && canvas.Height<420,
                "Applications submenu still allocates an oversized detached Popup");
            Assert(canvas.Children.OfType<TextBlock>().Count()>=3,
                "Applications root options are missing");
            var popup=Private<System.Windows.Controls.Primitives.Popup>(apps,"arc");
            var geoType=Product.GetType("DeskSpace.DeskSpaceSubArcGeometryV48",true)!;
            double cx=325,cy=325;
            var offset=window.ActualWidth/2-cx+bounds.Left-10;
            Assert(Math.Abs(popup.HorizontalOffset-offset)<2,
                "Applications arc popup not anchored to parent circle");
            double centerX=cx-bounds.Left+10,centerY=cy-bounds.Top+10;
            foreach(var label in canvas.Children.OfType<TextBlock>().Take(3))
            {
                double x=Canvas.GetLeft(label)+label.Width/2-centerX;
                double y=Canvas.GetTop(label)+13-centerY;
                double angle=Math.Atan2(y,x)*180/Math.PI;
                Assert(Math.Abs(angle-centre)<360.0/14+1,
                    "Application submenu action drawn outside parent wedge");
            }
            Render(canvas,(int)Math.Ceiling(canvas.Width),
                (int)Math.Ceiling(canvas.Height),"applications-aligned-parent");
            Call(apps,"ShowApplicationPinSearch");
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer{
                Interval=TimeSpan.FromMilliseconds(180)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);
            Assert(searchOpened==1,
                "Search was not opened from the wheel + Pin command");
            var searchType=Product.GetType("DeskSpace.DeskSpaceSearchV11",true)!;
            var pinFlag=searchType.GetField("nextApplicationPinMode",
                BindingFlags.Static|BindingFlags.NonPublic)!;
            Assert(((int)pinFlag.GetValue(null)!)==1,
                "Pin mode was not passed into the circle Search module");
            pinFlag.SetValue(null,0);
            Console.WriteLine("R61 WPF PASS: bounded arc attachment and in-circle app search trigger.");
        }
        finally
        {
            Call(apps,"Dispose");
            window.Close();
        }
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