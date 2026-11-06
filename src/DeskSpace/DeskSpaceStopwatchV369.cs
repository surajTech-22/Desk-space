using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DeskSpace;

// R369: stopwatch is a VIEW in the existing widget, never a second widget
// process, desktop overlay, independent tray, or fast-running background loop.
// Its clock is monotonic and remains accurate when the UI is closed/hidden.
internal sealed class DeskSpaceStopwatchV369 : IDisposable
{
    private static readonly object Sync=new();
    private static readonly Stopwatch Clock=new();
    private static readonly List<TimeSpan> LapSplits=new();
    private static event Action? StateChanged;
    private readonly DispatcherTimer redraw=new(){
        Interval=TimeSpan.FromMilliseconds(50)
    };
    private readonly Action returnToCircle;
    private readonly Canvas dial=new(){Width=540,Height=584};
    private TextBlock? timeDisplay;
    private TextBlock? lapCaption;
    private System.Windows.Shapes.Path? progressArc;
    private Line? subHand;
    private Button? lapButton;
    private string lastPainted="";
    private bool disposed;
    private bool returnRequestedV371;

    // R371: one reliable navigation action shared by crown and Escape.
    private void BackToPreviousV371()
    {
        if(disposed || returnRequestedV371)return;
        returnRequestedV371=true;
        returnToCircle();
    }

    internal DeskSpaceStopwatchV369(Action returnToCircle)
    {
        this.returnToCircle=returnToCircle;
        redraw.Tick+=(_,_)=>Render();
        StateChanged+=OnStateChanged;
    }

    internal static string CompactElapsed
    {
        get {lock(Sync)return FormatTime(Clock.Elapsed);}
    }
    internal static bool IsRunning
    {
        get {lock(Sync)return Clock.IsRunning;}
    }
    private static string FormatTime(TimeSpan elapsed)
    {
        long centis=elapsed.Ticks/TimeSpan.TicksPerMillisecond/10;
        long seconds=centis/100;
        if(seconds>=6000)
            return string.Format(CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}",seconds/3600,(seconds/60)%60,seconds%60);
        return string.Format(CultureInfo.InvariantCulture,
            "{0:00}:{1:00}.{2:00}",seconds/60,seconds%60,centis%100);
    }
    internal static void Start()
    {
        lock(Sync)Clock.Start();StateChanged?.Invoke();
    }
    internal static void Pause()
    {
        lock(Sync)Clock.Stop();StateChanged?.Invoke();
    }
    internal static void Reset()
    {
        lock(Sync){Clock.Reset();LapSplits.Clear();}
        StateChanged?.Invoke();
    }
    internal static bool Lap()
    {
        bool recorded=false;
        lock(Sync)
        {
            if(Clock.IsRunning)
            {
                LapSplits.Add(Clock.Elapsed);
                recorded=true;
            }
        }
        if(recorded)StateChanged?.Invoke();
        return recorded;
    }
    internal static IReadOnlyList<TimeSpan> Laps
    {
        get{lock(Sync)return LapSplits.ToArray();}
    }
    private void OnStateChanged()
    {
        if(disposed)return;
        if(dial.Dispatcher.CheckAccess())Render();
        else _=dial.Dispatcher.BeginInvoke(new Action(Render));
    }
    private static Brush Solid(byte r,byte g,byte b)=>
        new SolidColorBrush(Color.FromRgb(r,g,b));
    private static Brush Glow(byte r,byte g,byte b,byte alpha=255)=>
        new SolidColorBrush(Color.FromArgb(alpha,r,g,b));
    private static Brush Gradient(Color top,Color bottom)
    {
        var brush=new LinearGradientBrush{StartPoint=new Point(0,0),
            EndPoint=new Point(1,1)};
        brush.GradientStops.Add(new GradientStop(top,0));
        brush.GradientStops.Add(new GradientStop(bottom,1));
        return brush;
    }
    private static Brush Metal()=>Gradient(Color.FromRgb(230,237,247),
        Color.FromRgb(44,63,91));
    private static RadialGradientBrush Face()
    {
        var brush=new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(28,37,58),0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(9,15,29),1));
        return brush;
    }
    private void Put(UIElement element,double left,double top)
    {
        Canvas.SetLeft(element,left);
        Canvas.SetTop(element,top);
        dial.Children.Add(element);
    }
    private void Circle(double x,double y,double diameter,Brush fill,
        Brush? stroke=null,double strokeWidth=0)
    {
        Put(new Ellipse{Width=diameter,Height=diameter,Fill=fill,
            Stroke=stroke,StrokeThickness=strokeWidth,
            IsHitTestVisible=false},x,y);
    }
    private void Tick(double cx,double cy,double radius,double angle,
        double length,Brush ink,double thickness)
    {
        double radians=(angle-90)*Math.PI/180.0;
        double sine=Math.Sin(radians),cosine=Math.Cos(radians);
        dial.Children.Add(new Line{
            X1=cx+radius*cosine,Y1=cy+radius*sine,
            X2=cx+(radius-length)*cosine,
            Y2=cy+(radius-length)*sine,
            Stroke=ink,StrokeThickness=thickness,
            StrokeStartLineCap=PenLineCap.Round,
            StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false});
    }
    private TextBlock Label(string text,double x,double y,double width,
        double height,double font,Brush ink,FontWeight? weight=null)
    {
        var block=new TextBlock{Text=text,Width=width,Height=height,
            FontFamily=new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize=font,Foreground=ink,
            FontWeight=weight??FontWeights.Normal,
            TextAlignment=TextAlignment.Center,
            VerticalAlignment=VerticalAlignment.Center,
            IsHitTestVisible=false};
        Put(block,x,y);return block;
    }
    private static void AddArc(System.Windows.Shapes.Path path,
        double cx,double cy,double radius,double degrees)
    {
        degrees=Math.Clamp(degrees,0.01,359.9);
        double radians=(degrees-90)*Math.PI/180;
        var start=new Point(cx,cy-radius);
        var end=new Point(cx+radius*Math.Cos(radians),
            cy+radius*Math.Sin(radians));
        var figure=new PathFigure{StartPoint=start,IsClosed=false};
        figure.Segments.Add(new ArcSegment(end,new Size(radius,radius),
            0,degrees>180,SweepDirection.Clockwise,true));
        path.Data=new PathGeometry(new[]{figure});
    }
    private static Brush ButtonFill(Color from,Color to)=>Gradient(from,to);
    private Button RoundButton(string icon,string label,double x,double y,
        Brush fill,Brush edge,Action action)
    {
        // WPF template ensures these are actual accessible Buttons instead
        // of image hitboxes: mouse, keyboard, UI automation and focus work.
        var button=new Button{
            Width=87,Height=87,Background=fill,BorderBrush=edge,
            BorderThickness=new Thickness(2.2),
            Cursor=Cursors.Hand,ToolTip=label,
            FocusVisualStyle=null
        };
        var stack=new StackPanel{
            HorizontalAlignment=HorizontalAlignment.Center,
            VerticalAlignment=VerticalAlignment.Center};
        stack.Children.Add(new TextBlock{Text=icon,
            FontFamily=new FontFamily("Segoe UI Symbol, Segoe UI"),
            FontSize=29,FontWeight=FontWeights.SemiBold,
            TextAlignment=TextAlignment.Center,
            Foreground=Brushes.White,Height=38});
        stack.Children.Add(new TextBlock{Text=label,FontSize=16,
            FontFamily=new FontFamily("Segoe UI Variable, Segoe UI"),
            TextAlignment=TextAlignment.Center,
            Foreground=Brushes.White});
        button.Content=stack;
        var root=new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.CornerRadiusProperty,new CornerRadius(47));
        root.SetValue(Border.BorderThicknessProperty,new Thickness(2.2));
        root.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background"){
                RelativeSource=new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        root.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding("BorderBrush"){
                RelativeSource=new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);
        root.AppendChild(presenter);
        button.Template=new ControlTemplate(typeof(Button)){VisualTree=root};
        button.Effect=new DropShadowEffect{Color=Colors.Black,Opacity=.7,
            BlurRadius=11,ShadowDepth=5};
        button.Click+=(_,_)=>action();
        Put(button,x,y);
        return button;
    }

    internal FrameworkElement Create()
    {
        // The exact approved analogue/digital stopwatch arrangement:
        // steel case/crown, numbered 60-second dial and cyan indicator,
        // small running subdial, digital readout and four circular buttons.
        var outer=new Grid{Width=540,Height=584,Background=Brushes.Transparent};
        outer.Children.Add(dial);
        outer.Focusable=true;
        outer.PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){BackToPreviousV371();e.Handled=true;}
            else if(e.Key==Key.Space){
                if(IsRunning)Pause();else Start();e.Handled=true;
            }
            else if(e.Key==Key.R){Reset();e.Handled=true;}
            else if(e.Key==Key.L){Lap();e.Handled=true;}
        };
        Circle(217,4,106,Metal(),Glow(188,216,255),3);
        Circle(230,17,80,Solid(8,14,26));
        // The polished mechanical top crown doubles as Back to Circle.
        // No extra close button intrudes into the approved dial design.
        // R371: the decorative Border only listened for MouseLeftButtonUp,
        // which is not a reliable input control inside the nested scaled
        // Viewbox. Make the exact same crown a genuine keyboard/mouse
        // accessible WPF Button and keep it above the dial in hit-testing.
        var crown=new Button{Width=58,Height=60,
            Background=Metal(),BorderBrush=Glow(213,233,255),
            BorderThickness=new Thickness(1.5),Cursor=Cursors.Hand,
            ToolTip="Back to DeskSpace circle",FocusVisualStyle=null};
        var crownVisual=new FrameworkElementFactory(typeof(Border));
        crownVisual.SetValue(Border.CornerRadiusProperty,
            new CornerRadius(13));
        crownVisual.SetValue(Border.BorderThicknessProperty,
            new Thickness(1.5));
        crownVisual.SetValue(Border.BackgroundProperty,
            crown.Background);
        crownVisual.SetValue(Border.BorderBrushProperty,
            crown.BorderBrush);
        crown.Template=new ControlTemplate(typeof(Button)){
            VisualTree=crownVisual};
        System.Windows.Automation.AutomationProperties.SetName(
            crown,"Back to DeskSpace circle");
        crown.Click+=(_,_)=>BackToPreviousV371();
        Put(crown,241,29);
        Canvas.SetZIndex(crown,100);
        for(int i=0;i<8;i++)
        {
            var rib=new Line{X1=248+i*6,Y1=34,X2=248+i*6,Y2=73,
                Stroke=Glow(53,72,103),StrokeThickness=2,
                IsHitTestVisible=false};
            dial.Children.Add(rib);
            Canvas.SetZIndex(rib,101);
        }
        for(int i=0;i<2;i++)
        {
            var pusher=new Border{Width=42,Height=38,
                CornerRadius=new CornerRadius(8),Background=Metal(),
                BorderBrush=Glow(200,227,255),
                BorderThickness=new Thickness(1.4),
                RenderTransformOrigin=new Point(.5,.5),
                RenderTransform=new RotateTransform(i==0?-28:28),
                IsHitTestVisible=false};
            Put(pusher,i==0?91:408,93);
        }
        // Body and luminous bezel.
        Circle(15,64,510,Gradient(Color.FromRgb(218,228,244),
            Color.FromRgb(42,59,88)),Glow(218,239,255),3);
        Circle(26,75,488,Gradient(Color.FromRgb(8,17,34),
            Color.FromRgb(41,68,112)),Glow(92,162,224),2.5);
        Circle(37,86,466,Face(),Glow(10,26,46),3);
        Circle(49,98,442,Brushes.Transparent,Glow(40,129,209,125),3.2);

        const double cx=270,cy=319;
        for(int i=0;i<60;i++)
            Tick(cx,cy,215,i*6,i%5==0?14:6,
                i%5==0?Glow(230,241,255):Glow(143,171,203),i%5==0?2.3:1.2);
        foreach(int value in new[]{0,5,10,15,20,40,45,50,55})
        {
            var angle=(value*6-90)*Math.PI/180;
            double x=cx+188*Math.Cos(angle)-18;
            double y=cy+188*Math.Sin(angle)-15;
            Label(value==0?"60":value.ToString(CultureInfo.InvariantCulture),
                x,y,36,30,21,Glow(233,244,255));
        }
        var ringGuide=new Ellipse{Width=458,Height=458,
            Stroke=Glow(24,108,171,90),StrokeThickness=7,
            Fill=Brushes.Transparent,IsHitTestVisible=false};
        Put(ringGuide,cx-229,cy-229);
        progressArc=new System.Windows.Shapes.Path{
            Stroke=Glow(27,182,255),StrokeThickness=7,
            StrokeStartLineCap=PenLineCap.Round,
            StrokeEndLineCap=PenLineCap.Round,
            IsHitTestVisible=false,
            Effect=new DropShadowEffect{
                Color=Color.FromRgb(17,165,255),BlurRadius=9,
                Opacity=.75,ShadowDepth=0}};
        dial.Children.Add(progressArc);
        Circle(cx-7,cy-229-7,14,Glow(75,221,255));

        // Top miniature mechanical subdial.
        const double scy=220;
        Circle(cx-52,scy-52,104,
            Gradient(Color.FromRgb(194,210,233),Color.FromRgb(51,69,105)));
        Circle(cx-48,scy-48,96,Face(),Glow(132,164,198),1.5);
        for(int i=0;i<60;i++)
            Tick(cx,scy,43,i*6,i%5==0?6:3,
                Glow(223,236,248),i%5==0?1.2:.6);
        foreach(var pair in new[]{(0,"60"),(15,"15"),(30,"30"),(45,"45")})
        {
            double radians=(pair.Item1*6-90)*Math.PI/180;
            Label(pair.Item2,cx+30*Math.Cos(radians)-11,
                scy+30*Math.Sin(radians)-10,22,20,12,Brushes.White);
        }
        subHand=new Line{X1=cx,Y1=scy,X2=cx,Y2=scy-34,
            Stroke=Glow(57,201,255),StrokeThickness=2.5,
            StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false};
        dial.Children.Add(subHand);
        Circle(cx-5,scy-5,10,Glow(207,218,236));
        Label("S T O P W A T C H",120,277,300,30,18,
            Glow(240,246,255),FontWeights.Medium);
        // Dark inset digital readout, not an additional window.
        var displayPanel=new Border{Width=422,Height=90,
            Background=Gradient(Color.FromRgb(16,25,44),
                Color.FromRgb(6,11,21)),
            BorderBrush=Glow(73,113,171),BorderThickness=new Thickness(1.6),
            CornerRadius=new CornerRadius(45),
            Effect=new DropShadowEffect{Color=Colors.Black,
                Opacity=.5,BlurRadius=12,ShadowDepth=3}};
        Put(displayPanel,59,322);
        timeDisplay=Label("00:00.00",68,329,404,76,60,
            Glow(244,249,255),FontWeights.SemiBold);
        timeDisplay.FontFamily=new FontFamily("Consolas");
        lapCaption=Label("",110,407,320,28,12,Glow(172,201,234));

        RoundButton("▶","Start",66,438,
            ButtonFill(Color.FromRgb(34,125,86),Color.FromRgb(3,42,37)),
            Glow(84,255,179),Start);
        RoundButton("Ⅱ","Pause",173,438,
            ButtonFill(Color.FromRgb(55,71,99),Color.FromRgb(12,20,36)),
            Glow(150,185,231),Pause);
        RoundButton("↶","Reset",280,438,
            ButtonFill(Color.FromRgb(50,62,86),Color.FromRgb(11,17,32)),
            Glow(142,156,192),Reset);
        lapButton=RoundButton("⚑","Lap",387,438,
            ButtonFill(Color.FromRgb(87,56,139),Color.FromRgb(27,15,60)),
            Glow(192,126,255),()=>Lap());
        // Subtle bottom bezel glow from the approved reference.
        var bottomDash=new Line{X1=245,Y1=550,X2=295,Y2=550,
            Stroke=Glow(56,192,255),StrokeThickness=4,
            StrokeStartLineCap=PenLineCap.Round,
            StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false,
            Effect=new DropShadowEffect{Color=Colors.DeepSkyBlue,
                BlurRadius=8,Opacity=.8,ShadowDepth=0}};
        dial.Children.Add(bottomDash);
        outer.Loaded+=(_,_)=>{if(!disposed){Render();outer.Focus();}};
        Render();
        return outer;
    }

    private void Render()
    {
        if(disposed||timeDisplay==null)return;
        TimeSpan elapsed;
        TimeSpan[] laps;
        bool running;
        lock(Sync){
            elapsed=Clock.Elapsed;
            running=Clock.IsRunning;
            laps=LapSplits.ToArray();
        }
        string time=FormatTime(elapsed);
        if(time!=lastPainted){
            timeDisplay.Text=time;
            lastPainted=time;
            if(progressArc!=null)
            {
                double sec=elapsed.TotalSeconds%60;
                if(sec<.005)progressArc.Visibility=Visibility.Collapsed;
                else{
                    progressArc.Visibility=Visibility.Visible;
                    AddArc(progressArc,270,319,229,sec*6);
                }
            }
            if(subHand!=null)
            {
                double angle=(elapsed.TotalSeconds%60)*Math.PI/30-Math.PI/2;
                subHand.X2=270+34*Math.Cos(angle);
                subHand.Y2=220+34*Math.Sin(angle);
            }
        }
        if(lapCaption!=null)
        {
            string caption="";
            if(laps.Length>0)
            {
                var split=laps.Length==1?laps[0]:laps[^1]-laps[^2];
                caption="LAP "+laps.Length.ToString("00",CultureInfo.InvariantCulture)+
                    "   "+FormatTime(split);
            }
            if(lapCaption.Text!=caption)lapCaption.Text=caption;
        }
        if(lapButton!=null)
        {
            string tip=laps.Length==0?"Lap (L) — record split while running":
                "Lap (L) — recent splits:\n"+string.Join("\n",
                    laps.TakeLast(8).Select((t,i)=>
                        "Lap "+(laps.Length-Math.Min(8,laps.Length)+i+1)+
                        ": "+FormatTime(t-(i==0?
                            (laps.Length>8?laps[^9]:TimeSpan.Zero):
                            laps[laps.Length-Math.Min(8,laps.Length)+i-1]))));
            if(!Equals(lapButton.ToolTip,tip))lapButton.ToolTip=tip;
        }
        if(running&&!redraw.IsEnabled)redraw.Start();
        if(!running&&redraw.IsEnabled)redraw.Stop();
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        redraw.Stop();
        StateChanged-=OnStateChanged;
    }
}
