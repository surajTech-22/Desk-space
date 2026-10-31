$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceGracefulExitV230.cs'
$s=[IO.File]::ReadAllText($p)
foreach($required in @(
 'internal static bool ConsumeSignal(bool active)',
 'handledCurrentSignal=false;',
 'if(handledCurrentSignal)return false;',
 'handledCurrentSignal=true;',
 'if(!ConsumeSignal(active))return;',
 'active=signal.WaitOne(0);',
 'Application.Current.Shutdown();',
 'TryCloseWindows(Application.Current.Windows.Cast<Window>().ToArray())'
)){
 if(!$s.Contains($required)){throw ('R233 missing behavior: '+$required)}
}
if($s.Contains('lastAttempt') -or $s.Contains('TimeSpan.FromSeconds(8)')){
 throw 'R233 still periodically retries close on the same held supervisor signal'
}
Write-Host 'R233 STATIC PASS: new update signal closes once; user cancellation is not retried repeatedly.'
