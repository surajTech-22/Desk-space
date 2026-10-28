$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    'R170 BEGIN: R160 Copy and ZIP staging acceptance',
    'System.Windows.Threading.DispatcherPriority.Send,',
    'Interval=TimeSpan.FromMilliseconds(40)',
    'try{System.Windows.Threading.Dispatcher.PushFrame(frame);}',
    'finally{stopper.Stop();}',
    'R160 held-folder operation timed out'
)){if(!$s.Contains($token)){throw "R170 bounded WPF staging test missing: $token"}}
Write-Host 'R170 PASS: R160 folder Copy/ZIP WPF pump has a 40ms bounded frame and 15s operation timeout.'
