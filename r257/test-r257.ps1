$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$layout=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs')
$plan=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowRestorePlanV257.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static DeskSpaceWindowRestorePlanV257.Plan Preview(Profile profile)',
 'DeskSpaceWindowRestorePlanV257.Build(profile.Windows,live,screens)',
 'Content="Preview selected"',
 'previewProofV257=ProofV257(plan)',
 'Preview this profile before restoring',
 'Windows or monitors changed. Preview again before restore.',
 'previewedProfileV257=null;',
 'Saved monitor disconnected',
 'new HashSet<int>()',
 'TestWindowRestorePreviewV257();'
)){
 if(!$layout.Contains($needle) -and !$plan.Contains($needle) -and !$smoke.Contains($needle)){
  throw 'R257 preview safety is missing: '+$needle
 }
}
if(!$layout.Contains('if(!screens.Any(screen=>string.Equals(screen.DeviceName,') -or
   !$layout.Contains('saved.Monitor,StringComparison.OrdinalIgnoreCase)))')){
 throw 'R257 must skip disconnected original monitor before any fallback'
}
foreach($forbidden in @('Process.Start(', 'SetWindowPos(', 'File.WriteAll',
 'Clipboard.', 'HttpClient', 'Task.Run(', 'DispatcherTimer')){
 if($plan.Contains($forbidden)){
  throw ('R257 pure plan must never make system changes: '+$forbidden)
 }
}
Write-Host 'R257 STATIC PASS: display-exact privacy-safe layout projection and single-use preview before restore.'
