using System;

namespace DeskSpace;

// R195: Pure per-widget DPI geometry, used by the real R190 dock. Reject
// abnormal WPF transforms before converting DIP sizes to native pixel widths.
// Owner monitor geometry remains in physical pixels; never borrow a DPI scale
// from the other monitor or use virtual-desktop dimensions as the work area.
internal static class DeskSpaceDpiBoundsV195
{
    internal static double CleanScale(double value)
    {
        if(!double.IsFinite(value) || value<0.5 || value>8)return 1d;
        return value;
    }
    internal static int Pixels(double dips,double scale,int workPixels)
    {
        int max=Math.Max(1,workPixels);
        if(!double.IsFinite(dips)||dips<=0)return 1;
        double native=dips*CleanScale(scale);
        if(!double.IsFinite(native)||native<=0)return 1;
        return native>=max?max:Math.Clamp((int)Math.Ceiling(native),1,max);
    }
}
