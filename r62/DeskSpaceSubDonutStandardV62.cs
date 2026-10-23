using System;
using System.Windows;
using System.Windows.Controls;
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

    internal static void DrawHiddenCounters(Canvas canvas,View view,
        double verticalCentre,Action<int> move)
    {
        if(view.HiddenLeft>0)
        {
            var left=Counter("‹ "+view.HiddenLeft,()=>move(-1));
            Canvas.SetLeft(left,1);
            Canvas.SetTop(left,Math.Max(0,verticalCentre-left.Height/2));
            canvas.Children.Add(left);
        }
        if(view.HiddenRight>0)
        {
            var right=Counter(view.HiddenRight+" ›",()=>move(1));
            Canvas.SetLeft(right,Math.Max(0,canvas.Width-right.Width-1));
            Canvas.SetTop(right,Math.Max(0,verticalCentre-right.Height/2));
            canvas.Children.Add(right);
        }
    }
    private static Button Counter(string text,Action action)
    {
        var b=new Button{
            Content=text,Width=39,Height=26,FontSize=11,FontWeight=FontWeights.SemiBold,
            Padding=new Thickness(1),ToolTip="Scroll to reveal hidden items",
            Foreground=Brushes.White,
            Background=new SolidColorBrush(Color.FromArgb(238,70,89,160)),
            BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand
        };
        b.Click+=(_,_)=>action();
        return b;
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
