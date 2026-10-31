using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Session-only context shared by the existing two widget Deck views.
// Monitoring is OFF unless a user explicitly clicks Start inside Deck.
// The only observed datum is a coarse category of the foreground process.
// Never retain window titles, document paths, URLs, clipboard or content.
internal static class DeskSpaceSmartContextDeckV280
{
    internal sealed record Suggestion(string Id,string Label);
    internal sealed record Snapshot(bool Listening,string Category,int Events);
    private const uint ForegroundEvent=3,OutOfContext=0,SkipOwnProcess=2;
    private static readonly object Gate=new();
    private static readonly Dictionary<string,Suggestion[]> Suggestions=
        new(StringComparer.Ordinal)
        {
            ["Excel"]=new[]{
                new Suggestion("excel","Open Excel mappings"),
                new Suggestion("clipboard","Clipboard conversions")},
            ["Files"]=new[]{
                new Suggestion("search","Universal search"),
                new Suggestion("preview","Preview selected file or folder")},
            ["Documents"]=new[]{
                new Suggestion("clipboard","Clipboard conversions"),
                new Suggestion("notes","Open notes")},
            ["Presentations"]=new[]{
                new Suggestion("clipboard","Clipboard conversions"),
                new Suggestion("palette","Action palette")},
            ["Browser"]=new[]{
                new Suggestion("notes","Open notes"),
                new Suggestion("palette","Action palette")},
            ["Media"]=new[]{
                new Suggestion("palette","Action palette")}
        };

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(IntPtr hook,uint evt,IntPtr hwnd,
        int objectId,int childId,uint thread,uint eventTime);

    [DllImport("user32.dll",SetLastError=true)]
    private static extern IntPtr SetWinEventHook(uint minimum,uint maximum,
        IntPtr module,WinEventProc callback,uint processId,
        uint threadId,uint flags);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hwnd,out uint processId);

    private static IntPtr activeHook;
    private static WinEventProc? callback;
    private static Dispatcher? ui;
    private static long session;
    private static string category="None";
    private static int events;
    internal static event Action? Changed;

    internal static Snapshot State
    {
        get
        {
            lock(Gate)return new(activeHook!=IntPtr.Zero,
                activeHook!=IntPtr.Zero?category:"None",
                activeHook!=IntPtr.Zero?events:0);
        }
    }

    internal static IReadOnlyList<Suggestion> Actions(string? name)
    {
        if(name==null || !Suggestions.TryGetValue(name,out var found))
            return Array.Empty<Suggestion>();
        // A category never supplies executable instructions or file paths.
        return found;
    }

    internal static bool Start(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if(!dispatcher.CheckAccess())return false;
        lock(Gate)
        {
            if(activeHook!=IntPtr.Zero)return true;
            callback=Observed;
            ui=dispatcher;
            category="None";
            events=0;
            session++;
            try
            {
                activeHook=SetWinEventHook(ForegroundEvent,ForegroundEvent,
                    IntPtr.Zero,callback,0,0,OutOfContext|SkipOwnProcess);
            }
            catch(Exception e) when(e is Win32Exception or
                DllNotFoundException or EntryPointNotFoundException)
            {
                Debug.WriteLine("Context Deck listener unavailable: "+
                    e.GetType().Name);
                activeHook=IntPtr.Zero;
            }
            if(activeHook==IntPtr.Zero)
            {
                callback=null;ui=null;
                return false;
            }
        }
        Changed?.Invoke();
        return true;
    }

    private static void Observed(IntPtr source,uint evt,IntPtr hwnd,
        int objectId,int childId,uint thread,uint eventTime)
    {
        if(evt!=ForegroundEvent || hwnd==IntPtr.Zero ||
           objectId!=0 || childId!=0)return;
        Dispatcher? target;
        long version;
        lock(Gate)
        {
            if(activeHook==IntPtr.Zero)return;
            version=session;
            target=ui;
        }
        if(target==null || target.HasShutdownStarted)return;
        string proposed="None";
        try
        {
            GetWindowThreadProcessId(hwnd,out uint pid);
            if(pid==0 || pid==Environment.ProcessId)return;
            using var process=Process.GetProcessById(checked((int)pid));
            if(process.HasExited)return;
            proposed=DeskSpaceContextSuggestionsV237.Classify(
                process.ProcessName);
            if(proposed=="Other")proposed="None";
        }
        catch(Exception e) when(e is ArgumentException or
            OverflowException or InvalidOperationException or Win32Exception or
            UnauthorizedAccessException){return;}
        try
        {
            target.BeginInvoke(new Action(()=>{
                bool changed=false;
                lock(Gate)
                {
                    if(activeHook==IntPtr.Zero || session!=version)return;
                    events=Math.Min(10000,events+1);
                    changed=!string.Equals(category,proposed,
                        StringComparison.Ordinal);
                    category=proposed;
                }
                // Refresh only for category changes, not each window event.
                if(changed)Changed?.Invoke();
            }),DispatcherPriority.Background);
        }
        catch(InvalidOperationException){}
    }

    internal static void Stop()
    {
        IntPtr old;
        lock(Gate)
        {
            old=activeHook;activeHook=IntPtr.Zero;
            session++;
            callback=null;ui=null;
            category="None";events=0;
        }
        if(old!=IntPtr.Zero)
        {
            try{UnhookWinEvent(old);}
            catch(Exception ex) when(ex is Win32Exception or
                DllNotFoundException or EntryPointNotFoundException)
            {Debug.WriteLine("Context Deck unhook: "+ex.GetType().Name);}
        }
        Changed?.Invoke();
    }

    // The active context appears INSIDE the existing Deck, not in another
    // independent tool window. Actions are a fixed local whitelist and only
    // launch following a real click; none writes Excel cells automatically.
    internal static Border CreatePanel(Window owner,Action refresh)
    {
        var state=State;
        var content=new StackPanel{Margin=new Thickness(8,6,8,7)};
        var header=new DockPanel{LastChildFill=true};
        var toggle=new Button{
            Content=state.Listening?"Stop":"Start",
            Padding=new Thickness(7,3,7,3),
            MinWidth=48,Margin=new Thickness(5,0,0,0),
            ToolTip=state.Listening?
                "Stop listening and erase this session's context":
                "Start process-category foreground listener; session only"};
        AutomationProperties.SetName(toggle,state.Listening?
            "Stop Smart Context Deck monitoring":
            "Start Smart Context Deck monitoring");
        DockPanel.SetDock(toggle,Dock.Right);
        header.Children.Add(toggle);
        header.Children.Add(new TextBlock{
            Text=state.Listening?"Smart Context · "+state.Category:
                "Smart Context · Off",
            FontWeight=FontWeights.SemiBold,FontSize=11,
            Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center
        });
        content.Children.Add(header);
        var status=new TextBlock{
            Text=state.Listening?
                "Suggestions are local, read-only recommendations. "+
                "Choose any action explicitly.":
                "Opt in to suggestions based only on the active app category.",
            FontSize=10,TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,5,0,5)
        };
        content.Children.Add(status);
        toggle.Click+=(_,_)=>{
            if(state.Listening)Stop();
            else if(!Start(owner.Dispatcher))
            {
                status.Text="Windows foreground listener unavailable. "+
                    "No monitoring started.";
                refresh();
            }
        };
        if(state.Listening)
        {
            foreach(var suggestion in Actions(state.Category))
            {
                var entry=suggestion;
                var button=new Button{
                    Content=entry.Label,FontSize=11,
                    Padding=new Thickness(6,5,6,5),
                    Margin=new Thickness(0,2,0,2),
                    HorizontalContentAlignment=HorizontalAlignment.Left,
                    ToolTip="Open this DeskSpace tool only when clicked; "+
                        "no automatic data access or paste"
                };
                AutomationProperties.SetName(button,
                    "Smart Context suggestion: "+entry.Label);
                button.Click+=(_,_)=>Open(owner,entry.Id);
                content.Children.Add(button);
            }
            content.Children.Add(new TextBlock{
                Text=Audit(state),FontSize=9,
                TextWrapping=TextWrapping.Wrap,
                Foreground=Brushes.LightSteelBlue,
                Margin=new Thickness(0,5,0,0)
            });
        }
        return new Border{
            Child=content,
            Background=new SolidColorBrush(Color.FromRgb(39,53,83)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(72,88,125)),
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(7),
            Margin=new Thickness(2,3,2,4)
        };
    }

    internal static void Open(Window owner,string id)
    {
        switch(id)
        {
            case "excel":
                _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner);
                break;
            case "clipboard":
                DeskSpaceClipboardTransformsV177.Show(owner);
                break;
            case "search":
            case "palette":
                DeskSpaceActionPaletteV197.Show(owner,false);
                break;
            case "preview":
                DeskSpacePreviewChooserV273.Show(owner);
                break;
            case "notes":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
            default:
                throw new ArgumentException(
                    "Unknown context action. No operation performed.");
        }
    }

    internal static string Audit(Snapshot state)=>
        DeskSpaceContextAuditV258.Summary(
            state.Listening,state.Events,state.Category);
}
