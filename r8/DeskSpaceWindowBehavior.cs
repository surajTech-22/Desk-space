using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeskSpace;

// A floating widget is not a normal application switch target. It must also
// never reactivate the previously focused textbox in another application.
internal static class DeskSpaceWindowBehavior
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const long WS_EX_NOACTIVATE = 0x08000000L;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetLong(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetLong(IntPtr handle, int index, IntPtr value);

    internal static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        long flags = GetLong(handle, GWL_EXSTYLE).ToInt64();
        flags |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        flags &= ~WS_EX_APPWINDOW;
        SetLong(handle, GWL_EXSTYLE, new IntPtr(flags));
        if (HwndSource.FromHwnd(handle) is HwndSource source)
            source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == WM_MOUSEACTIVATE)
                {
                    handled = true;
                    return new IntPtr(MA_NOACTIVATE);
                }
                return IntPtr.Zero;
            });
    }
}