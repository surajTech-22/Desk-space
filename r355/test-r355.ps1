$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText((Join-Path $root 'App.xaml.cs'))
$grace=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceGracefulExitV230.cs'))
function Assert([bool]$ok,[string]$problem) {
    if(!$ok){throw ('R355 TEST FAILED: '+$problem)}
}
# C# compile verifies the actual WPF syntax separately. This test guards
# critical order, so a future refactor cannot reintroduce stuck widget PIDs.
Assert ($app.Contains('Environment.Exit(0);')) 'no process-completion fallback'
$duplicate=$app.IndexOf('if(!first)')
$shown=$app.IndexOf('var window=new WidgetShellWindow();')
Assert ($duplicate -ge 0 -and $shown -gt $duplicate) 'duplicate host guard missing'
$duplicateBody=$app.Substring($duplicate,$shown-$duplicate)
Assert ($duplicateBody.Contains('Shutdown(0);') -and
    $duplicateBody.Contains('Environment.Exit(0);')) 'duplicate hosts can remain as hidden processes'
Assert (!$duplicateBody.Contains('new WidgetShellWindow(')) 'duplicate process created a user widget'
$onExit=$app.IndexOf('protected override void OnExit(')
$base=$app.IndexOf('base.OnExit(e);',$onExit)
$authorised=$app.IndexOf('if(DeskSpaceGracefulExitV230.UpdateExitAuthorized)',$onExit)
$finished=$app.IndexOf('Environment.Exit(0);',$authorised)
Assert ($onExit -ge 0 -and $base -gt $onExit -and
    $authorised -gt $base -and $finished -gt $authorised) 'forced process completion before WPF OnExit'
Assert ($app.Contains('DeskSpaceStartupRecoveryV183.MarkCleanExit(') -or
    $app.Contains('DeskSpaceStartupRecoveryV183.BeginEarly(')) 'startup safety logic not retained'
Assert ($app.Contains('widgetMutex?.Dispose()')) 'widget singleton mutex cleanup not retained'
Assert ($grace.Contains('if(TryCloseWindows(Application.Current.Windows.Cast<Window>().ToArray()))')) 'normal WPF close veto logic removed'
Assert ($grace.Contains('updateExitAuthorized=true;') -and
    $grace.Contains('updateExitAuthorized=false;')) 'authorisation not revoked on veto'
Assert ($grace.Contains('var ordered=windows.Where(w=>!ReferenceEquals(w,main))')) 'process main window is not closed last'
Assert ($grace.Contains('if(!closed)return false;')) 'a Closing veto must block installer shutdown'
Assert ($grace.Contains('one WPF window refused graceful Close')) 'window-veto diagnostics missing'
Assert ($grace.Contains('App.OnExit finished; completing authorized update shutdown') -eq $false) 'OnExit log incorrectly inserted in window listener'
Assert (!$app.Contains('Process.GetCurrentProcess().Kill(') -and !$grace.Contains('Process.GetCurrentProcess().Kill(')) 'unsafe process kill'
Write-Host 'R355 PASS: no windows/data in duplicate process, voluntary Closing veto honored, main WPF window closed last, full App.OnExit then scope-limited process completion.'
