$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceMultiMonitorV151.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason) {
  $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
  $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
  if($n -ne 1){throw ("R247 "+$reason+": expected one anchor, got "+$n)}
  $script:s=$script:s.Replace($old,$new)
}
Swap @'
    private static FileSystemWatcher? preferenceWatcherV216;
'@ @'
    private static FileSystemWatcher? preferenceWatcherV216;
    // Sleep/resume can expose stale virtual-screen topology for several
    // seconds. Short, finite retries only, never an additional polling loop.
    private static readonly int[] ResumeDelaysV247={1500,5000,12000};
    private static DispatcherTimer? resumeTimerV247;
    private static int resumeAttemptV247;
    internal static int ResumeDelayForAttemptV247(int attempt)=>
        attempt>=0 && attempt<ResumeDelaysV247.Length?
            ResumeDelaysV247[attempt]:-1;
'@ 'bounded resume policy'
Swap @'
    internal static void Register(WidgetShellWindow widget)
'@ @'
    // Only the primary widget owns the recovery subscription. Do not change
    // assigned screen identity or copy other monitor's Deck positions.
    private static void OnPowerModeV247(object? sender,PowerModeChangedEventArgs args)
    {
        if(args.Mode!=PowerModes.Resume)return;
        var owner=primary;
        if(owner==null || owner.Dispatcher.HasShutdownStarted)return;
        try
        {
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(!ReferenceEquals(primary,owner))return;
                resumeAttemptV247=0;
                resumeTimerV247?.Stop();
                if(resumeTimerV247==null)
                {
                    resumeTimerV247=new DispatcherTimer(
                        DispatcherPriority.Background,owner.Dispatcher);
                    resumeTimerV247.Tick+=(_,_)=>{
                        var timer=resumeTimerV247;
                        timer?.Stop();
                        if(primary==null){resumeAttemptV247=0;return;}
                        try
                        {
                            // Re-read the saved display preference and then
                            // resolve current screens. Reconcile preserves
                            // per-display identity and independent positions.
                            ReadPreference();
                            Reconcile();
                        }
                        catch(Exception error){Log(error);}
                        resumeAttemptV247++;
                        int next=ResumeDelayForAttemptV247(resumeAttemptV247);
                        if(next>0 && resumeTimerV247!=null && primary!=null)
                        {
                            resumeTimerV247.Interval=TimeSpan.FromMilliseconds(next);
                            resumeTimerV247.Start();
                        }
                    };
                }
                resumeTimerV247.Interval=TimeSpan.FromMilliseconds(
                    ResumeDelayForAttemptV247(0));
                resumeTimerV247.Start();
            }));
        }
        catch(InvalidOperationException){}
    }

    internal static void Register(WidgetShellWindow widget)
'@ 'wake recovery event and finite retry scheduling'
Swap @'
        SystemEvents.DisplaySettingsChanged+=OnDisplaysChanged;
        widget.Closed+=(_,_)=>{
'@ @'
        SystemEvents.DisplaySettingsChanged+=OnDisplaysChanged;
        SystemEvents.PowerModeChanged+=OnPowerModeV247;
        widget.Closed+=(_,_)=>{
'@ 'power event subscription with primary lifetime'
Swap @'
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
            CloseAll();
'@ @'
            SystemEvents.DisplaySettingsChanged-=OnDisplaysChanged;
            SystemEvents.PowerModeChanged-=OnPowerModeV247;
            resumeTimerV247?.Stop();
            resumeTimerV247=null;
            resumeAttemptV247=0;
            CloseAll();
'@ 'release wake event and deferred recovery on close'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R247: on Windows resume, bounded three-pass monitor reconciliation and full event teardown.'
