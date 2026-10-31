$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceDeckOffsetV228.cs")
$d=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
$g=[IO.File]::ReadAllText("$root/DeskSpaceDeckSaveGuardV265.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($term in @(
 'crossProcess.WaitOne(TimeSpan.FromMilliseconds(250));',
 'finally{crossProcess.ReleaseMutex();}',
 'map[device]=Clamp(value);',
 'File.Move(tmp,FilePath,true);'
)){
 if(!$s.Contains($term)){throw 'R265 lost atomic per-monitor cross-process guard: '+$term}
}
if(([regex]::Matches($d,[regex]::Escape(
 'DeskSpaceDeckSaveGuardV265.TrySave(() =>'))).Count -ne 3){
 throw 'R265 all 3 widget Deck save paths must be guarded'
}
foreach($term in @(
 'catch(Exception error)when(error is IOException',
 'UnauthorizedAccessException or SecurityException',
 'WaitHandleCannotBeOpenedException',
 'error.GetType().Name',
 'TestDeckSaveRecoveryV265();'
)){
 if(!$g.Contains($term) -and !$t.Contains($term)){
  throw 'R265 recoverable WPF Deck persistence missing: '+$term
 }
}
if($g.Contains('error.Message') -or $g.Contains('error.ToString()')){
 throw 'R265 must not log raw private files or saved positions'
}
Write-Host 'R265 STATIC PASS: short mutex wait, all three guarded Deck saves, no private paths leaked.'
