using System;
using System.Windows;

namespace DeskSpace;

// Single coordinate contract for all DeskSpace parent/child donut rings.
// Logical host diameter is 420 DIP. Child wedges are drawn just OUTSIDE
// the parent's 195-DIP ring, centered on the exact parent wedge.
internal static class DeskSpaceDonutGeometryV43
{
    internal const double HostDiameter=420.0;
    internal const double ChildInner=207.0;
    internal const double ChildOuter=260.0;

    internal static double Scale(double diameter)
    {
        if(double.IsNaN(diameter)||double.IsInfinity(diameter)||diameter<=0)
            throw new ArgumentOutOfRangeException(nameof(diameter));
        return Math.Clamp(diameter/HostDiameter,0.5,2.0);
    }

    internal static double Scale(Window owner)
    {
        double w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        return Scale(Math.Min(w,h));
    }

    internal static double OuterSectorCenter(int slot,int count=7)
    {
        if(count<=0||slot<0||slot>=count)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return -90.0+(slot+0.5)*(360.0/count);
    }

    // WPF Popup PlacementMode.Relative offsets are in owner-local DIPs.
    // localX/Y describe the tightly clipped child-arc Canvas origin.
    internal static Point PopupOffset(Window owner,double centerX,double centerY,
                                     double localX,double localY)
    {
        double w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        double scale=Scale(owner);
        return new Point(w/2-centerX*scale+localX*scale,
                         h/2-centerY*scale+localY*scale);
    }
}
