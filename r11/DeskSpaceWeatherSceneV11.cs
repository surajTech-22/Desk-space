using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DeskSpace;

// Vector weather illustrations painted INSIDE the circular clock.
// Three slow compositor animations max; no frame timers, network calls or raster work.
internal sealed class DeskSpaceWeatherSceneV11 : Canvas
{
    private RotateTransform? sunRotation;
    private TranslateTransform? cloudMovement;
    private TranslateTransform? rainMovement;
    private Ellipse? star;
    public DeskSpaceWeatherSceneV11(WeatherReading reading,double diameter)
    {
        Width=Height=diameter;
        IsHitTestVisible=false;
        Clip=new EllipseGeometry(new Point(diameter/2,diameter/2),diameter/2-7,diameter/2-7);
        var code=reading.Code;
        if(!reading.IsDay)Night(diameter);
        else if(code>=51)Rain(diameter);
        else if(code<=1)Sunny(diameter);
        else Cloudy(diameter);
    }
    private void Draw(UIElement element,double x,double y)
    {
        Canvas.SetLeft(element,x);Canvas.SetTop(element,y);
        Children.Add(element);
    }
    private static Ellipse Disc(double d,Brush fill,double opacity=1)=>new(){
        Width=d,Height=d,Fill=fill,Opacity=opacity,IsHitTestVisible=false};
    private static Brush Glow(Color centre)
    {
        return new RadialGradientBrush(
            new GradientStopCollection{
                new GradientStop(Color.FromArgb(225,centre.R,centre.G,centre.B),0),
                new GradientStop(Color.FromArgb(80,centre.R,centre.G,centre.B),0.48),
                new GradientStop(Color.FromArgb(0,centre.R,centre.G,centre.B),1)
            }){RadiusX=0.5,RadiusY=0.5,Center=new Point(.5,.5)};
    }
    private static DoubleAnimation Motion(double from,double to,double seconds)
        =>new(from,to,TimeSpan.FromSeconds(seconds)){
            AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,
            EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}
        };
    private void SunCore(double d,double x,double y,double factor)
    {
        var size=d*factor;
        var light=Disc(size*2.3,Glow(Color.FromRgb(255,228,112)),.9);
        Draw(light,x-size*.65,y-size*.65);
        var rays=new Canvas{Width=size*1.75,Height=size*1.75,IsHitTestVisible=false};
        sunRotation=new RotateTransform(0,rays.Width/2,rays.Height/2);
        rays.RenderTransform=sunRotation;
        for(int i=0;i<8;i++)
        {
            double theta=i*Math.PI/4;
            var radius=size*.59;
            var startX=rays.Width/2+radius*Math.Cos(theta);
            var startY=rays.Height/2+radius*Math.Sin(theta);
            var endX=rays.Width/2+radius*1.4*Math.Cos(theta);
            var endY=rays.Height/2+radius*1.4*Math.Sin(theta);
            var ray=new Line{X1=startX,Y1=startY,X2=endX,Y2=endY,
                Stroke=new SolidColorBrush(Color.FromArgb(215,255,241,168)),
                StrokeThickness=Math.Max(2,size*.085),StrokeStartLineCap=PenLineCap.Round,
                StrokeEndLineCap=PenLineCap.Round};
            rays.Children.Add(ray);
        }
        Draw(rays,x-size*.38,y-size*.38);
        Draw(Disc(size, new RadialGradientBrush(Color.FromRgb(255,250,176),
            Color.FromRgb(255,181,61)) { RadiusX=.6,RadiusY=.6 }),x,y);
        sunRotation.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0,360,TimeSpan.FromSeconds(56)){
                RepeatBehavior=RepeatBehavior.Forever});
    }
    private void CloudCore(double d,double x,double y,double scale,bool dark=false)
    {
        var cloud=new Canvas{Width=d*.53*scale,Height=d*.30*scale,Opacity=.86};
        var soft=dark?Color.FromRgb(196,210,244):Color.FromRgb(252,250,255);
        var shade=dark?Color.FromRgb(128,158,212):Color.FromRgb(210,218,245);
        var fill=new LinearGradientBrush(soft,shade,90);
        void Ell(double xx,double yy,double w,double h,double opacity=1)
        {
            var ellipse=new Ellipse{Width=w,Height=h,Fill=fill,Opacity=opacity};
            Canvas.SetLeft(ellipse,xx);Canvas.SetTop(ellipse,yy);cloud.Children.Add(ellipse);
        }
        var unit=d*scale;
        Ell(unit*.03,unit*.11,unit*.26,unit*.14);
        Ell(unit*.12,unit*.045,unit*.24,unit*.21);
        Ell(unit*.27,unit*.09,unit*.21,unit*.17);
        Ell(unit*.11,unit*.15,unit*.34,unit*.09);
        cloudMovement=new TranslateTransform();
        cloud.RenderTransform=cloudMovement;
        Draw(cloud,x,y);
        cloudMovement.BeginAnimation(TranslateTransform.XProperty,
            Motion(-d*.015,d*.028,11));
    }
    private void Sunny(double d)
    {
        SunCore(d,d*.68,d*.115,.21);
        // A small drifting cloud complements the sun without hiding the clock text.
        CloudCore(d,d*.11,d*.74,.85);
        Draw(Disc(d*.26,Glow(Color.FromRgb(255,208,116)),.4),d*.04,d*.58);
    }
    private void Cloudy(double d)
    {
        Draw(Disc(d*.61,Glow(Color.FromRgb(169,220,255)),.64),d*.39,d*.01);
        CloudCore(d,d*.32,d*.14,1.15);
        Draw(Disc(d*.37,Glow(Color.FromRgb(209,204,255)),.55),d*.0,d*.61);
    }
    private void Rain(double d)
    {
        Draw(Disc(d*.71,Glow(Color.FromRgb(111,187,255)),.72),d*.28,-d*.05);
        CloudCore(d,d*.24,d*.13,1.2,true);
        var drops=new Canvas{Width=d*.80,Height=d*.34,Opacity=.80};
        var brush=new SolidColorBrush(Color.FromArgb(210,183,226,255));
        for(int i=0;i<8;i++)
        {
            double xx=d*(.05+i*.089);
            var line=new Line{X1=xx,Y1=d*(.05+(i%3)*.045),X2=xx-d*.026,
                Y2=d*(.13+(i%3)*.045),Stroke=brush,
                StrokeThickness=Math.Max(1.6,d*.009),StrokeStartLineCap=PenLineCap.Round};
            drops.Children.Add(line);
        }
        rainMovement=new TranslateTransform();
        drops.RenderTransform=rainMovement;
        Draw(drops,d*.11,d*.64);
        rainMovement.BeginAnimation(TranslateTransform.YProperty,
            Motion(-d*.028,d*.035,2.8));
    }
    private void Night(double d)
    {
        Draw(Disc(d*.67,Glow(Color.FromRgb(90,134,245)),.47),d*.30,-d*.05);
        var moon=Disc(d*.22,new RadialGradientBrush(Color.FromRgb(255,251,208),
            Color.FromRgb(202,221,255)) {RadiusX=.7,RadiusY=.7});
        Draw(moon,d*.69,d*.14);
        // Small, ambient stars. One slow opacity animation, not a frame loop.
        for(int i=0;i<6;i++)
        {
            var size=d*(.010+(i%3)*.004);
            var dot=Disc(size,Brushes.White,.75);
            Draw(dot,d*(.19+(i*.124)%0.55),d*(.15+(i*.19)%0.57));
            if(i==2)star=dot;
        }
        if(star!=null)star.BeginAnimation(OpacityProperty,Motion(.28,1,6.5));
        CloudCore(d,d*.09,d*.76,.72,true);
    }
    public void StopMotion()
    {
        sunRotation?.BeginAnimation(RotateTransform.AngleProperty,null);
        cloudMovement?.BeginAnimation(TranslateTransform.XProperty,null);
        rainMovement?.BeginAnimation(TranslateTransform.YProperty,null);
        star?.BeginAnimation(OpacityProperty,null);
    }
}