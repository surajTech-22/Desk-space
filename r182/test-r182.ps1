$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$app=Get-Content -LiteralPath 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs' -Raw
foreach($gate in @(
    'internal static RestorePreview Preview(Profile profile)',
    'MissingWindows','MissingMonitors','No windows have moved.',
    'Restore requested for ','before.Eligible==0',
    'buttons.Children.Add(capture);buttons.Children.Add(preview);'
)){
    if(!$app.Contains($gate)){throw "R182 missing safe-preview gate: $gate"}
}
$methodStart=$app.IndexOf('internal static RestorePreview Preview(Profile profile)')
$methodEnd=$app.IndexOf('internal static Window Create(Window owner)')
if($methodStart -lt 0 -or $methodEnd -le $methodStart){
    throw 'R182 preview code cannot be isolated for native safety check'
}
$readOnly=$app.Substring($methodStart,$methodEnd-$methodStart)
foreach($forbidden in @('SetWindowPos(','.Activate(','.Show(','.Start(')){
    if($readOnly.Contains($forbidden)){throw "R182 preview may change external state: $forbidden"}
}
if(!(Select-String 'r36/UiSmoke/Program.cs' 'TestRestorePreviewV182();' -SimpleMatch -Quiet)){
    throw 'R182 WPF test missing'
}
Write-Host 'R182 PASS: preview contains no native move, activation, launch or Show call.'
