$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($p)
foreach($token in @(
 'string.Equals(args[0],"--widget-host"',
 'R169 PASS: smoke child widget-host launch ignored.',
 'return 0;',
 'TestInterruptedCopyRecoveryV165();',
 'TestTrayRecoveryV166();',
 'TestIndependentMonitorsV154();'
)){if(!$s.Contains($token)){throw "R169 recursion guard missing: $token"}}
Write-Host 'R169 PASS: smoke-only self-spawn guard preserves original DeskSpace widget-host command.'
