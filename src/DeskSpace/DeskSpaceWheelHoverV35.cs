using System;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DeskSpace;

// Shared radial navigation source for both the main donut and its child arcs.
// WPF's MouseMove stops at transparent popup HWNDs and captured mouse surfaces;
// screen position -> WPF PointFromScreen works for all such surfaces.
internal static class DeskSpaceWheelHoverV35
{
    public const int PointerPollMilliseconds=35;
    public const int ChildCommitMilliseconds=510;
    public const int MainCommitMilliseconds=680;
    public const int EdgeFirstMilliseconds=175;
    public const int EdgeRepeatMilliseconds=135;

    public static Point MouseIn(Visual surface)
    {
        var point=Forms.Control.MousePosition;
        return surface.PointFromScreen(new Point(point.X,point.Y));
    }
    public static bool Dwelled(DateTime startedUtc,int milliseconds) =>
        DateTime.UtcNow-startedUtc>=TimeSpan.FromMilliseconds(milliseconds);
}