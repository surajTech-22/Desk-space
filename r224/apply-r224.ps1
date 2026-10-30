$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$receipt="$root/DeskSpacePostInstallReceiptV185.cs"
$host="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$label)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R224 "+$label+" expected 1 anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
Swap $receipt @'
    internal static void Ready(WidgetShellWindow owner)
'@ @'
    // R224: the updater must not treat a one-frame startup as healthy.
    // Only an actual pending supervised transaction schedules this timer.
    // Normal widget launches add no polling or background work.
    internal static readonly TimeSpan StableIntervalV224=TimeSpan.FromSeconds(12);

    internal static void ReadyAfterStabilityV224(WidgetShellWindow owner)
    {
        if(!owner.IsLoaded || DeskSpaceMultiMonitorV151.IsSatellite(owner) ||
           !File.Exists(PendingPath))return;
        var grace=new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,owner.Dispatcher){
            Interval=StableIntervalV224};
        EventHandler? closed=null;
        closed=(_,_)=>{
            grace.Stop();
            owner.Closed-=closed;
        };
        grace.Tick+=(_,_)=>{
            grace.Stop();
            owner.Closed-=closed;
            if(owner.IsLoaded && !DeskSpaceMultiMonitorV151.IsSatellite(owner))
                Ready(owner);
        };
        owner.Closed+=closed;
        grace.Start();
    }

    internal static void Ready(WidgetShellWindow owner)
'@ 'deferred receipt'
Swap $host @'
            DeskSpacePostInstallReceiptV185.Ready(this);
'@ @'
            // A sustained healthy WPF host must acknowledge the installer,
            // not a single Loaded frame that may immediately crash.
            DeskSpacePostInstallReceiptV185.ReadyAfterStabilityV224(this);
'@ 'receipt registration'
Write-Host 'R224: supervised post-install Ready receipt delayed until sustained WPF health; user startup unchanged.'
