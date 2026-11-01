using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Input, hover and native mouse polling are per physical widget, NOT shared
// services. Shared workspace/settings/clipboard are intentionally untouched.
internal static class DeskSpaceMonitorInputV158
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PixelPoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PixelRect { public int Left,Top,Right,Bottom; }

    [DllImport("user32.dll",ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out PixelPoint point);
    [DllImport("user32.dll",ExactSpelling=true)]
    private static extern IntPtr WindowFromPoint(PixelPoint point);
    [DllImport("user32.dll",ExactSpelling=true)]
    private static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out PixelRect rect);

    private static Forms.Screen ScreenFor(Window owner)
    {
        if(owner is WidgetShellWindow widget)
        {
            var assigned=DeskSpaceMonitorFenceV152.BoundScreen(widget);
            if(assigned!=null)return assigned;
        }
        var hwnd=new WindowInteropHelper(owner).Handle;
        return hwnd==IntPtr.Zero
            ? Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0]
            : Forms.Screen.FromHandle(hwnd);
    }

    internal static bool IsPointerOnAssignedMonitor(Window owner)
    {
        if(!GetCursorPos(out var point))return false;
        var actual=Forms.Screen.FromPoint(new Drawing.Point(point.X,point.Y));
        return string.Equals(actual.DeviceName,ScreenFor(owner).DeviceName,
            StringComparison.OrdinalIgnoreCase);
    }

    // Pure geometry/ownership gate used both by production and WPF regression.
    // Comparing HWND roots also disambiguates two widgets on the same screen.
    internal static bool Allows(Drawing.Rectangle ownerRect,
        Drawing.Rectangle monitorRect,Drawing.Point cursor,
        IntPtr hitWindowRoot,IntPtr ownerWindow)
    {
        return ownerWindow!=IntPtr.Zero && hitWindowRoot==ownerWindow &&
            ownerRect.Width>0 && ownerRect.Height>0 &&
            monitorRect.Contains(cursor) && ownerRect.Contains(cursor);
    }

    internal static bool OwnsRightClick(Window owner)
    {
        if(!owner.IsVisible||!GetCursorPos(out var point))return false;
        var source=new Drawing.Point(point.X,point.Y);
        var screen=ScreenFor(owner);
        // No window on another monitor may toggle from a global pointer state.
        if(!string.Equals(Forms.Screen.FromPoint(source).DeviceName,
            screen.DeviceName,StringComparison.OrdinalIgnoreCase))return false;
        var hwnd=new WindowInteropHelper(owner).Handle;
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect))return false;
        var hit=WindowFromPoint(point);
        if(hit==IntPtr.Zero)return false;
        var root=GetAncestor(hit,2); // GA_ROOT: physical window receiving click
        var bounds=new Drawing.Rectangle(rect.Left,rect.Top,
            rect.Right-rect.Left,rect.Bottom-rect.Top);
        return Allows(bounds,screen.WorkingArea,source,root,hwnd);
    }
}
