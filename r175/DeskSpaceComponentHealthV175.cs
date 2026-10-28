using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Observe WM_CLIPBOARDUPDATE only. Never inspect the clipboard's contents,
// passwords, file paths, text, images or formats; no persistence or telemetry.
// The OS posts a notification instead of requiring a new polling timer.
internal static class DeskSpaceClipboardSignalV175
{
    private const int ClipboardUpdated=0x031D;
    [DllImport("user32.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private static int connected,changes;
    internal static bool IsConnected=>Volatile.Read(ref connected)>0;
    internal static int Changes=>Volatile.Read(ref changes);

    private sealed class Listener : IDisposable
    {
        private readonly Window owner;
        private HwndSource? source;
        private readonly HwndSourceHook hook;
        private bool registered,disposed;
        internal Listener(Window owner)
        {
            this.owner=owner;
            hook=OnMessage;
            owner.SourceInitialized+=Initialized;
            owner.Closed+=Closed;
            Attach();
        }
        private void Initialized(object? sender,EventArgs e)=>Attach();
        private void Closed(object? sender,EventArgs e)=>Dispose();
        private void Attach()
        {
            if(disposed||registered)return;
            var candidate=PresentationSource.FromVisual(owner) as HwndSource;
            if(candidate==null||candidate.IsDisposed)return;
            candidate.AddHook(hook);
            if(!AddClipboardFormatListener(candidate.Handle))
            {
                candidate.RemoveHook(hook);
                return; // Unsupported session: no clipboard polling fallback.
            }
            source=candidate;
            registered=true;
            Interlocked.Increment(ref connected);
        }
        private IntPtr OnMessage(IntPtr hwnd,int msg,IntPtr wp,
            IntPtr lp,ref bool handled)
        {
            if(!disposed&&msg==ClipboardUpdated)
                Interlocked.Increment(ref changes);
            // Never mark handled: preserve normal WPF/input routing.
            return IntPtr.Zero;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            owner.SourceInitialized-=Initialized;
            owner.Closed-=Closed;
            if(source!=null&&!source.IsDisposed)
            {
                if(registered)RemoveClipboardFormatListener(source.Handle);
                source.RemoveHook(hook);
            }
            if(registered)Interlocked.Decrement(ref connected);
            registered=false;
            source=null;
        }
    }
    internal static IDisposable Watch(Window owner)=>new Listener(owner);
}

// On-demand, read-only status panel. Values are deliberately coarse;
// no raw logs, search index contents, workbook names or clipboard data.
internal static class DeskSpaceComponentHealthV175
{
    internal sealed record Status(string Name,string Detail);
    private static readonly Dictionary<Window,Window> Open=new();

    internal static IReadOnlyList<Status> Snapshot(Window widget,bool trayVisible)
    {
        var result=new List<Status>();
        int screens=0;
        try{screens=Forms.Screen.AllScreens.Length;}catch(System.ComponentModel.InvalidEnumArgumentException){}
        catch(System.ComponentModel.Win32Exception){}
        result.Add(new("Displays",screens>0?screens+" connected":"Display state unavailable"));
        result.Add(new("This widget",widget.IsLoaded?
            (widget.IsVisible?"Visible":"Loaded, hidden"):"Not yet loaded"));
        result.Add(new("Tray icon",trayVisible?"Registered as visible":"Not currently visible"));
        result.Add(new("Clipboard signal",DeskSpaceClipboardSignalV175.IsConnected?
            "Windows change listener active; "+DeskSpaceClipboardSignalV175.Changes+
            " notifications (contents never read)":"Windows change listener unavailable"));
        int held=0;
        try{held=DeskSpaceDropShelfV15.ActivePaths.Count;}catch(Exception ex)
            when(ex is InvalidOperationException or ObjectDisposedException){}
        result.Add(new("Shared Deck",held+" held item(s); contents not inspected"));
        int runningExcel=0;
        try
        {
            var processes=Process.GetProcessesByName("EXCEL");
            runningExcel=processes.Length;
            foreach(var p in processes)p.Dispose();
        }
        catch(Exception ex)when(ex is InvalidOperationException or
            System.ComponentModel.Win32Exception){}
        result.Add(new("Excel",runningExcel>0?
            "Excel process detected; workbook not inspected":"No Excel process detected"));
        result.Add(new("File search","Index health not yet independently tested"));
        bool workspace=false;
        try{workspace=Directory.Exists(WorkspaceStore.Root);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
        result.Add(new("Local workspace",workspace?
            "Local data folder exists":"Data folder not currently detected"));
        var update=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","update-status.log");
        result.Add(new("Updater",File.Exists(update)?
            "Local update activity log found (not read)":
            "No local update activity log found"));
        result.Add(new("Crash diagnostics",File.Exists(Path.Combine(
            DeskSpaceDiagnosticsV161.Root,"crash-types.log"))?
            "Local crash categories recorded (details hidden)":
            "No crash-category file recorded"));
        return result;
    }
    internal static Window Create(Window owner,bool trayVisible)
    {
        var title=new TextBlock{Text="Component status",
            Foreground=Brushes.White,FontSize=20,
            FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,5)};
        var hint=new TextBlock{
            Text="On-demand local checks only. No clipboard, Excel, search, "+
                "account or saved-content data is read or uploaded.",
            Foreground=Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,
            FontSize=11,Margin=new Thickness(0,0,0,12)};
        var list=new StackPanel();
        void Refresh()
        {
            list.Children.Clear();
            foreach(var row in Snapshot(owner,trayVisible))
            {
                var card=new StackPanel{Margin=new Thickness(0,0,0,10)};
                card.Children.Add(new TextBlock{
                    Text=row.Name,FontWeight=FontWeights.SemiBold,
                    Foreground=Brushes.White,FontSize=12});
                card.Children.Add(new TextBlock{
                    Text=row.Detail,Foreground=Brushes.Gainsboro,
                    FontSize=11,TextWrapping=TextWrapping.Wrap});
                list.Children.Add(card);
            }
        }
        var refresh=new Button{Content="Refresh",
            Margin=new Thickness(0,0,8,0),Padding=new Thickness(12,6,12,6)};
        refresh.Click+=(_,_)=>Refresh();
        var export=new Button{Content="Export safe diagnostics to Downloads",
            Padding=new Thickness(10,6,10,6)};
        export.Click+=(_,_)=>{
            try
            {
                var path=DeskSpaceDiagnosticsV161.ExportToDownloads();
                MessageBox.Show("Diagnostic ZIP saved to Downloads: "+
                    Path.GetFileName(path)+"\nNo private contents were exported.",
                    "DeskSpace diagnostics",MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Diagnostic export failed ("+ex.GetType().Name+").",
                    "DeskSpace diagnostics",MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
        var controls=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,8,0,0)};
        controls.Children.Add(refresh);
        controls.Children.Add(export);
        var body=new StackPanel{Margin=new Thickness(16)};
        body.Children.Add(title);body.Children.Add(hint);
        body.Children.Add(list);body.Children.Add(controls);
        var window=new Window{
            Title="DeskSpace · Component status",
            Width=440,Height=510,MinWidth=380,MinHeight=340,
            ShowInTaskbar=true,
            Background=new SolidColorBrush(Color.FromRgb(24,33,60)),
            Content=new ScrollViewer{Content=body,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto},
            WindowStartupLocation=WindowStartupLocation.CenterScreen
        };
        if(owner.IsVisible)
        {
            window.Owner=owner;
            window.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            window.Topmost=owner.Topmost;
        }
        Refresh();
        return window;
    }
    internal static void Show(Window owner,bool trayVisible)
    {
        if(Open.TryGetValue(owner,out var prior))
        {
            if(prior.IsLoaded){prior.Activate();return;}
            Open.Remove(owner);
        }
        var window=Create(owner,trayVisible);
        Open[owner]=window;
        window.Closed+=(_,_)=>Open.Remove(owner);
        // Never lock the tray or application thread with ShowDialog.
        window.Show();
    }
}
