$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdater.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
$start=$s.IndexOf('    public static void StartAutomaticChecks(Window host)',[StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    public static async Task CheckAsync(', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R39 startup update lifecycle anchor missing'}
$replacement=@'
    // R39: no idle-only gate or several-minute delay at application start.
    // Check as soon as the widget loads; retry failures and watch for new
    // releases at most once per minute without blocking the UI.
    public static void StartAutomaticChecks(Window host)
    {
        if (timer != null) return;
        timer = new DispatcherTimer {
            Interval = TimeSpan.FromMinutes(1)
        };
        timer.Tick += async (_, _) => await CheckAsync(host,automatic:true);
        timer.Start();
        host.Dispatcher.BeginInvoke(new Action(async () =>
        {
            if(!host.IsLoaded)return;
            await CheckAsync(host,automatic:true);
        }),DispatcherPriority.Background);
    }

'@
$replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$start)+$replacement+$s.Substring($end)
$s=$s.Replace('TimeSpan.FromMinutes(5)','TimeSpan.FromMinutes(1)')
if(!$s.Contains('Interval = TimeSpan.FromMinutes(1)') -or
   !$s.Contains('DispatcherPriority.Background')){
    throw 'R39 fast updater cycle missing'
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R39: check immediately at load and then every minute; preserve GH authentication, release SHA-256 and installation supervisor.'
