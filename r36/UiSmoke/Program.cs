using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
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
            TestDueShelfV74();
            TestReminderV76();
            TestStandardSubDonutScroll();
            TestDonutHomeAndApplicationIcons();
            TestMiddleButtonSubDonutScroll();
            TestWeatherClockContrast();
            TestWheelOverVisibleInformationItem();
            TestInformationSelectionPreviewGate();
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
    private static void TestInformationSelectionPreviewGate()
    {
        var window=new Window{Width=420,Height=420,Left=140,Top=140,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false,Opacity=.2};
        var info=Make("DeskSpace.DeskSpaceInformationV36",window,
            (Action)(()=>{}),(Action)(()=>{}));
        try
        {
            window.Show();
            var nested=Product.GetType("DeskSpace.DeskSpaceInformationV36",true)!
                .GetNestedType("Collection",BindingFlags.NonPublic)!;
            var demo=Activator.CreateInstance(nested,true)!;
            var id=Guid.NewGuid();
            nested.GetProperty("Id")!.SetValue(demo,id);
            nested.GetProperty("Name")!.SetValue(demo,"Temporary hover preview");
            var list=(System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(nested))!;
            list.Add(demo);
            Set(info,"all",list);
            Call(info,"RenderArc");
            var arc=Private<System.Windows.Controls.Primitives.Popup>(info,"arc");
            var shelf=Private<System.Windows.Controls.Primitives.Popup>(info,"shelf");
            arc.IsOpen=true;
            Call(info,"HoverChild",0,id);
            Assert(shelf.IsOpen,
                "R64 hovering Information should immediately display a preview");
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(720)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);
            Assert(shelf.IsOpen && arc.IsOpen,
                "R64 hover preview must remain while hovered, never auto-commit");
            Call(info,"EndArcHover");
            Assert(!shelf.IsOpen,
                "R64 temporary hover preview persisted after pointer left");
            Call(info,"HoverChild",0,id);
            Assert(shelf.IsOpen,"R64 hover preview failed to reopen");
            Call(info,"ActivateChild",0,id);
            Assert(shelf.IsOpen,
                "Explicit selection should keep Information preview open");
            Call(info,"HideArc");
            Assert(shelf.IsOpen,
                "Explicitly selected Information shelf should survive arc closing");
            Call(info,"Show");
            Assert(!shelf.IsOpen,
                "Returning to Information did not dismiss prior preview");
            Console.WriteLine("R64 WPF PASS: immediate Information hover preview, "+
                "no dwell commit, leave cleanup, explicit selection persistence.");
        }
        finally
        {
            ((IDisposable)info).Dispose();
            window.Close();
        }
    }
    private static void TestWheelOverVisibleInformationItem()
    {
        // Real WPF Popup geometry and routed PreviewMouseWheel over the
        // fourth visible wedge, not a call to the pagination function.
        var window=new Window{Width=420,Height=420,Left=290,Top=175,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false,Opacity=.65};
        var info=Make("DeskSpace.DeskSpaceInformationV36",window,
            (Action)(()=>{}),(Action)(()=>{}));
        var original=System.Windows.Forms.Control.MousePosition;
        try
        {
            window.Show();
            var model=Product.GetType("DeskSpace.DeskSpaceInformationV36",true)!
                .GetNestedType("Collection",BindingFlags.NonPublic)!;
            var list=(System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(model))!;
            for(int i=0;i<10;i++)
            {
                var item=Activator.CreateInstance(model,true)!;
                model.GetProperty("Name")!.SetValue(item,"Item "+(i+1));
                list.Add(item);
            }
            Set(info,"all",list);
            Call(info,"RenderArc");
            var canvas=Private<Canvas>(info,"arcCanvas");
            var arc=Private<System.Windows.Controls.Primitives.Popup>(info,"arc");
            arc.IsOpen=true;
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(160)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);
            // Information uses five 22.8-degree wedges starting at -147.
            // Index three is the fourth named collection, where users wheel.
            double angle=(-147+3.5*22.8)*Math.PI/180.0;
            var local=new Point(244+233.5*Math.Cos(angle),
                306+233.5*Math.Sin(angle));
            var absolute=canvas.PointToScreen(local);
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point(
                (int)Math.Round(absolute.X),(int)Math.Round(absolute.Y));
            frame=new DispatcherFrame();
            timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(110)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);
            var pointer=canvas.PointFromScreen(new Point(
                System.Windows.Forms.Control.MousePosition.X,
                System.Windows.Forms.Control.MousePosition.Y));
            Assert(canvas.InputHitTest(pointer)!=null,
                "Fourth visible Information item is not WPF hit-testable");
            Assert(Private<int>(info,"startIndex")==0,
                "Unexpected initial Information paging position");
            var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,
                Environment.TickCount,-120){
                RoutedEvent=Mouse.PreviewMouseWheelEvent};
            canvas.RaiseEvent(wheel);
            Assert(wheel.Handled,
                "Mouse wheel over visible Information item was not consumed");
            Assert(Private<int>(info,"startIndex")==1,
                "Wheel over fourth item did not reveal next hidden item");
            Console.WriteLine("R64 WPF PASS: pointer over fourth Information "+
                "wedge receives native routed wheel and advances exactly one.");
        }
        finally
        {
            System.Windows.Forms.Cursor.Position=original;
            ((IDisposable)info).Dispose();
            window.Close();
        }
    }
    private static void TestWeatherClockContrast()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWeatherContrastV64",true)!;
        var color=type.GetMethod("ClockColor",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var ratio=type.GetMethod("ContrastRatio",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var apply=type.GetMethod("Apply",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var bright=new WeatherReading("Location-based weather",25,2,true,DateTime.UtcNow);
        var cloudy=new WeatherReading("Another city",25,3,true,DateTime.UtcNow);
        var night=new WeatherReading("Night location",18,1,false,DateTime.UtcNow);
        Color Bright()=>(Color)color.Invoke(null,new object[]{bright,true})!;
        Color Night()=>(Color)color.Invoke(null,new object[]{night,true})!;
        Assert(Bright().R<70 && Bright().G<70,
            "Daytime pale sky should use dark clock text");
        Assert((Color)color.Invoke(null,new object[]{cloudy,true})!=Bright(),
            "Overcast weather uses the dark scene, not daytime-pale ink");
        Assert(Night().R==255,"Night weather should use white clock text");
        double Contrast(Color foreground,Color background)=>
            (double)ratio.Invoke(null,new object[]{foreground,background})!;
        Assert(Contrast(Bright(),Color.FromRgb(100,176,234))>=4.5 &&
            Contrast(Bright(),Color.FromRgb(211,236,250))>=4.5,
            "Daytime clock contrast below 4.5:1 over sky gradient");
        Assert(Contrast(Night(),Color.FromRgb(59,70,121))>=4.5,
            "Nighttime clock contrast below 4.5:1");
        var widget=new TextBlock{Text="03:15 PM"};
        widget.Inlines.Clear();
        var run=new System.Windows.Documents.Run("03:15 PM"){
            Foreground=Brushes.White};
        widget.Inlines.Add(run);
        apply.Invoke(null,new object[]{widget,bright,true});
        Assert(((SolidColorBrush)run.Foreground).Color==Bright() &&
            ((SolidColorBrush)widget.Foreground).Color==Bright(),
            "Clock contrast did not override local text runs");
        apply.Invoke(null,new object[]{widget,night,true});
        Assert(((SolidColorBrush)run.Foreground).Color==Night(),
            "Clock did not refresh foreground on weather/day transition");
        Console.WriteLine("R64 WPF PASS: bright/day, dark/overcast and night clock "+
            "contrast including explicit Run foreground overrides.");
    }
    private static void TestMiddleButtonSubDonutScroll()
    {
        var api=Product.GetType("DeskSpace.DeskSpaceMiddleScrollV63",true)!;
        var direction=api.GetMethod("Direction",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        int Dir(Point from,Point to)=>(int)direction.Invoke(null,
            new object[]{from,to})!;
        var center=new Point(50,50);
        Assert(Dir(center,new Point(55,55))==0,
            "Middle scroll activates within the dead zone");
        Assert(Dir(center,new Point(50,92))==1,
            "Middle scroll should reveal hidden items below");
        Assert(Dir(center,new Point(50,8))==-1,
            "Middle scroll should reveal hidden items above");
        Assert(Dir(center,new Point(100,50))==1 &&
            Dir(center,new Point(0,50))==-1,
            "Middle scroll also needs left/right radial movement");
        var type=api.GetNestedType("Controller",BindingFlags.NonPublic)!;
        var surface=new Canvas{Width=220,Height=140};
        int shifted=0;
        bool visible=true,hasHidden=true;
        var control=Activator.CreateInstance(type,Any,null,
            new object[]{surface,(Func<bool>)(()=>hasHidden),
                (Func<bool>)(()=>visible),
                (Action<int>)(d=>shifted+=d)},null)!;
        var start=type.GetMethod("Start",Any)!;
        var pulse=type.GetMethod("Pulse",Any)!;
        var stop=type.GetMethod("Stop",Any)!;
        var active=type.GetProperty("IsActive",Any)!;
        try
        {
            start.Invoke(control,new object[]{center});
            Assert((bool)active.GetValue(control)!,
                "Middle autoscroll did not enter active mode");
            pulse.Invoke(control,new object[]{new Point(52,52)});
            Assert(shifted==0,"Autoscroll should have a pointer dead zone");
            pulse.Invoke(control,new object[]{new Point(50,95)});
            Assert(shifted==1,
                "Middle autoscroll did not advance a hidden item");
            pulse.Invoke(control,new object[]{new Point(50,2)});
            Assert(shifted==0,
                "Middle autoscroll did not reverse direction");
            hasHidden=false;
            pulse.Invoke(control,new object[]{new Point(50,95)});
            Assert(!(bool)active.GetValue(control)!,
                "Autoscroll remained active without hidden items");
            hasHidden=true;start.Invoke(control,new object[]{center});
            visible=false;
            pulse.Invoke(control,new object[]{new Point(50,95)});
            Assert(!(bool)active.GetValue(control)!,
                "Middle autoscroll did not stop when parent closed");
            stop.Invoke(control,null);
            Console.WriteLine("R63 WPF PASS: middle-click scroll activation, "+
                "dead zone, both directions, and automatic cleanup.");
        }
        finally{((IDisposable)control).Dispose();}
    }
    private static void TestDonutHomeAndApplicationIcons()
    {
        var hostType=Product.GetType("DeskSpace.DeskSpaceDonutHomeV65",true)!;
        var state=Activator.CreateInstance(hostType,true)!;
        var capture=hostType.GetMethod("Capture",BindingFlags.Instance|
            BindingFlags.NonPublic)!;
        var restore=hostType.GetMethod("Restore",BindingFlags.Instance|
            BindingFlags.NonPublic)!;
        var window=new Window{Left=137,Top=182,Width=420,Height=420};
        capture.Invoke(state,new object[]{window});
        window.Left=260;window.Top=340;
        Assert((bool)restore.Invoke(state,new object[]{window})!,
            "Donut lost its pre-expansion home");
        Assert(Math.Abs(window.Left-137)<.01 &&
            Math.Abs(window.Top-182)<.01,
            "Donut stayed in temporary screen-edge position");
        // Capturing the next manual location must not reuse an older origin.
        window.Left=310;window.Top=225;
        capture.Invoke(state,new object[]{window});
        window.Left=440;window.Top=280;
        restore.Invoke(state,new object[]{window});
        Assert(Math.Abs(window.Left-310)<.01 &&
            Math.Abs(window.Top-225)<.01,
            "Donut restoration overwrote new user placement");
        Assert(!(bool)restore.Invoke(state,new object[]{window})!,
            "A finished expansion must not restore again");
        var icons=Product.GetType("DeskSpace.DeskSpaceApplicationIconsV65",true)!;
        var get=icons.GetMethod("Get",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        string candidate=System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "notepad.exe");
        if(!System.IO.File.Exists(candidate))
            candidate=System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");
        Assert(System.IO.File.Exists(candidate),
            "Windows test machine lacks a standard executable to inspect");
        var icon=get.Invoke(null,new object[]{candidate}) as ImageSource;
        Assert(icon!=null,"Application logo was not extracted from Windows file");
        Assert(ReferenceEquals(icon,get.Invoke(null,new object[]{candidate})),
            "App logo extraction is not cached for repeat rendering");
        window.Close();
        Console.WriteLine("R65 WPF PASS: donut returns to original location "+
            "and application logos are real, cached shell icons.");
    }
        private static void TestReminderV76()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDueShelfV74",true)!;
        var action=type.GetMethod("IsAllowedAction",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        bool Can(string mode,string target)=>(bool)action.Invoke(null,
            new object[]{mode,target})!;
        Assert(Can("None","") && !Can("Open file","") &&
            !Can("Open application",@"C:\Windows\System32\cmd.exe /c calc") &&
            !Can("Open file","https://example.com"),
            "R76 unsafe or unsupported reminder launch target accepted");
        var model=new ReminderItem{Title="Saved",AlertSound="Hand",
            AlertAction="Open file",AlertTarget=@"C:\fake\note.txt"};
        var ws=new Workspace();ws.Reminders.Add(model);
        var json=System.Text.Json.JsonSerializer.Serialize(ws);
        var restored=System.Text.Json.JsonSerializer.Deserialize<Workspace>(json)!;
        Assert(restored.Reminders.Single().AlertSound=="Hand" &&
            restored.Reminders.Single().AlertTarget==model.AlertTarget,
            "R76 sound/action preferences were not persisted");
        var view=type.GetMethod("Preview",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var alertType=Product.GetType("DeskSpace.DeskSpaceDueShelfV74+DueAlert",true)!;
        var alert=Activator.CreateInstance(alertType,
            new object[]{model.Id,model.Title,new DateTime(2026,10,23,19,30,45)})!;
        var array=Array.CreateInstance(alertType,1);array.SetValue(alert,0);
        var preview=(FrameworkElement)view.Invoke(null,new object[]{
            array,new DateTime(2026,10,23,19,30,45),
            (Action<Guid>)(_=>{}),(Action<Guid>)(_=>{})})!;
        Assert(Walk(preview).OfType<Border>().Any(b=>
            b.Background is LinearGradientBrush),
            "R76 rainbow animation should live inside due-alert perimeter");
        Render(preview,310,140,"r76-rainbow-due-reminder");
        Console.WriteLine("R76 PASS: user-configured sound/action serialization, launch restrictions, rainbow due display.");
    }

    private static void TestDueShelfV74()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDueShelfV74",true)!;
        var modelType=Product.GetType("DeskSpace.DeskSpaceDueShelfV74+DueAlert",true)!;
        var find=type.GetMethod("FindDue",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var now=new DateTime(2026,10,23,19,30,0);
        var dueItem=new ReminderItem{Title="Invoice due",Due=now.AddMinutes(-1)};
        var future=new ReminderItem{Title="Tomorrow",Due=now.AddDays(1)};
        var completed=new ReminderItem{Title="Completed",
            Due=now.AddHours(-1),Done=true};
        var notified=new ReminderItem{Title="Already notified",
            Due=now.AddHours(-1),LastAlert=now.AddMinutes(-1)};
        var ws=new Workspace();
        ws.Reminders.AddRange(new[]{dueItem,future,completed,notified});
        var due=(System.Collections.IEnumerable)find.Invoke(null,new object[]{ws,now})!;
        var count=due.Cast<object>().Count();
        Assert(count==1,
            "R74 due scheduling uses Windows local system time");
        var placement=type.GetMethod("PlaceAtCircle",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var bounds=(Rect)placement.Invoke(null,new object[]{
            new Rect(350,180,150,150),new Rect(0,0,1050,800),
            298.0,106.0})!;
        Assert(Math.Abs(bounds.Top-338)<.1 &&
            Math.Abs(bounds.Left-276)<.1,
            "R74 due shelf stays directly below circle");
        var nearBottom=(Rect)placement.Invoke(null,new object[]{
            new Rect(350,690,150,100),new Rect(0,0,1050,800),
            298.0,106.0})!;
        Assert(nearBottom.Bottom<=690 && nearBottom.Top>=0,
            "R74 due shelf must fit screen edge without losing visibility");
        var alert=Activator.CreateInstance(modelType,
            new object[]{dueItem.Id,dueItem.Title,dueItem.Due})!;
        var list=Array.CreateInstance(modelType,1);
        list.SetValue(alert,0);
        var preview=type.GetMethod("Preview",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var shown=(FrameworkElement)preview.Invoke(null,new object[]{
            list,now,(Action<Guid>)(_=>{}),(Action<Guid>)(_=>{})})!;
        Render(shown,310,135,"r74-due-shelf");
        Assert(Walk(shown).OfType<Button>().Count()==2,
            "R74 due shelf requires completion and dismiss actions");
        Console.WriteLine("R74 WPF PASS: local time, due-only, no duplicates, anchored compact alert.");
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
        Assert((int)advance.Invoke(null,new object[]{middle,1})==5,
            "Sub-donut should move one item per wheel step");
        var wheelType=type.GetNestedType("Wheel",BindingFlags.NonPublic)!;
        var wheel=Activator.CreateInstance(wheelType,true)!;
        var step=wheelType.GetMethod("Step",BindingFlags.Instance|
            BindingFlags.NonPublic)!;
        Assert((int)step.Invoke(wheel,new object[]{60})==0,
            "Fine-scroll first 60 delta must accumulate");
        Assert((int)step.Invoke(wheel,new object[]{60})==-1,
            "Fine-scroll next 60 delta must produce one item");
        Assert((int)step.Invoke(wheel,new object[]{-120})==1,
            "Reverse mouse wheel must shift one item backward");
        var board=new Canvas{Width=700,Height=700};
        var sectorType=Product.GetType("DeskSpace.DeskSpaceSubArcGeometryV48",true)!;
        var sector=sectorType.GetMethod("Sector",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        for(int i=0;i<5;i++)
        {
            Geometry wedge=(Geometry)sector.Invoke(null,new object[]{
                0,-140.0+i*20,-120.0+i*20})!;
            board.Children.Add(new System.Windows.Shapes.Path{
                Data=wedge,Fill=Brushes.SlateBlue,Stroke=Brushes.White,
                StrokeThickness=2});
        }
        var indicators=type.GetMethod("DrawHiddenCounters",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var midpoint=type.GetMethod("DividerMidpoint",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var dividerAngle=type.GetMethod("DividerAngle",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        int shifted=0;
        indicators.Invoke(null,new object[]{board,middle,350.0,
            (Action<int>)(d=>shifted=d)});
        var buttons=board.Children.OfType<Button>().ToArray();
        var outlines=board.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>!p.IsHitTestVisible &&
                (p.Tag as string)!="DeskSpaceHiddenCountInline").ToArray();
        Assert(!board.Children.OfType<Border>().Any(),
            "Hidden counters must have NO separate background box");
        Assert(outlines.Length==2,
            "Both end dividers must have genuine segmented outline strokes");
        Assert(buttons.Length==2 && (string)buttons[0].Content=="4" &&
            (string)buttons[1].Content=="3",
            "Hidden counts must be numerals embedded in radial end lines");
        var inlineInk=board.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>(p.Tag as string)=="DeskSpaceHiddenCountInline")
            .ToArray();
        Assert(inlineInk.Length==2 && buttons.All(b=>b.Opacity==0),
            "R72: hidden-count number must be actual divider ink, not an overlay");
        foreach(var part in inlineInk)
        {
            Assert(part.Data is GeometryGroup g &&
                g.Children.Count==3 &&
                g.Children[0] is EllipseGeometry &&
                g.Children[1] is Geometry &&
                g.Children[2] is EllipseGeometry,
                "R72 line-dots-number-dots-line must be ONE drawn geometry");
            var group=(GeometryGroup)part.Data;
            var topDot=(EllipseGeometry)group.Children[0];
            var bottomDot=(EllipseGeometry)group.Children[2];
            Assert(Math.Abs((topDot.Center-bottomDot.Center).Length-26)<.1,
                "R72 dots must be one shared line, not floating labels");
            var countInk=((SolidColorBrush)part.Fill).Color;
            Assert((countInk.R<60 || countInk.R>190) &&
                part.Stroke==null && part.IsHitTestVisible==false,
                "R77 hidden-count ink must be high contrast for light and dark wedges");
        }
        for(int side=0;side<2;side++)
        {
            var wedge=(System.Windows.Shapes.Path)board.Children[side==0?0:4];
            var point=(Point)midpoint.Invoke(null,new object[]{wedge.Data,
                side==0})!;
            double x=Canvas.GetLeft(buttons[side])+buttons[side].Width/2;
            double y=Canvas.GetTop(buttons[side])+buttons[side].Height/2;
            Assert(Math.Abs(x-point.X)<.05 && Math.Abs(y-point.Y)<.05,
                "Hidden count not centered on actual vertical/radial end line");
            double rotated=(buttons[side].RenderTransform as RotateTransform)?
                .Angle??double.NaN;
            double expected=(double)dividerAngle.Invoke(null,new object[]{
                wedge.Data,side==0})!;
            Assert(Math.Abs(rotated-expected)<.001 &&
                buttons[side].RenderTransformOrigin==new Point(.5,.5),
                "R70 hidden count does not rotate with its actual divider");
            Assert(wedge.Stroke==null &&
                buttons[side].BorderThickness.Left==0 &&
                buttons[side].Background==Brushes.Transparent &&
                buttons[side].Template!=null &&
                buttons[side].FontWeight==FontWeights.Bold &&
                buttons[side].FontSize>=12.0,
                "Counter must be a clear numeral without a rectangular button plate");
            var gap=(PathGeometry)outlines[side].Data;
            Assert(gap.Figures.Count==5 &&
                gap.Figures.All(f=>!f.IsClosed && !f.IsFilled),
                "Divider stroke was not split around the numeral");
            var radialEnds=gap.Figures
                .Where(f=>f.Segments.Count==1 &&
                    f.Segments[0] is LineSegment)
                .SelectMany(f=>new[]{
                    f.StartPoint,((LineSegment)f.Segments[0]).Point})
                .ToArray();
            int nearest=radialEnds.Count(p=>
                Math.Abs((p-point).Length-14.9)<.1);
            Assert(nearest==2,
                "Actual line gap must have two endpoints beside the numeral");
            // R71: the two divider endpoints must TOUCH the outside of
            // the two small dots rather than stop short or cross their centers.
            var delta=(RotateTransform)buttons[side].RenderTransform;
            var top=delta.Transform(new Point(0,-13));
            var bottom=delta.Transform(new Point(0,13));
            var centers=new[]{
                new Point(point.X+top.X,point.Y+top.Y),
                new Point(point.X+bottom.X,point.Y+bottom.Y)};
            var outlineEnds=radialEnds.Where(p=>
                Math.Abs((p-point).Length-14.9)<.1).ToArray();
            Assert(outlineEnds.Length==2 &&
                centers.All(dotCenter=>outlineEnds.Any(end=>
                    Math.Abs((end-dotCenter).Length-1.9)<.1)),
                "R71 divider must end at the top dot and restart at bottom dot");
        }
        // Dark numerals on pastel, light numerals on selected dark wedges.
        // All must have at least 4.5:1 contrast, independent of wedge hue.
        var inkMethod=type.GetMethod("CounterInk",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var ratioType=Product.GetType("DeskSpace.DeskSpaceWeatherContrastV64",true)!;
        var ratio=ratioType.GetMethod("ContrastRatio",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var bg in new[]{
            Color.FromRgb(222,229,247),
            Color.FromRgb(151,135,222),
            Color.FromRgb(32,45,78)})
        {
            var ink=(Color)inkMethod.Invoke(null,
                new object[]{new SolidColorBrush(bg)})!;
            double score=(double)ratio.Invoke(null,new object[]{ink,bg})!;
            Assert(score>=4.5,
                "Embedded divider numeral lacks 4.5:1 contrast for light/dark wedges");
        }
        Render(board,700,700,"subdonut-inline-divider-counts");
        Render(board,700,700,"subdonut-oriented-hidden-counts");
        foreach(var hiddenButton in buttons)
        {
            hiddenButton.ApplyTemplate();
            var dots=Walk(hiddenButton)
                .OfType<System.Windows.Shapes.Ellipse>().ToArray();
            Assert(dots.Length==2,
                "R70 hidden count must have exactly one dot above and below");
            var translations=dots.Select(dot=>dot.RenderTransform)
                .OfType<TranslateTransform>()
                .OrderBy(tr=>tr.Y).ToArray();
            Assert(translations.Length==2 &&
                Math.Abs(translations[0].Y+13)<.1 &&
                Math.Abs(translations[1].Y-13)<.1 &&
                translations.All(x=>Math.Abs(x.X)<.01),
                "R70 hidden-count dots must follow the divider, not the page");
        }
        // R69: reproduce the user's real failure: a purple +New end wedge
        // on WHITE rather than the transparent/black synthetic screenshot.
        // The full numeral must remain legible where it straddles the edge.
        var whiteScene=new Canvas{Width=700,Height=700,
            Background=Brushes.White};
        Geometry? lastGeometry=null;
        for(int i=0;i<5;i++)
        {
            Geometry wedge=(Geometry)sector.Invoke(null,new object[]{
                0,-140.0+i*20,-120.0+i*20})!;
            if(i==4)lastGeometry=wedge;
            whiteScene.Children.Add(new System.Windows.Shapes.Path{
                Data=wedge,
                Fill=i==4
                    ? new SolidColorBrush(Color.FromRgb(98,80,182))
                    : new SolidColorBrush(Color.FromRgb(220,229,252)),
                Stroke=Brushes.White,StrokeThickness=2});
        }
        var twoHidden=View(7,0,-90);
        int whiteSceneShift=0;
        indicators.Invoke(null,new object[]{whiteScene,twoHidden,350.0,
            (Action<int>)(d=>whiteSceneShift=d)});
        var twoButton=whiteScene.Children.OfType<Button>().Single();
        var singleInk=whiteScene.Children.OfType<System.Windows.Shapes.Path>()
            .Single(p=>(p.Tag as string)=="DeskSpaceHiddenCountInline");
        Assert(twoButton.Opacity==0 &&
            singleInk.Data is GeometryGroup &&
            ((GeometryGroup)singleInk.Data).Children.Count==3,
            "R72: purple end count must be drawn with its divider, not in button overlay");
        Assert((string)twoButton.Content=="2" && 
            !whiteScene.Children.OfType<Border>().Any(),
            "R69 purple end-line numeral has a separate badge or wrong count");
        var textPoint=(Point)midpoint.Invoke(null,new object[]{
            lastGeometry!,false})!;
        Assert(Math.Abs(Canvas.GetLeft(twoButton)+twoButton.Width/2-
            textPoint.X)<.05 &&
            Math.Abs(Canvas.GetTop(twoButton)+twoButton.Height/2-
            textPoint.Y)<.05,
            "R69 number moved off its actual divider midpoint");
        Assert(Math.Abs(((RotateTransform)twoButton.RenderTransform).Angle-
            (double)dividerAngle.Invoke(null,new object[]{lastGeometry!,false})!)<.01,
            "R70 end count must follow +New divider orientation");
        Render(whiteScene,700,700,"subdonut-purple-new-white-background");
        Render(whiteScene,700,700,"subdonut-purple-oriented-count-dots");
        Render(whiteScene,700,700,"subdonut-dot-joined-divider");
        Render(whiteScene,700,700,"subdonut-actual-inline-count-ink");
        Render(whiteScene,700,700,"subdonut-r74-no-extra-lines");
        Render(whiteScene,700,700,"subdonut-r77-inset-count");
        Assert(whiteScene.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>(p.Tag as string)=="DeskSpaceHiddenCountInline")
            .All(p=>p.Data is GeometryGroup group &&
                group.Children.Count==3 && p.Stroke==null),
            "R74: hidden count must NOT add extra divider lines");
        var integrated=(GeometryGroup)singleInk.Data;
        var digits=(Geometry)integrated.Children[1];
        var centerOfDigits=new Point(
            digits.Bounds.Left+digits.Bounds.Width/2,
            digits.Bounds.Top+digits.Bounds.Height/2);
        Assert(lastGeometry!.FillContains(centerOfDigits) &&
            (centerOfDigits-textPoint).Length>=4.5 &&
            ((SolidColorBrush)singleInk.Fill).Color.R>190,
            "R77 hidden numeral must be inlayed INTO colored wedge, not halfway outside");
        var preview=new RenderTargetBitmap(700,700,96,96,
            PixelFormats.Pbgra32);
        preview.Render(whiteScene);
        var raster=new byte[700*700*4];
        preview.CopyPixels(raster,700*4,0);
        int darkInkOutside=0;
        for(int y=(int)textPoint.Y-10;y<=(int)textPoint.Y+10;y++)
        for(int x=(int)textPoint.X-12;x<=(int)textPoint.X+12;x++)
        {
            if(x<0||y<0||x>=700||y>=700)continue;
            int k=(y*700+x)*4;
            bool navyOutline=raster[k]<95 && raster[k+1]<90 &&
                raster[k+2]<85 && raster[k+3]>180;
            if(navyOutline && !lastGeometry!.FillContains(
                new Point(x+.5,y+.5))) darkInkOutside++;
        }
        Assert(darkInkOutside<=2,
            "R77 inlaid numeral must not bleed onto white outside purple end wedge");
        twoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,twoButton));
        Assert(whiteSceneShift==1,"R69 clear numeral must still click-to-scroll");
        Console.WriteLine("R69 WPF PASS: full purple end-line count readable against white background without a badge.");

        // A clipped Popup arc stores translated sector geometry; the same
        // standard must honor its transform at any submenu depth.
        var translated=(Geometry)sector.Invoke(null,new object[]{2,40.0,62.0})!;
        var plain=(Point)midpoint.Invoke(null,new object[]{translated,false})!;
        translated.Transform=new TranslateTransform(-145,55);
        var moved=(Point)midpoint.Invoke(null,new object[]{translated,false})!;
        Assert(Math.Abs(moved.X-plain.X+145)<.01 &&
            Math.Abs(moved.Y-plain.Y-55)<.01,
            "Divider counter ignores clipped nested-Popup geometry transform");
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent,buttons[0]));
        Assert(shifted==-1,
            "Clicking the embedded left-line numeral failed to scroll");
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
        Assert(Math.Abs(centre-(-90+4*360.0/6))<.001,
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