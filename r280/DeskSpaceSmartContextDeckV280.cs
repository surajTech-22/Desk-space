using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

    internal static string Audit(Snapshot state)=>
        DeskSpaceContextAuditV258.Summary(
            state.Listening,state.Events,state.Category);
}
