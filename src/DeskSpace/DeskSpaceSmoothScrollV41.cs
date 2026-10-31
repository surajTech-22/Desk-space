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
        internal Motion(ScrollViewer viewer)
        {
            Viewer=viewer;
            Goal=viewer.VerticalOffset;
            Tick=new DispatcherTimer(DispatcherPriority.Render,viewer.Dispatcher) {
                Interval=TimeSpan.FromMilliseconds(16)
            };
            Tick.Tick+=(_,_)=>{
                if(!Viewer.IsVisible||Viewer.ScrollableHeight<=0){
                    Tick.Stop();return;
                }
                var distance=Goal-Viewer.VerticalOffset;
                if(Math.Abs(distance)<.8){
                    Viewer.ScrollToVerticalOffset(Goal);
                    Tick.Stop();
                    return;
                }
                Viewer.ScrollToVerticalOffset(Viewer.VerticalOffset+
                    distance*.31);
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
    private static ScrollViewer? FindTarget(DependencyObject? origin,int delta)
    {
        for(var current=origin;current!=null;current=Parent(current))
        {
            if(current is not ScrollViewer viewer || !viewer.IsVisible ||
               !viewer.IsEnabled || viewer.ScrollableHeight<=0)continue;
            double offset=viewer.VerticalOffset;
            // A mouse wheel can have an uncompleted animation: use both the
            // present position and the target to decide if an inner panel
            // has reached the end, allowing the outer panel to move.
            var motion=motions.GetValue(viewer,v=>new Motion(v));
            double planned=motion.Tick.IsEnabled?motion.Goal:offset;
            if(delta>0 && planned>.3)return viewer;
            if(delta<0 && planned<viewer.ScrollableHeight-.3)return viewer;
        }
        return null;
    }
    private static void OnWheel(object sender,MouseWheelEventArgs e)
    {
        if(e.Handled||e.Delta==0 || (Keyboard.Modifiers &
           (ModifierKeys.Control|ModifierKeys.Shift|ModifierKeys.Alt))!=0)
           return; // Keep system zoom/horizontal/modifier operations.
        var target=FindTarget(e.OriginalSource as DependencyObject,e.Delta);
        if(!ReferenceEquals(sender,target) || target==null)return;
        // WPF virtualized grids measure scrolling in item units. Smooth
        // animation on logical lists can trigger expensive re-layout, so
        // keep the native virtualized wheel behavior there.
        if(target.CanContentScroll) return;
        var motion=motions.GetValue(target,v=>new Motion(v));
        int lines=SystemParameters.WheelScrollLines;
        if(lines<1||lines>12)lines=3;
        double dip=Math.Clamp(lines*21.0,45,132);
        double start=motion.Tick.IsEnabled?motion.Goal:target.VerticalOffset;
        motion.Goal=Math.Clamp(start-(e.Delta/120.0)*dip,
            0,target.ScrollableHeight);
        if(Math.Abs(motion.Goal-target.VerticalOffset)<.3)return;
        motion.Tick.Start();
        e.Handled=true;
    }
}
