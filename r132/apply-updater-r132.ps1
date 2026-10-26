$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
function Swap([string]$from,[string]$to,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($script:file).Replace($crlf,$lf)
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($from))).Count
 if($hits -ne 1){throw "R132 updater $label expected 1 anchor, got $hits"}
 [IO.File]::WriteAllText($script:file,$s.Replace($from,$to),[Text.UTF8Encoding]::new($false))
}
Swap @'
    private static readonly SemaphoreSlim Gate=new(1,1);
'@ @'
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static string lastNotifiedTagV132="";
    private static void NotifyNewReleaseV132(string channel,string tag)
    {
        if(lastNotifiedTagV132==tag)return;
        lastNotifiedTagV132=tag;
        // Notification is best-effort. Failure cannot suppress verified install.
        try
        {
            var balloon=new System.Windows.Forms.NotifyIcon{
                Icon=System.Drawing.SystemIcons.Information,
                Visible=true,
                Text="DeskSpace new release"
            };
            balloon.ShowBalloonTip(7000,"DeskSpace update available",
                tag+" on "+channel+" channel. Secure download starting.",
                System.Windows.Forms.ToolTipIcon.Info);
            // Keep NotifyIcon alive on the WPF dispatcher while the update
            // downloads; dispose on the SAME UI thread when the balloon ends.
            var expiry=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};
            expiry.Tick+=(_,_)=>{expiry.Stop();balloon.Dispose();};
            expiry.Start();
            Log("New release notification shown: "+tag+" channel "+channel);
        }
        catch(Exception error)
        {
            Log("Update notification unavailable: "+error.Message);
        }
    }
'@ 'new release toast survives until download'
Swap @'
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(30)};
'@ @'
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
'@ 'public stable can be checked faster without API polling'
Swap @'
                var next=DeskSpaceUpdateRateV127.ReadUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(30);
'@ @'
                var next=DeskSpaceUpdateRateV127.ReadUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(5);
'@ 'persist stable 5 minute checks across restart'
Swap @'
                var stableFolder=Path.Combine(Local,"Updates",stableTag);
'@ @'
                NotifyNewReleaseV132("stable",stableTag);
                var stableFolder=Path.Combine(Local,"Updates",stableTag);
'@ 'notify stable on discovered newer version'
Swap @'
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ @'
            NotifyNewReleaseV132(channel,tag);
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ 'notify tester on discovered newer version'
Write-Host 'R132 updates: user-visible Windows notification for both channels; 5 minute public stable check with persisted interval; tester REST API 30 minute limit unchanged. SHA256, official URLs and installation supervisor unchanged.'