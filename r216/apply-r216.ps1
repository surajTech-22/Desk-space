$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMultiMonitorV151.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R216 "+$reason+" expected exactly one anchor, found "+$count)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
    private static DispatcherTimer? poll;
'@ @'
    private static DispatcherTimer? poll;
    private static FileSystemWatcher? preferenceWatcherV216;
    private static DispatcherTimer? preferenceDebounceV216;
    private static readonly TimeSpan ReconcileFallbackV216=TimeSpan.FromSeconds(45);
'@ 'event watcher ownership'
Swap @'
    internal static void Register(WidgetShellWindow widget)
'@ @'
    // FileSystemWatcher can occasionally overflow or miss an atomic rename.
    // A slow recovery pass remains in place; the normal path is event-driven.
    private static void SchedulePreferenceRefreshV216()
    {
        var owner=primary;
        if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
        try
        {
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(primary==null || !ReferenceEquals(primary,owner))return;
                preferenceDebounceV216?.Stop();
                preferenceDebounceV216?.Start();
            }));
        }
        catch(InvalidOperationException){}
    }
    private static void StartPreferenceWatcherV216()
    {
        if(primary==null || preferenceWatcherV216!=null)return;
        try
        {
            if(!Directory.Exists(WorkspaceStore.Root))return;
            var watcher=new FileSystemWatcher(WorkspaceStore.Root,
                "widget-displays-v151.json"){
                NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|
                    NotifyFilters.CreationTime,
                IncludeSubdirectories=false
            };
            watcher.Changed+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Created+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Deleted+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Renamed+=(_,_)=>SchedulePreferenceRefreshV216();
            watcher.Error+=(_,_)=>{
                SchedulePreferenceRefreshV216();
                // Fallback still reads preferences if FileSystemWatcher fails.
                // Do not dispose on the watcher callback thread; the next
                // dispatcher tick cleans up and may recreate it.
                var owner=primary;
                if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
                try{owner.Dispatcher.BeginInvoke(new Action(()=>{
                    preferenceWatcherV216?.Dispose();
                    preferenceWatcherV216=null;
                }));}
                catch(InvalidOperationException){}
            };
            watcher.EnableRaisingEvents=true;
            preferenceWatcherV216=watcher;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Debug.WriteLine(
                "DeskSpace display preference signal unavailable: "+ex.GetType().Name);
        }
    }
    private static void StopPreferenceWatcherV216()
    {
        preferenceDebounceV216?.Stop();
        preferenceDebounceV216=null;
        preferenceWatcherV216?.Dispose();
        preferenceWatcherV216=null;
    }

    internal static void Register(WidgetShellWindow widget)
'@ 'event-based settings watcher lifecycle'
Swap @'
        poll=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher){
            Interval=TimeSpan.FromSeconds(3)};
'@ @'
        preferenceDebounceV216=new DispatcherTimer(
            DispatcherPriority.Background,widget.Dispatcher){
            Interval=TimeSpan.FromMilliseconds(180)};
        preferenceDebounceV216.Tick+=(_,_)=>{
            preferenceDebounceV216?.Stop();
            if(primary==null)return;
            try
            {
                ReadPreference();
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
        StartPreferenceWatcherV216();
        poll=new DispatcherTimer(DispatcherPriority.Background,widget.Dispatcher){
            Interval=ReconcileFallbackV216};
'@ 'slow watchdog and fast settings signal'
Swap @'
        poll.Tick+=(_,_)=>{
            if(primary==null)return;
            try
            {
                var stamp=File.Exists(Filename)?File.GetLastWriteTimeUtc(Filename):
                    DateTime.MinValue;
                if(stamp!=diskStamp)ReadPreference();
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
'@ @'
        poll.Tick+=(_,_)=>{
            if(primary==null)return;
            try
            {
                if(preferenceWatcherV216==null)StartPreferenceWatcherV216();
                var stamp=File.Exists(Filename)?File.GetLastWriteTimeUtc(Filename):
                    DateTime.MinValue;
                if(stamp!=diskStamp)ReadPreference();
                // Rare missed notification, owner close or monitor reconnect:
                // keep this slow check as a safety net, not the normal path.
                Reconcile();
            }
            catch(Exception error){Log(error);}
        };
'@ 'file event plus recovery fallback'
Swap @'
            poll?.Stop();poll=null;
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
'@ @'
            poll?.Stop();poll=null;
            StopPreferenceWatcherV216();
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
'@ 'reliable disposal on widget exit'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R216: event-driven monitor preferences with 180ms debounce, 45s recovery fallback and watcher disposal.'
