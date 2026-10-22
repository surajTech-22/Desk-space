using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DeskSpace;

// R38: light-weight, native WPF sky renderer. Every precipitation emitter is
// geometrically a CHILD of its cloud. It shares the cloud's horizontal motion,
// starts below the cloud base, and is clipped to the cloud's precipitation column.
// No WebView, network dependency, timers, frame callbacks or new installed SDK.
internal sealed class DeskSpaceWeatherSceneV11 : Canvas
{
    private readonly List<TranslateTransform> cloudMotion = new();
    private readonly List<TranslateTransform> dropMotion = new();
    private readonly List<UIElement> ambientMotion = new();
    private RotateTransform? sunlightMotion;
    public int CloudInstanceCount { get; private set; }
    public int RainEmitterCount { get; private set; }
    public int SnowEmitterCount { get; private set; }
    public bool IsRainScene { get; }

    public DeskSpaceWeatherSceneV11(WeatherReading reading, double diameter)
    {
        Width=Height=diameter;
        IsHitTestVisible=false;
        Clip=new EllipseGeometry(new Point(diameter/2,diameter/2),
            diameter/2-3,diameter/2-3);
        int code=reading.Code;
        bool day=reading.IsDay;
        bool thunder=code>=95;
        bool snow=(code>=71 && code<=77)||(code>=85 && code<=86);
        bool rain=(code>=51 && code<=67)||(code>=80 && code<=82)||thunder;
        IsRainScene=rain;
        bool clear=code<=1;
        bool overcast=code==3 || code==45 || code==48;
        bool muted=rain||snow||thunder||overcast;
        bool bright=day&&!muted;

        Color top=bright?Color.FromRgb(100,176,234):
            day?Color.FromRgb(44,80,127):Color.FromRgb(19,31,70);
        Color bottom=bright?Color.FromRgb(211,236,250):
            day?Color.FromRgb(121,150,182):Color.FromRgb(59,70,121);
        Draw(new Ellipse { Width=diameter,Height=diameter,
            Fill=new LinearGradientBrush(top,bottom,90),
            IsHitTestVisible=false },0,0);
        // Large soft atmospheric glows simulate depth across the whole sky.
        Draw(Disc(diameter*.96,Glow(day?
            Color.FromRgb(255,247,226):Color.FromRgb(107,138,205)),day?.28:.23),
            -diameter*.26,-diameter*.27);
        Draw(Disc(diameter*.80,Glow(day?
            Color.FromRgb(234,251,255):Color.FromRgb(69,111,188)),.25),
            diameter*.37,diameter*.50);

        if(!day)DrawMoonAndStars(diameter, !muted);
        else if(clear || code==2)DrawSun(diameter, muted);

        // Weather conditions determine clouds and their owned emitters.
        // The same cloud construction renders tiny/large widgets at any scale.
        if(rain)
        {
            Draw(Disc(diameter*.68,Glow(Color.FromRgb(128,164,214)),.35),
                diameter*.34,-diameter*.10);
            CloudBand(diameter,.06,.12,1.26,true,thunder?2:1,18.8);
            CloudBand(diameter,.57,.25,.88,true,thunder?2:1,25.6);
            if(thunder)DrawStormFlash(diameter);
        }
        else if(snow)
        {
            CloudBand(diameter,.09,.09,1.18,true,3,22.6);
            CloudBand(diameter,.57,.27,.79,true,3,28.2);
        }
        else if(overcast)
        {
            CloudBand(diameter,.13,.17,1.24,true,0,20.7);
            CloudBand(diameter,.61,.49,.91,true,0,29.5);
            if(code==45||code==48)DrawMist(diameter);
        }
        else if(clear)
        {
            if(day) CloudBand(diameter,.59,.73,.44,false,0,33.7);
            else CloudBand(diameter,.49,.78,.40,true,0,37.2);
        }
        else
        {
            CloudBand(diameter,.10,.15,1.00,!day,0,21.9);
            CloudBand(diameter,.57,.66,.56,!day,0,30.7);
        }
    }

    private void Draw(UIElement node,double x,double y)
    {
        Canvas.SetLeft(node,x);
        Canvas.SetTop(node,y);
        Children.Add(node);
    }
    private static Ellipse Disc(double d,Brush brush,double opacity=1) =>
        new() { Width=d,Height=d,Fill=brush,Opacity=opacity,
            IsHitTestVisible=false };
    private static Brush Glow(Color center) => new RadialGradientBrush(
        new GradientStopCollection {
            new GradientStop(Color.FromArgb(225,center.R,center.G,center.B),0),
            new GradientStop(Color.FromArgb(85,center.R,center.G,center.B),.48),
            new GradientStop(Color.FromArgb(0,center.R,center.G,center.B),1)
        }) { Center=new Point(.5,.5),RadiusX=.5,RadiusY=.5 };
    private static DoubleAnimation LinearLoop(double from,double to,double seconds) =>
        new(from,to,TimeSpan.FromSeconds(seconds)) {
            AutoReverse=false,RepeatBehavior=RepeatBehavior.Forever,
            EasingFunction=null
        };
    private static DoubleAnimation Pulse(double from,double to,double seconds) =>
        new(from,to,TimeSpan.FromSeconds(seconds)) {
            AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,
            EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}
        };

    private void DrawSun(double d,bool obscured)
    {
        double size=d*.20,x=d*.68,y=d*.19;
        var halo=Disc(d*.47,Glow(Color.FromRgb(255,229,141)),obscured?.29:.68);
        Draw(halo,x-d*.13,y-d*.13);
        halo.BeginAnimation(OpacityProperty,Pulse(.32,obscured?.46:.81,6.8));
        ambientMotion.Add(halo);
        var rays=new Canvas { Width=size*2,Height=size*2,
            IsHitTestVisible=false };
        var rotate=new RotateTransform(0,size,size);
        sunlightMotion=rotate;
        rays.RenderTransform=rotate;
        for(int i=0;i<12;i++)
        {
            double theta=i*Math.PI/6;
            double inner=size*.66,outer=size*.90;
            rays.Children.Add(new Line {
                X1=size+inner*Math.Cos(theta),Y1=size+inner*Math.Sin(theta),
                X2=size+outer*Math.Cos(theta),Y2=size+outer*Math.Sin(theta),
                Stroke=new SolidColorBrush(Color.FromArgb(188,255,244,193)),
                StrokeThickness=Math.Max(1.3,d*.009),
                StrokeStartLineCap=PenLineCap.Round,
                StrokeEndLineCap=PenLineCap.Round
            });
        }
        Draw(rays,x-size*.5,y-size*.5);
        Draw(Disc(size,new RadialGradientBrush(
            Color.FromRgb(255,253,220),Color.FromRgb(255,188,94))),x,y);
        rotate.BeginAnimation(RotateTransform.AngleProperty,
            LinearLoop(0,360,75));
    }
    private void DrawMoonAndStars(double d,bool visible)
    {
        var halo=Disc(d*.50,Glow(Color.FromRgb(216,231,255)),visible?.58:.17);
        Draw(halo,d*.46,-d*.05);
        halo.BeginAnimation(OpacityProperty,Pulse(visible?.32:.10,
            visible?.66:.22,5.8));
        ambientMotion.Add(halo);
        Draw(Disc(d*.22,new RadialGradientBrush(
            Color.FromRgb(255,250,221),Color.FromRgb(179,207,245)),
            visible?.95:.28),d*.67,d*.15);
        if(!visible)return;
        for(int i=0;i<12;i++)
        {
            double x=d*(.12+(i*31%79)/100.0);
            double y=d*(.09+(i*13%51)/100.0);
            var star=Disc(Math.Max(1.2,d*(.009+(i%3)*.005)),
                Brushes.White,.68);
            Draw(star,x,y);
            star.BeginAnimation(OpacityProperty,
                Pulse(.20,.90,1.9+(i%5)*.59));
            ambientMotion.Add(star);
        }
    }
    private void DrawMist(double d)
    {
        for(int i=0;i<3;i++)
        {
            var mist=Disc(d*.65,
                Glow(Color.FromRgb(225,238,250)),.17);
            Draw(mist,d*(-.11+i*.32),d*(.56+i*.055));
        }
    }
    private void DrawStormFlash(double d)
    {
        // Subtle ambient storm lighting; no strobe or seizure-risk flash.
        var light=Disc(d*.47,Glow(Color.FromRgb(204,215,250)),.20);
        Draw(light,d*.51,d*.05);
        light.BeginAnimation(OpacityProperty,Pulse(.10,.27,7.3));
        ambientMotion.Add(light);
    }

    // An entire cloud + its drops moves on ONE transform. Identical cloud
    // copies are exactly one diameter apart; translating 0 -> +d gives a
    // continuous one-direction loop, never a bouncing/ping-pong reset.
    // WeatherScene's circular clip is the final visible boundary.
    private void CloudBand(double d,double x,double y,double scale,
        bool dark,int precip,double seconds)
    {
        var ribbon=new Canvas { Width=d*3.5,Height=d*1.10,
            IsHitTestVisible=false,ClipToBounds=false,
            Opacity=dark?.91:.84 };
        for(int copy=0;copy<3;copy++)
        {
            var puff=MakeCloud(d,scale,dark,precip,copy);
            Canvas.SetLeft(puff,copy*d);
            ribbon.Children.Add(puff);
        }
        var glide=new TranslateTransform();
        cloudMotion.Add(glide);
        ribbon.RenderTransform=glide;
        Draw(ribbon,x-d,y);
        glide.BeginAnimation(TranslateTransform.XProperty,
            LinearLoop(0,d,seconds));
    }

    private Canvas MakeCloud(double d,double scale,bool dark,int precip,int copy)
    {
        CloudInstanceCount++;
        double unit=d*scale;
        var puff=new Canvas { Width=unit*.53,Height=d*1.06,
            IsHitTestVisible=false,ClipToBounds=false };
        // Put rain BEHIND the cloud surface so droplets naturally emerge
        // from its underside, rather than being overlaid on the sky.
        if(precip==1||precip==2)AddCloudRain(puff,d,unit,copy,precip==2);
        if(precip==3)AddCloudSnow(puff,d,unit,copy);

        Color high=dark?Color.FromRgb(207,218,235):Color.FromRgb(255,255,255);
        Color shadow=dark?Color.FromRgb(91,119,157):Color.FromRgb(191,215,238);
        var fill=new LinearGradientBrush(high,shadow,90);
        void Blob(double x,double y,double w,double h,double alpha=1)
        {
            var e=new Ellipse { Width=w*unit,Height=h*unit,
                Fill=fill,Opacity=alpha,IsHitTestVisible=false };
            Canvas.SetLeft(e,x*unit);Canvas.SetTop(e,y*unit);
            puff.Children.Add(e);
        }
        // The bottom .20..23u is the visible cloud base: this SAME shape
        // defines where precipitation can start.
        Blob(.012,.130,.28,.129,.88);
        Blob(.100,.052,.24,.174,.96);
        Blob(.260,.081,.22,.157,.91);
        Blob(.085,.155,.392,.078,.98);
        Blob(.181,.041,.149,.112,.72);
        // Clouds are soft; rainfall itself stays sharp.
        var mist=Disc(unit*.37,Glow(high),dark?.15:.32);
        Canvas.SetLeft(mist,unit*.095);Canvas.SetTop(mist,unit*.022);
        puff.Children.Add(mist);
        return puff;
    }

    private void AddCloudRain(Canvas puff,double d,double unit,int copy,bool heavy)
    {
        RainEmitterCount++;
        // These coordinates are relative to this puff, not the whole scene.
        const double cloudBase=.209;
        double left=unit*.102,width=unit*.315;
        double limit=d*.80;
        var column=new Canvas { Width=unit*.53,Height=d,
            Clip=new RectangleGeometry(
                new Rect(left,unit*cloudBase,width,limit-unit*cloudBase)),
            IsHitTestVisible=false,Opacity=.86 };
        puff.Children.Add(column);
        var brush=new SolidColorBrush(Color.FromArgb(200,182,222,249));
        int drops=heavy?18:12;
        double fall=d*.71;
        for(int i=0;i<drops;i++)
        {
            double tx=unit*(.117+.285*((i*37+copy*17)%101)/100.0);
            // Rows start along the downward path so the scene is populated
            // immediately. Each streak emerges ONLY inside its parent puff.
            double ty=unit*cloudBase + d*.46*((i*41+copy*19)%100)/100.0;
            var line=new Line {
                X1=tx,Y1=ty,X2=tx-d*.012,Y2=ty+d*(heavy?.071:.056),
                Stroke=brush,Opacity=.62+((i%4)*.085),
                StrokeThickness=Math.Max(1.2,d*(heavy?.0075:.006)),
                StrokeStartLineCap=PenLineCap.Round,
                IsHitTestVisible=false
            };
            var fallTransform=new TranslateTransform();
            line.RenderTransform=fallTransform;
            column.Children.Add(line);
            dropMotion.Add(fallTransform);
            fallTransform.BeginAnimation(TranslateTransform.YProperty,
                LinearLoop(0,fall,2.9+(i%5)*.24));
        }
    }
    private void AddCloudSnow(Canvas puff,double d,double unit,int copy)
    {
        SnowEmitterCount++;
        double left=unit*.095;
        var column=new Canvas {
            Width=unit*.53,Height=d,Opacity=.91,IsHitTestVisible=false,
            Clip=new RectangleGeometry(
                new Rect(left,unit*.21,unit*.33,d*.79-unit*.21))
        };
        puff.Children.Add(column);
        for(int i=0;i<11;i++)
        {
            double x=unit*(.105+.285*((i*29+copy*7)%97)/100.0);
            double y=unit*.212+d*.37*((i*43+copy*13)%100)/100.0;
            var flake=Disc(Math.Max(1.3,d*(.009+(i%3)*.004)),
                Brushes.White,.60+(i%3)*.11);
            Canvas.SetLeft(flake,x);Canvas.SetTop(flake,y);
            var fall=new TranslateTransform();
            flake.RenderTransform=fall;
            column.Children.Add(flake);
            dropMotion.Add(fall);
            fall.BeginAnimation(TranslateTransform.YProperty,
                LinearLoop(0,d*.57,6.3+(i%4)*.65));
        }
    }
    public void StopMotion()
    {
        sunlightMotion?.BeginAnimation(RotateTransform.AngleProperty,null);
        foreach(var transform in cloudMotion)
            transform.BeginAnimation(TranslateTransform.XProperty,null);
        foreach(var transform in dropMotion)
            transform.BeginAnimation(TranslateTransform.YProperty,null);
        foreach(var item in ambientMotion)
            item.BeginAnimation(OpacityProperty,null);
        cloudMotion.Clear();
        dropMotion.Clear();
        ambientMotion.Clear();
    }
}
