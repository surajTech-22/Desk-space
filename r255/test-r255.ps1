$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFeatureActionGuardV255.cs')
$hub=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFeatureHubV199.cs')
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static bool Execute(string caption,Action action,',
 'catch(Exception ex)when(!IsFatal(ex))',
 'OutOfMemoryException or AccessViolationException',
 'ex.GetType().Name',
 'DeskSpaceFeatureActionGuardV255.Execute(caption,action,',
 'SelectExistingFeatureTabV199("Settings")',
 'TestFeatureActionGuardV255();'
)){
 if(!$s.Contains($needle) -and !$hub.Contains($needle) -and !$test.Contains($needle)){
  throw ('R255 Workspace crash containment missing '+$needle)
 }
}
foreach($forbidden in @('File.WriteAll','Process.Start','Task.Run','Clipboard.',
 'DispatcherTimer','HttpClient','SystemEvents')){
 if($s.Contains($forbidden)){throw ('R255 event containment must be local '+$forbidden)}
}
Write-Host 'R255 STATIC PASS: settings/navigation recoverable-error boundary, fatal failures propagate and private errors redacted.'
