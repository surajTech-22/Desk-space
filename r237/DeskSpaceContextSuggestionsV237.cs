using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R237: user-initiated and session-only foreground application suggestions.
// No window titles, URLs, paths, documents, clipboard or passwords are read.
// No background observer is installed unless the user presses Start; the
// listener is removed when the panel closes or the user presses Stop.
internal static class DeskSpaceContextSuggestionsV237
{
    private const uint ForegroundEvent=0x0003;
    private const uint OutOfContext=0;
    private const uint SkipOwnProcess=2;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(IntPtr hook,uint evt,IntPtr hwnd,
        int idObject,int idChild,uint thread,uint eventTime);

    [DllImport("user32.dll",SetLastError=true)]
    private static extern IntPtr SetWinEventHook(uint min,uint max,
        IntPtr module,WinEventProc callback,uint processId,
        uint threadId,uint flags);

    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd,
        out uint processId);

    internal static string Classify(string? processName)
    {
        string n=(processName??"").Trim().ToLowerInvariant();
        return n switch
        {
            "excel"=>"Excel",
            "winword"=>"Documents",
            "powerpnt"=>"Presentations",
            "explorer"=>"Files",
            "chrome" or "msedge" or "firefox" or "brave" or
                "opera" or "vivaldi"=>"Browser",
            "vlc" or "spotify" or "wmplayer" or "music.ui"=>"Media",
            _=>"Other"
        };
    }

    private sealed class Session : IDisposable
    {
        private readonly Window dialog;
        private readonly Window owner;
        private readonly TextBlock status;
        private readonly StackPanel suggestions;
        private IntPtr hook;
        private WinEventProc? callback;
        private string category="None";
        private bool disposed;

        internal Session(Window dialog,Window owner,TextBlock status,
            StackPanel suggestions)
        {
            this.dialog=dialog;this.owner=owner;
            this.status=status;this.suggestions=suggestions;
        }
        internal void Start()
        {
            if(hook!=IntPtr.Zero || disposed)return;
            callback=OnForeground;
            hook=SetWinEventHook(ForegroundEvent,ForegroundEvent,
                IntPtr.Zero,callback,0,0,OutOfContext|SkipOwnProcess);
            if(hook==IntPtr.Zero)
            {
                callback=null;
                status.Text="Windows foreground notifications unavailable. No monitoring started.";
                return;
            }
            status.Text="Listening only while this panel is open. Switch to Excel, "+
                "Explorer, a browser, or another application, then return here.";
        }
        private void OnForeground(IntPtr ignored,uint evt,IntPtr hwnd,
            int objectId,int childId,uint thread,uint eventTime)
        {
            if(disposed || evt!=ForegroundEvent || hwnd==IntPtr.Zero ||
                objectId!=0 || childId!=0)return;
            try
            {
                GetWindowThreadProcessId(hwnd,out uint pid);
                if(pid==0 || pid==Environment.ProcessId)return;
                using var process=Process.GetProcessById((int)pid);
                string proposed=Classify(process.ProcessName);
                if(proposed=="Other")return;
                dialog.Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(disposed || !dialog.IsLoaded)return;
                    category=proposed;UpdateSuggestions();
                }));
            }
            catch(Exception ex)when(ex is ArgumentException or
                InvalidOperationException or System.ComponentModel.Win32Exception or
                UnauthorizedAccessException)
            {
                // Access-restricted programs are intentionally ignored.
            }
        }
        private void Add(string caption,Action onClick)
        {
            var button=new Button{
                Content=caption,Margin=new Thickness(0,0,0,7),
                Padding=new Thickness(8),MinHeight=34,
                HorizontalContentAlignment=HorizontalAlignment.Left};
            button.Click+=(_,_)=>onClick();
            suggestions.Children.Add(button);
        }
        private void UpdateSuggestions()
        {
            suggestions.Children.Clear();
            status.Text="Last recognized application type: "+category+
                ". Recommendations only; no action has been executed.";
            if(category=="Excel")
            {
                Add("Open saved Excel mappings",()=>
                    _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner));
                Add("Open clipboard conversions",()=>
                    DeskSpaceClipboardTransformsV177.Show(owner));
            }
            else if(category is "Documents" or "Presentations" or "Files")
            {
                Add("Open clipboard conversions",()=>
                    DeskSpaceClipboardTransformsV177.Show(owner));
                Add("Open command palette",()=>
                    DeskSpaceActionPaletteV197.Show(owner,false));
            }
            else if(category=="Browser")
            {
                Add("Open command palette",()=>
                    DeskSpaceActionPaletteV197.Show(owner,false));
                Add("Open DeskSpace notes",()=>
                    _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES"));
            }
            else
            {
                Add("Open command palette",()=>
                    DeskSpaceActionPaletteV197.Show(owner,false));
            }
        }
        internal void Stop()
        {
            if(hook!=IntPtr.Zero)
            {
                UnhookWinEvent(hook);
                hook=IntPtr.Zero;
            }
            callback=null;
            if(!disposed)
                status.Text="Stopped. No foreground application monitoring is active.";
        }
        public void Dispose()
        {
            if(disposed)return;
            Stop();
            disposed=true;
        }
    }

    internal static void Show(Window owner)
    {
        var panel=new Window{
            Title="DeskSpace — Context Suggestions (Opt-in)",
            Width=470,Height=360,MinWidth=360,MinHeight=300,
            Owner=owner,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ResizeMode=ResizeMode.CanResize};
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(new TextBlock{
            Text="Suggest relevant DeskSpace tools from the active app",
            TextWrapping=TextWrapping.Wrap,FontSize=16,
            FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,10)});
        content.Children.Add(new TextBlock{
            Text="Explicit opt-in for this window only. Reads an app's process "+
                 "category, not its title, opened files, tabs, URLs or content. "+
                 "Nothing is saved, sent or pasted.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var status=new TextBlock{
            Text="Off. Start listening, switch to another app, then return.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        content.Children.Add(status);
        var controls=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,0,0,15)};
        var start=new Button{Content="Start session",Padding=new Thickness(10,5,10,5),
            Margin=new Thickness(0,0,8,0)};
        var stop=new Button{Content="Stop",Padding=new Thickness(10,5,10,5)};
        controls.Children.Add(start);controls.Children.Add(stop);
        content.Children.Add(controls);
        var suggestions=new StackPanel();
        content.Children.Add(suggestions);
        panel.Content=new ScrollViewer{
            Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var session=new Session(panel,owner,status,suggestions);
        start.Click+=(_,_)=>session.Start();
        stop.Click+=(_,_)=>session.Stop();
        panel.Closed+=(_,_)=>session.Dispose();
        panel.Show();
    }
}
