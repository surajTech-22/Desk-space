$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r276/DeskSpaceStartupFallbackV276.cs' "$root/DeskSpaceStartupFallbackV276.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $count=([regex]::Matches($source,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R276 '+$why+' expected one anchor, saw '+$count)}
 [IO.File]::WriteAllText($path,$source.Replace($old,$next),
    [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceStartupRecoveryV183.cs" @'
// Startup-only crash-loop recovery. This is NOT a complete crash watchdog:
// crashes before the primary widget loads or after the startup grace period
// require separate diagnostics. Never deletes or rewrites workspace data.
'@ @'
// Startup-only crash-loop recovery, including failures before widget Loaded.
// This is not a general runtime watchdog and never edits workspace data.
'@ 'early recovery'
Swap "$root/DeskSpaceStartupRecoveryV183.cs" @'
    private static bool started,safeMode,tracking;
'@ @'
    private static bool started,attached,safeMode,tracking;
'@ 'early recovery'
Swap "$root/DeskSpaceStartupRecoveryV183.cs" @'
    internal static void Start(Window owner)
    {
        // Only the primary widget calls Start; satellite windows do not write
        // another session or overwrite the primary crash-loop evidence.
        lock(Gate)
        {
            if(started)return;
            started=true;
            try
            {
                session=NextStart(Read(),DateTime.UtcNow);
                Persist(session); // fail open if the marker cannot be written
                tracking=true;
                safeMode=session.SafeMode;
            }
            catch(Exception error) when(error is IOException or
                UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                tracking=false;safeMode=false;
                Debug.WriteLine("DeskSpace startup recovery unavailable: "+
                    error.GetType().Name);
            }
        }
        var grace=
'@ @'
    // Called once by the primary widget-host process, after acquiring
    // its single-instance mutex and BEFORE WidgetShellWindow construction.
    // A crash in the constructor is now counted on the next launch.
    internal static void BeginEarly()
    {
        lock(Gate)
        {
            if(started)return;
            started=true;
            try
            {
                session=NextStart(Read(),DateTime.UtcNow);
                Persist(session);
                tracking=true;
                safeMode=session.SafeMode;
            }
            catch(Exception error) when(error is IOException or
                UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                tracking=false;safeMode=false;
                Debug.WriteLine("DeskSpace startup recovery unavailable: "+
                    error.GetType().Name);
            }
        }
    }

    internal static void MarkCleanExit()
    {
        // Never clear crash evidence for failed constructors or unrelated
        // Workspace and secondary widget processes.
        lock(Gate)
        {
            if(attached)MarkHealthy();
        }
    }

    internal static void Start(Window owner)
    {
        // Called by the primary widget Loaded handler after BeginEarly.
        // Secondary monitors must never create another recovery marker.
        BeginEarly();
        lock(Gate)
        {
            if(attached)return;
            attached=true;
        }
        var grace=
'@ 'early recovery'
Swap "$root/App.xaml.cs" @'
using System.Diagnostics;
using System.Windows;
'@ @'
using System.Diagnostics;
using System.IO;
using System.Windows;
'@ 'early recovery'
Swap "$root/App.xaml.cs" @'
    private Mutex? widgetMutex;
'@ @'
    private Mutex? widgetMutex;
    private bool widgetStarted;
'@ 'early recovery'
Swap "$root/App.xaml.cs" @'
            var window=new WidgetShellWindow();MainWindow=window;window.Show();return;
'@ @'
            // Persist the short-start marker before widget/tray creation.
            // Failure in the constructor now contributes to safe recovery.
            DeskSpaceStartupRecoveryV183.BeginEarly();
            try
            {
                var window=new WidgetShellWindow();
                MainWindow=window;
                window.Show();
                widgetStarted=true;
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception or
                System.Runtime.InteropServices.COMException or
                System.Windows.Markup.XamlParseException or
                TypeInitializationException)
            {
                Debug.WriteLine("DeskSpace widget early startup failed: "+
                    ex.GetType().Name);
                // Keep a usable, single-instance recovery window and tray
                // without deleting settings or entering a relaunch storm.
                DeskSpaceStartupFallbackV276.Show(ex.GetType().Name);
            }
            return;
'@ 'early recovery'
Swap "$root/App.xaml.cs" @'
    protected override void OnExit(ExitEventArgs e)
    {
        try {widgetMutex?.ReleaseMutex();}
'@ @'
    protected override void OnExit(ExitEventArgs e)
    {
        // Only a successfully shown widget can mark a normal process exit.
        // A failed constructor must remain recorded as an unhealthy start.
        if(widgetStarted)DeskSpaceStartupRecoveryV183.MarkCleanExit();
        try {widgetMutex?.ReleaseMutex();}
'@ 'early recovery'
Write-Host 'R276: primary widget marks pre-constructor crashes, serves independent recovery window and tray, and marks only successful clean exits.'