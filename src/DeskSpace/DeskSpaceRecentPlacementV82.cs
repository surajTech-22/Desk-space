using System;
using System.Windows;

namespace DeskSpace;

// Returns the relative Popup position from the centre widget using the
// observed work area. The recent shelf must stay attached below the circle
// (or above it near the taskbar), never disappear off-screen.
internal static class DeskSpaceRecentPlacementV82
{
    internal static Point Choose(Rect widget,Rect work,Size popup)
    {
        double absoluteX=Math.Clamp(
            widget.Left+(widget.Width-popup.Width)/2,
            work.Left,Math.Max(work.Left,work.Right-popup.Width));
        double below=widget.Bottom+7;
        double above=widget.Top-popup.Height-7;
        double absoluteY=below+popup.Height<=work.Bottom?below:
            above>=work.Top?above:
            Math.Clamp(below,work.Top,Math.Max(work.Top,work.Bottom-popup.Height));
        return new Point(absoluteX-widget.Left,absoluteY-widget.Top);
    }
}
