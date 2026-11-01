using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeskSpace;

// A single optional global shortcut for the EXISTING inline Search module.
// Does not create a second app or an independent Search window.
internal sealed class DeskSpaceCommandShortcutV85 : IDisposable
{
    private const int HotkeyId=0x4453;
    private const int WmHotkey=0x0312;
    private const uint Alt=0x0001,Control=0x0002,NoRepeat=0x4000;
    private const uint Space=0x20;
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window,int id,
        uint modifiers,uint key);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window,int id);

    private readonly Window owner;
    private readonly Action openSearch;
    private HwndSource? source;
    private bool registered,disposed;
    internal DeskSpaceCommandShortcutV85(Window owner,Action openSearch)
    {
        this.owner=owner;this.openSearch=openSearch;
        owner.SourceInitialized+=Initialized;
        owner.Closed+=Closed;
        InitializeIfReady();
    }
    private void Initialized(object? sender,EventArgs e)=>InitializeIfReady();
    private void Closed(object? sender,EventArgs e)=>Dispose();
    private void InitializeIfReady()
    {
        if(disposed || source!=null)return;
        var handle=new WindowInteropHelper(owner).Handle;
        if(handle==IntPtr.Zero)return;
        source=HwndSource.FromHwnd(handle);
        if(source==null)return;
        source.AddHook(Hook);
        registered=RegisterHotKey(handle,HotkeyId,Control|Alt|NoRepeat,Space);
    }
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,
        ref bool handled)
    {
        if(!disposed && registered && message==WmHotkey &&
           wParam.ToInt32()==HotkeyId)
        {
            handled=true;openSearch();
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        if(source!=null)
        {
            if(registered)UnregisterHotKey(source.Handle,HotkeyId);
            source.RemoveHook(Hook);
        }
        owner.SourceInitialized-=Initialized;
        owner.Closed-=Closed;
    }
}
