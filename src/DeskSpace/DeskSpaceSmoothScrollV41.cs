using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace DeskSpace;

// Global, lightweight WPF wheel routing. Nested ScrollViewers are common in
// Excel mapping and the Workspace: when the inner one hits a boundary the
// outer one must take over. Move toward a scroll target at a short 16-ms
// cadence without blocking the UI or compromising virtualized lists.
internal static class DeskSpaceSmoothScrollV41
{
    private static bool registered;
    private static readonly ConditionalWeakTable<ScrollViewer,Motion> motions=new();
    private sealed class Motion
    {
        internal readonly ScrollViewer Viewer;
        internal readonly DispatcherTimer Tick;
        internal double Goal;
        internal double HorizontalGoal;
        internal Motion(ScrollViewer viewer)
        {
            Viewer=viewer;
            Goal=viewer.VerticalOffset;
            HorizontalGoal=viewer.HorizontalOffset;
            Tick=new DispatcherTimer(DispatcherPriority.Render,viewer.Dispatcher) {
                Interval=TimeSpan.FromMilliseconds(16)
            };
            Tick.Tick+=(_,_)=>{
                if(!Viewer.IsVisible){Tick.Stop();return;}
                Goal=Math.Clamp(Goal,0,Viewer.ScrollableHeight);
                HorizontalGoal=Math.Clamp(HorizontalGoal,0,Viewer.ScrollableWidth);
                var y=Goal-Viewer.VerticalOffset;
                var x=HorizontalGoal-Viewer.HorizontalOffset;
                if(Math.Abs(y)<.6 && Math.Abs(x)<.6)
                {
                    Viewer.ScrollToVerticalOffset(Goal);
                    Viewer.ScrollToHorizontalOffset(HorizontalGoal);
                    Tick.Stop();return;
                }
                if(Math.Abs(y)>=.6)
                    Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset+y*.40);
                if(Math.Abs(x)>=.6)
                    Viewer.ScrollToHorizontalOffset(Viewer.HorizontalOffset+x*.40);
            };
            viewer.Unloaded+=(_,_)=>Tick.Stop();
        }
    }
    internal static void Enable()
    {
        if(registered)return;
        registered=true;
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnWheel));
    }
    private static DependencyObject? Parent(DependencyObject obj)
    {
        if(obj is Visual or Visual3D)
            return VisualTreeHelper.GetParent(obj)??LogicalTreeHelper.GetParent(obj);
        return LogicalTreeHelper.GetParent(obj);
    }
    private static ScrollViewer? FindTarget(DependencyObject? origin,int delta,
        bool horizontal)
    {
        for(var current=origin;current!=null;current=Parent(current))
        {
            if(current is not ScrollViewer viewer || !viewer.IsVisible ||
               !viewer.IsEnabled || (horizontal?
                  viewer.ScrollableWidth<=0:viewer.ScrollableHeight<=0))continue;
            double offset=horizontal?viewer.HorizontalOffset:viewer.VerticalOffset;
            // A mouse wheel can have an uncompleted animation: use both the
            // present position and the target to decide if an inner panel
            // has reached the end, allowing the outer panel to move.
            var motion=motions.GetValue(viewer,v=>new Motion(v));
            double planned=motion.Tick.IsEnabled?
                (horizontal?motion.HorizontalGoal:motion.Goal):offset;
            double max=horizontal?viewer.ScrollableWidth:viewer.ScrollableHeight;
            if(delta>0 && planned>.3)return viewer;
            if(delta<0 && planned<max-.3)return viewer;
        }
        return null;
    }
    // Trackpads emit many sub-120 deltas. Preserve each signed fraction
    // instead of rounding it to a full wheel notch or losing it.
    internal static double PixelsForWheelV289(int delta,int lines=3)
    {
        if(delta==0)return 0;
        if(lines<1||lines>12)lines=3;
        return -Math.Clamp(delta,-480,480)*lines*22.0/120.0;
    }
    private static void OnWheel(object sender,MouseWheelEventArgs e)
    {
        if(e.Handled||e.Delta==0)return;
        var mods=Keyboard.Modifiers;
        if((mods&(ModifierKeys.Control|ModifierKeys.Alt))!=0)return;
        // Shift-wheel gives horizontal motion to touchpad users. Ordinary
        // two-finger vertical wheel continues through the nested panels.
        bool horizontal=(mods&ModifierKeys.Shift)!=0;
        var target=FindTarget(e.OriginalSource as DependencyObject,
            e.Delta,horizontal);
        if(!ReferenceEquals(sender,target) || target==null)return;
        // Large logical/virtualized lists keep their native behavior;
        // Deck explicitly opts into pixel scrolling instead.
        if(target.CanContentScroll && !horizontal)return;
        var motion=motions.GetValue(target,v=>new Motion(v));
        double dip=PixelsForWheelV289(e.Delta,SystemParameters.WheelScrollLines);
        if(horizontal)
        {
            double start=motion.Tick.IsEnabled?
                motion.HorizontalGoal:target.HorizontalOffset;
            motion.HorizontalGoal=Math.Clamp(start+dip,0,target.ScrollableWidth);
        }
        else
        {
            double start=motion.Tick.IsEnabled?motion.Goal:target.VerticalOffset;
            motion.Goal=Math.Clamp(start+dip,0,target.ScrollableHeight);
        }
        // Accept even tiny fractional input: subsequent touchpad events
        // accumulate in Goal rather than being discarded.
        motion.Tick.Start();
        e.Handled=true;
    }
}
