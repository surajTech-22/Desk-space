using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace DeskSpace;

// Temporary layout movement is not a user decision. A ring may need a little
// room to expand at a monitor edge, but its owner belongs at the location the
// user actually placed it when the menu closes.
internal sealed class DeskSpaceDonutHomeV65
{
    private Point? original;
    internal bool HasOrigin=>original.HasValue;
    internal Point Origin=>original??new Point(double.NaN,double.NaN);

    internal void Capture(Window owner)
    {
        if(original.HasValue)return;
        if(!double.IsFinite(owner.Left)||!double.IsFinite(owner.Top))return;
        original=new Point(owner.Left,owner.Top);
    }
    internal bool Restore(Window owner)
    {
        if(original is not Point position)return false;
        original=null;
        // Prior R24 animations could retain an effective window position
        // even after setting Left/Top; clear only position animations.
        owner.BeginAnimation(Window.LeftProperty,null);
        owner.BeginAnimation(Window.TopProperty,null);
        owner.Left=position.X;
        owner.Top=position.Y;
        return true;
    }
    internal void Cancel()=>original=null;
}
