using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskSpace;

// Reusable, module-agnostic donut paging, radius, and scroll appearance.
// Every module provides its items, parent anchor and its launch/edit action.
// The engine never loads application or accounting data by itself.
internal static class DeskSpaceSubDonutStandardV62
{
    internal const int Visible=5;
    internal const double ParentSweep=360.0/7.0;
    internal const int DurationMs=155;

    internal readonly record struct View(int Total,int Start,int Count,
        int HiddenLeft,int HiddenRight,double Center,double Half,double Step)
    {
        internal double From(int index)=>Center-Half+Step*index;
        internal double To(int index)=>From(index)+Step;
    }

    // One parent plus half of each adjacent parent's width. At root this is
    // 102.857° maximum. Reused for child-of-child arcs with parentSweep
    // equal to the selected child's own angular width.
    internal static View Plan(int total,int start,double center,
        double parentSweep=ParentSweep)
    {
        if(total<0)throw new ArgumentOutOfRangeException(nameof(total));
        if(parentSweep<=0||parentSweep>180)
            throw new ArgumentOutOfRangeException(nameof(parentSweep));
        int count=Math.Min(total,Visible);
        int maxStart=Math.Max(0,total-count);
        start=Math.Clamp(start,0,maxStart);
        double half=count<=1?Math.Min(17,parentSweep):
            Math.Min(12.0*count,parentSweep);
        return new View(total,start,count,start,
            Math.Max(0,total-start-count),center,half,
            count==0?0:2*half/count);
    }

    // Mouse wheels can report detents of ±120 or fine-grained touchpad deltas.
    // Exactly one item shifts for each full detent; small deltas accumulate.
    internal sealed class Wheel
    {
        private int remainder;
        internal int Step(int delta)
        {
            if(delta==0)return 0;
            if(Math.Sign(delta)!=Math.Sign(remainder))remainder=0;
            remainder+=delta;
            int step=0;
            while(Math.Abs(remainder)>=120)
            {
                step+=remainder<0?1:-1;
                remainder+=remainder<0?120:-120;
            }
            return step;
        }
        internal void Reset()=>remainder=0;
    }

    internal static int Advance(View view,int delta)=>
        Math.Clamp(view.Start+delta,0,Math.Max(0,view.Total-view.Count));

    // Universal hidden-count design: the numeral interrupts the VERTICAL
    // radial divider at the end of the visible arc. Never add an exterior
    // badge, a mini wedge or a top-of-canvas label. This uses the actual WPF
    // sector geometry, so it works at any submenu angle, radius and depth.
    internal static void DrawHiddenCounters(Canvas canvas,View view,
        double verticalCentre,Action<int> move)
    {
        if(view.HiddenLeft==0&&view.HiddenRight==0)return;
        var wedges=canvas.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>p.Data!=null&&!p.Data.Bounds.IsEmpty)
            .ToArray();
        int count=Math.Min(view.Count,wedges.Length);
        if(count==0)return;
        if(view.HiddenLeft>0)
            AddDividerCount(canvas,wedges[0],view.HiddenLeft,true,
                ()=>move(-1));
        if(view.HiddenRight>0)
            AddDividerCount(canvas,wedges[count-1],view.HiddenRight,false,
                ()=>move(1));
    }

    // Start side: first arc's OUTER start to the second arc's INNER end.
    // End side: first arc's OUTER end to the straight segment's INNER end.
    // Do not use the geometry bounding rectangle: its corners aren't on the
    // wedge divider, which caused the previous detached counters.
    internal static Point DividerMidpoint(Geometry shape,bool leading)
    {
        if(shape is not PathGeometry pg || pg.Figures.Count==0)
            throw new ArgumentException("Sub-donut wedge must use arc geometry",
                nameof(shape));
        var figure=pg.Figures[0];
        if(figure.Segments.Count<3 ||
            figure.Segments[0] is not ArcSegment outerArc ||
            figure.Segments[1] is not LineSegment radialLine ||
            figure.Segments[2] is not ArcSegment innerArc)
            throw new ArgumentException("Unexpected sub-donut arc segments",
                nameof(shape));
        Point outer=leading?figure.StartPoint:outerArc.Point;
        Point inner=leading?innerArc.Point:radialLine.Point;
        var transform=shape.Transform;
        if(transform!=null)
        {
            outer=transform.Transform(outer);
            inner=transform.Transform(inner);
        }
        return new Point((outer.X+inner.X)/2,(outer.Y+inner.Y)/2);
    }

    private static void AddDividerCount(Canvas canvas,
        System.Windows.Shapes.Path wedge,int hidden,bool leading,Action move)
    {
        Point midpoint=DividerMidpoint(wedge.Data,leading);
        // Exactly on the end divider, halfway along its radial height.
        // Matching wedge fill masks ONLY the central few pixels of the
        // stroke. The existing white line remains visible above/below.
        string count=hidden.ToString(System.Globalization.CultureInfo.InvariantCulture);
        double width=Math.Max(17,9+count.Length*8),height=19;
        Brush ink=Brushes.White;
        if(wedge.Fill is SolidColorBrush paint)
        {
            double brightness=.2126*paint.Color.R+
                .7152*paint.Color.G+.0722*paint.Color.B;
            if(brightness>=135)
                ink=new SolidColorBrush(Color.FromRgb(26,37,72));
        }
        var button=new Button{
            Content=count,
            Width=width,Height=height,
            FontSize=11,FontWeight=FontWeights.SemiBold,
            HorizontalContentAlignment=HorizontalAlignment.Center,
            VerticalContentAlignment=VerticalAlignment.Center,
            Padding=new Thickness(0),
            Background=wedge.Fill??Brushes.Transparent,
            Foreground=ink,
            BorderThickness=new Thickness(0),
            FocusVisualStyle=null,
            Cursor=System.Windows.Input.Cursors.Hand,
            ToolTip=(leading?"Hidden before: ":"Hidden after: ")+hidden+
                " · scroll or select to reveal"
        };
        var embedded=new Border{
            Child=button,
            Background=wedge.Fill??Brushes.Transparent,
            BorderThickness=new Thickness(0),
            CornerRadius=new CornerRadius(1),
            SnapsToDevicePixels=true
        };
        Canvas.SetLeft(embedded,midpoint.X-width/2);
        Canvas.SetTop(embedded,midpoint.Y-height/2);
        Canvas.SetZIndex(embedded,40);
        button.Click+=(_,_)=>move();
        canvas.Children.Add(embedded);
    }

    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        canvas.BeginAnimation(UIElement.OpacityProperty,null);
        // Small tangential slide plus eased fade makes each detent continuous,
        // with a duration short enough to keep wheel and hover responsive.
        var tangent=(centerAngle+90)*Math.PI/180;
        double dist=direction>0?-11:11;
        var offset=new TranslateTransform();
        canvas.RenderTransform=offset;
        var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        offset.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(dist*Math.Cos(tangent),0,
                TimeSpan.FromMilliseconds(DurationMs)){EasingFunction=easing});
        offset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(dist*Math.Sin(tangent),0,
                TimeSpan.FromMilliseconds(DurationMs)){EasingFunction=easing});
        canvas.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(.74,1,TimeSpan.FromMilliseconds(DurationMs)){
                EasingFunction=easing});
    }
}
