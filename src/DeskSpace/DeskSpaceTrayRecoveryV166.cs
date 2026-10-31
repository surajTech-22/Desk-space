using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Primary widget only. Windows Explorer can restart while DeskSpace is
// running in tray-only mode, removing its notification icon from the taskbar.
// Recover the EXISTING NotifyIcon without creating a duplicate app/widget,
// touching data/settings or activating any window.
internal static class DeskSpaceTrayRecoveryV166
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern uint RegisterWindowMessage(string message);
    private static readonly uint TaskbarCreated=RegisterWindowMessage("TaskbarCreated");

    private sealed class Watcher : IDisposable
    {
        private readonly Window owner;
        private readonly Forms.NotifyIcon icon;
        private readonly DispatcherTimer fallback;
        private HwndSource? source;
        private bool disposed;
        private readonly HwndSourceHook hook;

        internal Watcher(Window owner,Forms.NotifyIcon icon)
        {
            this.owner=owner;
            this.icon=icon;
            hook=OnMessage;
            fallback=new DispatcherTimer(DispatcherPriority.Background,owner.Dispatcher){
                Interval=TimeSpan.FromSeconds(30)
            };
            fallback.Tick+=(_,_)=>EnsureVisible(false);
            owner.SourceInitialized+=OnSourceInitialized;
            owner.Loaded+=OnLoaded;
            owner.Closed+=OnClosed;
            Attach();
            if(owner.IsLoaded)OnLoaded(owner,new RoutedEventArgs());
            fallback.Start();
        }

        private void Attach()
        {
            if(disposed||source!=null)return;
            source=PresentationSource.FromVisual(owner) as HwndSource;
            if(source!=null && !source.IsDisposed)
                source.AddHook(hook);
            else source=null;
        }

        private void OnSourceInitialized(object? sender,EventArgs e)=>Attach();

        private void OnLoaded(object sender,RoutedEventArgs e)
        {
            Attach();
            EnsureVisible(false);
        }

        private IntPtr OnMessage(IntPtr hwnd,int msg,IntPtr wParam,
            IntPtr lParam,ref bool handled)
        {
            if(!disposed && TaskbarCreated!=0 && (uint)msg==TaskbarCreated)
            {
                // Explorer recreated its notification-area window. The
                // NotifyIcon must re-register, but the WPF widget must NOT
                // open, steal focus, reposition, or recreate its tray object.
                owner.Dispatcher.BeginInvoke(new Action(()=>
                    EnsureVisible(true)),DispatcherPriority.Background);
            }
            return IntPtr.Zero;
        }

        private void EnsureVisible(bool force)
        {
            if(disposed)return;
            try
            {
                if(icon.Icon==null)icon.Icon=SystemIcons.Application;
                if(force)
                {
                    // Toggling causes a fresh Shell_NotifyIcon registration.
                    icon.Visible=false;
                    icon.Visible=true;
                }
                else if(!icon.Visible)icon.Visible=true;
            }
            catch(ObjectDisposedException)
            {
                // Normal app shutdown may dispose NotifyIcon first.
            }
            catch(InvalidOperationException)
            {
                // Explorer can still be starting; the next fallback retries.
            }
        }

        private void OnClosed(object? sender,EventArgs e)=>Dispose();
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            fallback.Stop();
            owner.SourceInitialized-=OnSourceInitialized;
            owner.Loaded-=OnLoaded;
            owner.Closed-=OnClosed;
            if(source!=null && !source.IsDisposed)source.RemoveHook(hook);
            source=null;
        }
    }

    internal static IDisposable Watch(Window owner,Forms.NotifyIcon icon)
        =>new Watcher(owner,icon);

    // Isolated foreground-free registration used by real WPF smoke tests.
    internal static bool RestoreForTest(Forms.NotifyIcon icon,bool force)
    {
        if(icon.Icon==null)icon.Icon=SystemIcons.Application;
        if(force)icon.Visible=false;
        icon.Visible=true;
        return icon.Visible && icon.Icon!=null;
    }
}
