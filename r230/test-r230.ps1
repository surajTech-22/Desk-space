$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$root/DeskSpaceGracefulExitV230.cs")
$super=[IO.File]::ReadAllText("$root/DeskSpaceUpdateSupervisor.ps1")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$update=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($line in @(
 'internal static string EventName(string executable)',
 'SHA256.HashData(',
 'EventWaitHandle.OpenExisting(EventName(',
 'internal static bool TryCloseWindows(Window[] windows)',
 'window.Close();',
 'if(!closed)return false;',
 'TryCloseInOrder(windows,Application.Current?.MainWindow);',
 'Concat(windows.Where(w=>ReferenceEquals(w,main)))',
 'if(TryCloseWindows(Application.Current.Windows.Cast<Window>().ToArray()))',
 'Application.Current.Shutdown();'
)){if(!$app.Contains($line)){throw ('R230 WPF respectful full close missing '+$line)}}
if(!$main.Contains('DeskSpaceGracefulExitV230.Attach(this);') -or
   !$widget.Contains('DeskSpaceGracefulExitV230.Attach(this);')){
 throw 'R230 Workspace and multi-monitor widget/tray processes must both listen'
}
foreach($line in @(
 'function Get-R230InstalledPeers',
 'function New-R230ExitEvent',
 'function Request-R230NativeClose',
 '[void]$exitEvent.Set()',
 'Get-R230InstalledPeers $original',
 'Request-R230NativeClose $pids',
 'R230 all installed DeskSpace processes exited normally.',
 'if($remaining.Count -gt 0)',
 '$exitBlocked=$true',
 'No forced termination; installer deferred.',
 '/NOCLOSEAPPLICATIONS',
 '[void]$exitEvent.Reset()'
)){if(!$super.Contains($line)){throw ('R230 safe full process shutdown missing '+$line)}}
if($super -match '(?m)^\s*(?:Stop-Process|taskkill)(?:\.exe)?\b' -or
   $super -match '(?<!NO)/CLOSEAPPLICATIONS(?=\s)'){
 throw 'R230 must never forcibly end unsaved Workspace sessions'
}
if($update.Contains('if(DeferForOpenDeskSpace(owner,automatic))return;') -or
   $update.Contains('Application.Current.Shutdown();')){
 throw 'R230 must delegate normal exit for all processes to the supervisor'
}
if(!$native.Contains('TestAllWindowsGracefulCloseV230();')){
 throw 'R230 live WPF close cancellation tests missing'
}
Write-Host 'R230 STATIC PASS: close every matching process, preserve WPF cancel, do not force kill, install only after zero peers.'
