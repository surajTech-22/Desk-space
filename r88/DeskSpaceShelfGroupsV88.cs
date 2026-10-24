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

// A running app means a user-visible top-level Windows app, NOT every
// background service or process. Each executable is shown only once.
internal static class DeskSpaceRunningAppsV88
{
    internal sealed record Entry(string Name,string Path,IntPtr Handle,string WindowTitle);
    private const int Restore=9;
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle,out WindowRect rectangle);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,
        out int value,int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        internal int Left,Top,Right,Bottom;
    }
    private const int DwmCloaked=14;

    // A background app process can retain its MainWindowHandle even after its
    // window closes. Exclude non-visible and DWM-cloaked windows.
    internal static bool IsActiveWindow(IntPtr handle)
    {
        if(handle==IntPtr.Zero || !IsWindow(handle) ||
            !IsWindowVisible(handle))return false;
        try
        {
            if(DwmGetWindowAttribute(handle,DwmCloaked,out int cloak,sizeof(int))==0 &&
                cloak!=0)return false;
        }
        catch(DllNotFoundException){}
        catch(EntryPointNotFoundException){}
        if(!IsIconic(handle) && GetWindowRect(handle,out var rect) &&
            (rect.Right-rect.Left<32 || rect.Bottom-rect.Top<32))return false;
        return true;
    }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr handle,int command);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    internal static IReadOnlyList<Entry> Snapshot()
    {
        var found=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try{processes=Process.GetProcesses();}
        catch(InvalidOperationException){return Array.Empty<Entry>();}
        catch(Win32Exception){return Array.Empty<Entry>();}
        foreach(var process in processes)
        {
            using(process)
            {
                try
                {
                    if(process.Id==Environment.ProcessId || process.HasExited)continue;
                    process.Refresh();
                    IntPtr window=process.MainWindowHandle;
                    if(!IsActiveWindow(window))continue;
                    string title=process.MainWindowTitle;
                    if(string.IsNullOrWhiteSpace(title))continue;
                    string name=process.ProcessName;
                    if(name is "ApplicationFrameHost" or "ShellExperienceHost" or
                        "SearchHost" or "TextInputHost" or "RuntimeBroker")continue;
                    if(found.ContainsKey(name))continue;
                    string path="";
                    try{path=process.MainModule?.FileName??"";}
                    catch(Win32Exception){}
                    catch(InvalidOperationException){}
                    string label=name;
                    if(!string.IsNullOrWhiteSpace(path))
                    {
                        try
                        {
                            var description=FileVersionInfo.GetVersionInfo(path).FileDescription;
                            if(!string.IsNullOrWhiteSpace(description) &&
                                description.Length<=44)label=description.Trim();
                        }
                        catch(Win32Exception){}
                        catch(IOException){}
                    }
                    found[name]=new Entry(label,path,window,title);
                }
                catch(Win32Exception){}
                catch(InvalidOperationException){}
                catch(UnauthorizedAccessException){}
            }
        }
        return found.Values.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
            .Take(20).ToArray();
    }

    internal static void Focus(Entry entry)
    {
        // The user clicked an app. Never start a second instance.
        if(!IsActiveWindow(entry.Handle))return;
        ShowWindowAsync(entry.Handle,Restore);
        SetForegroundWindow(entry.Handle);
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
                TextAlignment=TextAlignment.Center,
                ToolTip=app.Name
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
}
