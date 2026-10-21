using System;
using System.Collections.Generic;
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
    private readonly List<TranslateTransform> cloudMovements=new();
    private TranslateTransform? rainMovement;
    private Ellipse? star;
    private readonly List<UIElement> nightAnimations=new();
    public DeskSpaceWeatherSceneV11(WeatherReading reading,double diameter)
    {
        Width=Height=diameter;
        IsHitTestVisible=false;
        // Draw into the ENTIRE host circle; the old 185/272 scaling made
        // weather look like a semicircle even before the animation ran.
        Clip=new EllipseGeometry(new Point(diameter/2,diameter/2),
            diameter/2-3,diameter/2-3);
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
    // Unlike a ping-pong weather icon, this hardware-composited animation
    // moves the artwork THROUGH the full circular viewport in one direction.
    // The loop boundary lies beyond both edges so reset is never visible.
    private static DoubleAnimation Flow(double from,double to,double seconds,
        double delay=0)
        =>new(from,to,TimeSpan.FromSeconds(seconds)){
            BeginTime=TimeSpan.FromSeconds(delay),
            AutoReverse=false,RepeatBehavior=RepeatBehavior.Forever,
            EasingFunction=null
        };
    private void SunCore(double d,double x,double y,double factor)
    {
        var size=d*factor;
        var light=Disc(size*2.3,Glow(Color.FromRgb(255,228,112)),.9);
        Draw(light,x-size*.65,y-size*.65);
        light.BeginAnimation(UIElement.OpacityProperty,Motion(.35,.95,3.4));
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
            new DoubleAnimation(0,360,TimeSpan.FromSeconds(18)){
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
        var movement=new TranslateTransform();
        cloudMovements.Add(movement);
        cloud.RenderTransform=movement;
        Draw(cloud,x,y);
        movement.BeginAnimation(TranslateTransform.XProperty,
            Flow(-d*1.15,d*1.15,18.0,0));
    }
    private void Sunny(double d)
    {
        SunCore(d,d*.55,d*.12,.30);
        // A small drifting cloud complements the sun without hiding the clock text.
        CloudCore(d,d*.09,d*.61,1.08);
        Draw(Disc(d*.26,Glow(Color.FromRgb(255,208,116)),.4),d*.04,d*.58);
    }
    private void Cloudy(double d)
    {
        Draw(Disc(d*.61,Glow(Color.FromRgb(169,220,255)),.64),d*.39,d*.01);
        CloudCore(d,d*.23,d*.10,1.42);
        Draw(Disc(d*.37,Glow(Color.FromRgb(209,204,255)),.55),d*.0,d*.61);
    }
    private void Rain(double d)
    {
        Draw(Disc(d*.71,Glow(Color.FromRgb(111,187,255)),.72),d*.28,-d*.05);
        CloudCore(d,d*.19,d*.11,1.48,true);
        var drops=new Canvas{Width=d*.88,Height=d*.61,Opacity=.80};
        var brush=new SolidColorBrush(Color.FromArgb(210,183,226,255));
        for(int i=0;i<20;i++)
        {
            double xx=d*(.03+(i%10)*.077);
            double yy=d*(.04+(i/10)*.29+(i%3)*.022);
            var line=new Line{X1=xx,Y1=yy,X2=xx-d*.026,
                Y2=yy+d*.089,Stroke=brush,
                StrokeThickness=Math.Max(1.6,d*.009),StrokeStartLineCap=PenLineCap.Round};
            drops.Children.Add(line);
        }
        rainMovement=new TranslateTransform();
        drops.RenderTransform=rainMovement;
        Draw(drops,d*.10,d*.24);
        rainMovement.BeginAnimation(TranslateTransform.YProperty,
            Flow(-d*.65,d*.64,3.8));
    }
    private void Night(double d)
    {
        // The previous version had one tiny twinkling dot: on a 116px widget
        // the scene appeared motionless. Make ambient movement clearly visible.
        var wash=Disc(d*.83,Glow(Color.FromRgb(103,128,255)),.54);
        Draw(wash,d*.20,-d*.10);
        wash.BeginAnimation(UIElement.OpacityProperty,Motion(.23,.64,3.7));
        nightAnimations.Add(wash);
        var moonGlow=Disc(d*.51,Glow(Color.FromRgb(239,241,255)),.86);
        Draw(moonGlow,d*.45,d*.01);
        moonGlow.BeginAnimation(UIElement.OpacityProperty,Motion(.34,.95,2.8));
        nightAnimations.Add(moonGlow);
        var moon=Disc(d*.28,new RadialGradientBrush(
            Color.FromRgb(255,250,207),Color.FromRgb(185,211,249)) {
                RadiusX=.64,RadiusY=.64
            });
        Draw(moon,d*.59,d*.10);
        for(int i=0;i<9;i++)
        {
            var size=d*(.013+(i%3)*.008);
            var x=d*(.10+(i*.119)%0.78);
            var y=d*(.12+(i*.149)%0.48);
            var dot=Disc(size,Brushes.White,.92);
            Draw(dot,x,y);
            dot.BeginAnimation(OpacityProperty,Motion(i%2==0?.12:.35,1,1.7+(i%4)*.53));
            nightAnimations.Add(dot);
            if(i==2)star=dot;
        }
        // A recognizable cloud moves across the moon: real directional motion.
        CloudCore(d,d*.03,d*.46,1.1,true);
    }
    public void StopMotion()
    {
        sunRotation?.BeginAnimation(RotateTransform.AngleProperty,null);
        foreach(var movement in cloudMovements)
            movement.BeginAnimation(TranslateTransform.XProperty,null);
        cloudMovements.Clear();
        rainMovement?.BeginAnimation(TranslateTransform.YProperty,null);
        star?.BeginAnimation(OpacityProperty,null);
        foreach(var light in nightAnimations)light.BeginAnimation(UIElement.OpacityProperty,null);
        nightAnimations.Clear();
    }
}