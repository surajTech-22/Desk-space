using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// Small grouped headings INSIDE the existing below-circle shelf.
// Does not create a second dock/window or change the stored file snapshots.
internal static class DeskSpaceShelfGroupsV88
{
    internal static StackPanel Add(StackPanel root,string label,int count,
        bool expanded,Action toggle)
    {
        var content=new StackPanel{
            Visibility=expanded?Visibility.Visible:Visibility.Collapsed
        };
        if(count<=0)return content; // Hide genuinely empty categories.
        var header=new Button{
            Content=(expanded?"▾  ":"▸  ")+label+"  ·  "+count,
            FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,
            Background=new SolidColorBrush(Color.FromArgb(185,38,53,83)),
            BorderThickness=new Thickness(0),
            HorizontalContentAlignment=HorizontalAlignment.Left,
            HorizontalAlignment=HorizontalAlignment.Stretch,
            Padding=new Thickness(7,5,6,5),
            Margin=new Thickness(1,2,1,2),
            ToolTip="Collapse or expand "+label+" without removing items"
        };
        header.Click+=(_,_)=>toggle();
        root.Children.Add(header);
        if(expanded)root.Children.Add(content);
        return content;
    }
}

// Enumerate actual taskbar-like top-level windows instead of
// Process.MainWindowHandle. The latter misses Explorer/packaged apps and
// returns too few results when several visible windows share a process.
internal static class DeskSpaceRunningAppsV88
{
    internal sealed record Entry(string Name,string Path,IntPtr Handle,string WindowTitle);
    private const int Restore=9, Minimise=6, GwlExStyle=-20, GwOwner=4;
    private const uint WmClose=0x0010;
    private const long WsExToolWindow=0x80, WsExAppWindow=0x40000;
    private const int DwmCloaked=14;
    private delegate bool WindowEnumerator(IntPtr window,IntPtr context);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        internal int Left,Top,Right,Bottom;
    }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowEnumerator callback,IntPtr context);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window,out WindowRect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window,
        System.Text.StringBuilder output,int length);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window,
        System.Text.StringBuilder output,int length);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr window,int index);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,
        out int value,int size);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window,uint message,
        IntPtr wParam,IntPtr lParam);
    private static IntPtr lastSelectedWindow=IntPtr.Zero;

    private static long WindowStyle(IntPtr window)=>
        IntPtr.Size==8?GetWindowLongPtr64(window,GwlExStyle).ToInt64():
            (uint)GetWindowLong32(window,GwlExStyle);

    // A minimised application is still running. A cloaked or tool-only
    // window is not a user-visible application and should not be listed.
    internal static bool IsActiveWindow(IntPtr window)
    {
        if(window==IntPtr.Zero || !IsWindow(window) ||
           !IsWindowVisible(window))return false;
        try
        {
            if(DwmGetWindowAttribute(window,DwmCloaked,
                out int cloak,sizeof(int))==0 && cloak!=0)return false;
        }
        catch(DllNotFoundException){}
        catch(EntryPointNotFoundException){}
        var style=WindowStyle(window);
        if((style&WsExToolWindow)!=0 && (style&WsExAppWindow)==0)return false;
        if(GetWindow(window,GwOwner)!=IntPtr.Zero &&
           (style&WsExAppWindow)==0)return false;
        if(!IsIconic(window) && GetWindowRect(window,out var rect) &&
           (rect.Right-rect.Left<32 || rect.Bottom-rect.Top<32))return false;
        return true;
    }

    private static string WindowText(IntPtr window)
    {
        int n=GetWindowTextLength(window);
        if(n<=0)return "";
        var buffer=new System.Text.StringBuilder(Math.Min(n+1,513));
        return GetWindowText(window,buffer,buffer.Capacity)>0?
            buffer.ToString().Trim():"";
    }
    private static string WindowClass(IntPtr window)
    {
        var buffer=new System.Text.StringBuilder(128);
        return GetClassName(window,buffer,buffer.Capacity)>0?
            buffer.ToString():"";
    }

    // Apply the same normalisation to both displayed icons and count.
    // Each distinct app is displayed once, even if it has many windows.
    internal static IReadOnlyList<Entry> GroupVisible(IEnumerable<Entry> windows)=>
        DeskSpaceAppGroupOrderV261.Groups(windows);

    internal static IReadOnlyList<Entry> Snapshot()
    {
        var windows=SnapshotWindows();
        return GroupVisible(windows);
    }

    // Retain every individual HWND so hovering a grouped app exposes all
    // of its windows; the old Snapshot still provides the distinct app count.
    internal static IReadOnlyList<Entry> SnapshotWindows()
    {
        var windows=new List<Entry>();
        try
        {
            EnumWindows((window,_)=>{
                if(!IsActiveWindow(window))return true;
                string title=WindowText(window);
                if(title.Length==0)return true;
                string windowClass=WindowClass(window);
                if(windowClass is "Progman" or "WorkerW" or
                   "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")return true;
                GetWindowThreadProcessId(window,out uint pid);
                if(pid==0 || pid==(uint)Environment.ProcessId)return true;
                try
                {
                    using var process=Process.GetProcessById((int)pid);
                    if(process.HasExited)return true;
                    string name=process.ProcessName;
                    if(name.Equals("DeskSpace",StringComparison.OrdinalIgnoreCase) ||
                       name is "ShellExperienceHost" or "SearchHost" or
                           "TextInputHost" or "RuntimeBroker")return true;
                    // Windows Settings and other packaged apps may be wrapped
                    // in an ApplicationFrameHost top-level HWND. Include them
                    // using their actual visible title, not that generic host.
                    bool packaged=name.Equals("ApplicationFrameHost",
                        StringComparison.OrdinalIgnoreCase);
                    if(packaged && windowClass!="ApplicationFrameWindow")return true;
                    string path="";
                    try{path=process.MainModule?.FileName??"";}
                    catch(Win32Exception){}
                    catch(InvalidOperationException){}
                    catch(UnauthorizedAccessException){}
                    string label=name;
                    if(packaged)label=title;
                    else if(name.Equals("explorer",StringComparison.OrdinalIgnoreCase))
                        label="File Explorer";
                    else if(name.Equals("SystemSettings",StringComparison.OrdinalIgnoreCase))
                        label="Settings";
                    else if(!string.IsNullOrWhiteSpace(path))
                    {
                        try
                        {
                            string desc=FileVersionInfo.GetVersionInfo(path).
                                FileDescription??"";
                            if(!string.IsNullOrWhiteSpace(desc) && desc.Length<=44)
                                label=desc.Trim();
                        }
                        catch(Win32Exception){}
                        catch(IOException){}
                    }
                    if(label.Length>44)label=label[..44];
                    windows.Add(new Entry(label,path,window,title));
                }
                catch(Win32Exception){}
                catch(ArgumentException){}
                catch(InvalidOperationException){}
                catch(UnauthorizedAccessException){}
                return true;
            },IntPtr.Zero);
        }
        catch(Win32Exception){}
        return windows.GroupBy(x=>x.Handle).Select(g=>g.First()).ToArray();
    }

    // P1: stale HWNDs can be recycled between Deck refreshes. Every
    // user-requested window action must revalidate the exact live title;
    // a reused handle must never close/minimize an unrelated app.
    internal static bool CanControlV279(Entry entry,bool exists,
        string actualTitle)=>
        entry.Handle!=IntPtr.Zero && exists &&
        !string.IsNullOrWhiteSpace(entry.WindowTitle) &&
        string.Equals(actualTitle,entry.WindowTitle,StringComparison.Ordinal);

    internal static bool RequestStateV279(Entry entry,bool minimize)
    {
        if(!IsActiveWindow(entry.Handle) ||
           !CanControlV279(entry,true,WindowText(entry.Handle)))return false;
        // Send an ordinary window-state request. This never kills a
        // process, opens a document, or changes another monitor's Deck.
        ShowWindowAsync(entry.Handle,minimize?Minimise:Restore);
        if(!minimize)SetForegroundWindow(entry.Handle);
        lastSelectedWindow=minimize?IntPtr.Zero:entry.Handle;
        return true;
    }

    // Request only a normal window close: apps can prompt for unsaved work.
    // Never terminate the process, and never close a reused HWND whose title
    // no longer matches the originally enumerated window.
    internal static bool RequestClose(Entry entry)
    {
        if(!IsActiveWindow(entry.Handle) ||
           !CanControlV279(entry,true,WindowText(entry.Handle)))return false;
        return PostMessage(entry.Handle,WmClose,IntPtr.Zero,IntPtr.Zero);
    }

    internal static void Focus(Entry entry)
    {
        if(!IsActiveWindow(entry.Handle) ||
           !CanControlV279(entry,true,WindowText(entry.Handle)))return;
        // Like a taskbar button: select to activate, select the same active
        // window again to minimise it. The last selection also covers cases
        // where the no-activate floating deck temporarily owns foreground.
        IntPtr foreground=GetForegroundWindow();
        bool alreadySelected=lastSelectedWindow==entry.Handle;
        bool active=foreground==entry.Handle;
        if(!IsIconic(entry.Handle) && (active || alreadySelected))
        {
            ShowWindowAsync(entry.Handle,Minimise);
            lastSelectedWindow=IntPtr.Zero;
            return;
        }
        ShowWindowAsync(entry.Handle,Restore);
        SetForegroundWindow(entry.Handle);
        lastSelectedWindow=entry.Handle;
    }

    internal static void Fill(StackPanel target,IReadOnlyList<Entry> apps)
    {
        var grid=new WrapPanel{Margin=new Thickness(1,2,1,3)};
        foreach(var app in apps)
        {
            var inner=new StackPanel{Width=66};
            var icon=DeskSpaceApplicationIconsV65.Get(app.Path);
            if(icon!=null)inner.Children.Add(new Image{
                Source=icon,Width=22,Height=22,
                HorizontalAlignment=HorizontalAlignment.Center,
                Margin=new Thickness(0,0,0,2)
            });
            else inner.Children.Add(new TextBlock{
                Text="▣",FontSize=18,Foreground=Brushes.LightSteelBlue,
                TextAlignment=TextAlignment.Center});
            inner.Children.Add(new TextBlock{
                Text=app.Name,FontSize=10,Foreground=Brushes.White,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextAlignment=TextAlignment.Center,ToolTip=app.Name
            });
            var button=new Button{
                Content=inner,Width=72,MinHeight=48,
                Padding=new Thickness(2),Margin=new Thickness(1),
                Background=new SolidColorBrush(Color.FromArgb(116,44,60,93)),
                BorderThickness=new Thickness(0),
                ToolTip=app.Name+" · "+app.WindowTitle+
                    "\nSwitch to this running application"
            };
            button.Click+=(_,_)=>Focus(app);
            grid.Children.Add(button);
        }
        target.Children.Add(grid);
    }
    // Hover-only running applications. Neither the category nor the app
    // list uses click-to-expand. Only an individual window is selectable.
    // Both flyout levels live inside the original deck, with no extra
    // floating top-level windows, keeping mouse travel stable.
    internal static IReadOnlyList<Entry> WindowsFor(Entry app,IReadOnlyList<Entry> windows)=>
        DeskSpaceAppGroupOrderV261.Windows(app,windows);

    // Keep this original three-argument method for callers and smoke tests.
    internal static void AddHoverGroup(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows);
    }

    // The deck uses a distinct method, avoiding reflection overload
    // ambiguity and preserving the original public method contract.
    internal static void AddHoverGroupLive(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows,
        Action<IReadOnlyList<Entry>,IReadOnlyList<Entry>> onLiveChanged,
        Action onHoverClosed)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows,
            onLiveChanged,onHoverClosed);
    }
}
