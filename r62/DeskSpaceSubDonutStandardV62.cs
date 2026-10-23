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

    // One universal visual rule: counts are extensions of the LEFT/RIGHT
    // VISIBLE WEDGES, not disconnected badges at the top of the screen.
    // The same renderer is used by Information, Apps and every Excel depth.
    internal static void DrawHiddenCounters(Canvas canvas,View view,
        double verticalCentre,Action<int> move)
    {
        if(view.HiddenLeft==0&&view.HiddenRight==0)return;
        var wedges=canvas.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>p.Data!=null&&!p.Data.Bounds.IsEmpty)
            .ToArray();
        if(wedges.Length==0)return;
        int actual=Math.Min(view.Count,wedges.Length);
        if(actual==0)return;
        if(view.HiddenLeft>0)
            AddEdgeTab(canvas,wedges[0],view.HiddenLeft,true,()=>move(-1));
        if(view.HiddenRight>0)
            AddEdgeTab(canvas,wedges[actual-1],view.HiddenRight,false,()=>move(1));
    }
    private static void AddEdgeTab(Canvas canvas,System.Windows.Shapes.Path wedge,
        int amount,bool leading,Action advance)
    {
        // Wedge geometry is in the submenu Canvas coordinate system.
        // Anchor INSIDE its visible sector at the extreme edge, preserving
        // the same border and background as the sector being continued.
        Rect area=wedge.Data.Bounds;
        const double width=33,height=23;
        double x=leading?area.Left+2:area.Right-width-2;
        double y=area.Top+(area.Height-height)/2;
        x=Math.Clamp(x,0,Math.Max(0,canvas.Width-width));
        y=Math.Clamp(y,0,Math.Max(0,canvas.Height-height));
        var tab=new Button{
            Content=leading?"‹ "+amount:amount+" ›",
            Width=width,Height=height,FontSize=10.5,
            FontWeight=FontWeights.SemiBold,
            Padding=new Thickness(0),
            Background=wedge.Fill,
            Foreground=Brushes.White,
            BorderBrush=wedge.Stroke??Brushes.White,
            BorderThickness=new Thickness(1.5),
            Cursor=System.Windows.Input.Cursors.Hand,
            ToolTip=(leading?"Hidden before: ":"Hidden after: ")+amount+
                " · wheel to reveal"
        };
        var ring=new Border{
            CornerRadius=new CornerRadius(5),
            BorderThickness=new Thickness(0),
            Child=tab
        };
        Canvas.SetLeft(ring,x);
        Canvas.SetTop(ring,y);
        Canvas.SetZIndex(ring,40);
        ring.ClipToBounds=true;
        tab.Click+=(_,_)=>advance();
        canvas.Children.Add(ring);
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
