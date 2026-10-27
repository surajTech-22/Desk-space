using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Failsafe for per-monitor HWND containment, including WPF's independent
// translucent Popup windows and a compositor frame during animated expansion.
// Unlike a post-layout position correction, a Win32 window region forbids
// drawing AND mouse hit tests in the neighboring monitor on that frame.
internal static class DeskSpaceNativeClipV154
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect {public int L,T,R,B;}
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr region);
    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);

    private static readonly HashSet<IntPtr> Clipped=new();

    internal static Rectangle IntersectInWindow(Rectangle window,Rectangle monitor)
    {
        var visible=Rectangle.Intersect(window,monitor);
        if(visible.IsEmpty)return Rectangle.Empty;
        return new Rectangle(visible.Left-window.Left,visible.Top-window.Top,
            visible.Width,visible.Height);
    }

    internal static void Apply(IntPtr hwnd,Rectangle monitorWorkArea)
    {
        if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var native))return;
        var bounds=new Rectangle(native.L,native.T,native.R-native.L,
            native.B-native.T);
        if(bounds.Width<=0||bounds.Height<=0)return;
        var visible=IntersectInWindow(bounds,monitorWorkArea);
        if(visible.X==0&&visible.Y==0&&visible.Width==bounds.Width&&
           visible.Height==bounds.Height)
        {
            // Reset only our own previous clip, not WPF's normal round-region
            // and transparency handling on already-contained windows.
            if(Clipped.Remove(hwnd))
                SetWindowRgn(hwnd,IntPtr.Zero,true);
            return;
        }
        IntPtr region=CreateRectRgn(visible.Left,visible.Top,
            visible.Right,visible.Bottom);
        if(region==IntPtr.Zero)return;
        // Successful SetWindowRgn transfers ownership of the region to Windows.
        if(SetWindowRgn(hwnd,region,true)==0)
            DeleteObject(region);
        else Clipped.Add(hwnd);
    }

    internal static void Release(IntPtr hwnd)
    {
        if(Clipped.Remove(hwnd))SetWindowRgn(hwnd,IntPtr.Zero,true);
    }
}
