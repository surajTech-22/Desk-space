using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskSpace;

// Standard middle-button auto-scroll for every paged sub-donut and nested
// arc. A middle click toggles it; moving above/below (or left/right along an
// arc) the anchor shifts one item repeatedly without additional clicking.
// No browser/system hook, global mouse capture or polling outside an open menu.
internal static class DeskSpaceMiddleScrollV63
{
    internal static int Direction(Point anchor,Point cursor)
    {
        double dx=cursor.X-anchor.X,dy=cursor.Y-anchor.Y;
        if(Math.Abs(dy)>=18 && Math.Abs(dy)>=Math.Abs(dx)*.50)
            return dy>0?1:-1;
        if(Math.Abs(dx)>=34)return dx>0?1:-1;
        return 0;
    }

    // High-resolution trackpads emit fractional (<120) wheel deltas.
    // A normal mouse wheel must keep one page step per 120 units, whereas
    // precision touches can advance after a smaller accumulated movement.
    internal sealed class PrecisionWheelV290
    {
        private int remainder;
        internal int Step(int delta)
        {
            if(delta==0)return 0;
            if(Math.Abs(delta)>=120)
            {
                remainder=0;
                return Math.Clamp(-delta/120,-3,3);
            }
            if(remainder!=0 && Math.Sign(remainder)!=Math.Sign(delta))
                remainder=0;
            remainder=Math.Clamp(remainder+delta,-360,360);
            int page=0;
            while(remainder>=60){remainder-=60;page--;}
            while(remainder<=-60){remainder+=60;page++;}
            return Math.Clamp(page,-3,3);
        }
        internal void Reset()=>remainder=0;
    }

    internal sealed class Controller : IDisposable
    {
        private readonly Canvas surface;
        private readonly Func<bool> hasOverflow,menuVisible;
        private readonly Action<int> scroll;
        private readonly DispatcherTimer timer;
        private readonly PrecisionWheelV290 precisionWheel=new();
        private Point anchor;
        private Cursor? previousCursor;
        private bool disposed;
        internal bool IsActive {get;private set;}
        internal int TickCount {get;private set;}

        internal Controller(Canvas surface,Func<bool> hasOverflow,
            Func<bool> menuVisible,Action<int> scroll)
        {
            this.surface=surface;this.hasOverflow=hasOverflow;
            this.menuVisible=menuVisible;this.scroll=scroll;
            timer=new DispatcherTimer(DispatcherPriority.Input){
                Interval=TimeSpan.FromMilliseconds(170)};
            timer.Tick+=OnTick;
            surface.PreviewMouseDown+=OnMouseDown;
            surface.PreviewMouseWheel+=OnWheel;
            // WPF child Popups have their own HWND. Forward preview-wheel
            // input only when the pointer is inside a visible subdonut item.
            InputManager.Current.PreProcessInput+=OnPreProcessWheel;
            surface.PreviewKeyDown+=OnKeyDown;
        }
        private bool IsPointerOverVisiblePart()
        {
            if(disposed||!menuVisible()||!hasOverflow()||!surface.IsLoaded)
                return false;
            try
            {
                var at=DeskSpaceWheelHoverV35.MouseIn(surface);
                return at.X>=0&&at.Y>=0&&
                    at.X<=surface.ActualWidth&&at.Y<=surface.ActualHeight&&
                    surface.InputHitTest(at)!=null;
            }
            catch(InvalidOperationException){return false;}
        }
        private void ScrollWheel(MouseWheelEventArgs e)
        {
            if(e.Handled||!IsPointerOverVisiblePart())return;
            // Respect zoom, accessibility/system and horizontal gestures.
            if((Keyboard.Modifiers &(ModifierKeys.Control|
                ModifierKeys.Alt|ModifierKeys.Shift))!=0)return;
            if(IsActive)Stop();
            int amount=precisionWheel.Step(e.Delta);
            if(amount!=0)scroll(amount);
            // Even a fine-grained touchpad delta belongs to this wheel.
            e.Handled=true;
        }
        private void OnWheel(object sender,MouseWheelEventArgs e)=>ScrollWheel(e);
        private void OnPreProcessWheel(object sender,PreProcessInputEventArgs e)
        {
            if(e.StagingItem.Input is MouseWheelEventArgs wheel &&
               wheel.RoutedEvent==Mouse.PreviewMouseWheelEvent)
                ScrollWheel(wheel);
        }
        private void OnMouseDown(object sender,MouseButtonEventArgs e)
        {
            if(e.ChangedButton==MouseButton.Middle)
            {
                if(IsActive)Stop();
                else if(!disposed && menuVisible() && hasOverflow() &&
                    surface.IsLoaded)
                    Start(DeskSpaceWheelHoverV35.MouseIn(surface));
                e.Handled=true;
            }
            else if(IsActive)Stop();
        }
        private void OnKeyDown(object sender,KeyEventArgs e)
        {
            if(IsActive && e.Key==Key.Escape)
            {Stop();e.Handled=true;}
        }
        internal void Start(Point location)
        {
            if(disposed || !menuVisible() || !hasOverflow())return;
            Stop();
            anchor=location;
            IsActive=true;
            previousCursor=surface.Cursor;
            surface.Cursor=Cursors.ScrollAll;
            timer.Start();
        }
        internal void Pulse(Point pointer)
        {
            if(!IsActive || disposed)return;
            if(!menuVisible() || !hasOverflow())
            {precisionWheel.Reset();Stop();return;}
            int dir=Direction(anchor,pointer);
            if(dir==0)return;
            TickCount++;
            scroll(dir);
            double distance=Math.Max(Math.Abs(pointer.Y-anchor.Y),
                Math.Abs(pointer.X-anchor.X)*.7);
            timer.Interval=TimeSpan.FromMilliseconds(distance>115?105:
                distance>65?135:175);
        }
        private void OnTick(object? sender,EventArgs e)
        {
            if(!IsActive)return;
            if(!menuVisible() || !surface.IsLoaded){Stop();return;}
            try{Pulse(DeskSpaceWheelHoverV35.MouseIn(surface));}
            catch(InvalidOperationException){Stop();}
        }
        internal void Stop()
        {
            timer.Stop();
            precisionWheel.Reset();
            if(IsActive)surface.Cursor=previousCursor;
            previousCursor=null;
            IsActive=false;
        }
        public void Dispose()
        {
            if(disposed)return;
            Stop();disposed=true;
            InputManager.Current.PreProcessInput-=OnPreProcessWheel;
            surface.PreviewMouseWheel-=OnWheel;
            surface.PreviewMouseDown-=OnMouseDown;
            surface.PreviewKeyDown-=OnKeyDown;
            timer.Tick-=OnTick;
        }
    }
}
