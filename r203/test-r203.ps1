$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$feature=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$review=[IO.File]::ReadAllText("$root/DeskSpaceWorkflowDryRunV203.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'DeskSpaceWorkflowDryRunV203.Show(this)',
 'Preview saved workflows',
 'Review steps and conditions without launching'
)){
 if(!$feature.Contains($needle)){throw ("R203 no Features entry: "+$needle)}
}
foreach($needle in @(
 'new("workflowreview"',
 'DeskSpaceWorkflowDryRunV203.Show(owner)'
)){
 if(!$palette.Contains($needle)){throw ("R203 no Action Palette entry: "+$needle)}
}
foreach($needle in @(
 'DeskSpaceAutomationV84.ValidCondition',
 'DeskSpaceDueShelfV74.IsAllowedAction',
 'template.Additional.Count>8',
 'DisplayTarget(step.Action,step.Target)',
 'Uri.TryCreate(target,UriKind.Absolute',
 'This preview NEVER executes',
 'No workflow will run from this window'
)){
 if(!$review.Contains($needle)){throw ("R203 non-execution gate missing "+$needle)}
}
if(!$smoke.Contains('TestWorkflowDryRunV203();')){
 throw 'R203 native isolated WPF smoke missing'
}
if($review.Contains('Process.Start(') -or $review.Contains('File.Write') -or
   $review.Contains('DeskSpaceAutomationV84.Run(') -or
   $review.Contains('SendKeys(')){
 throw 'R203 preview must never execute or mutate workflows'
}
Write-Host 'R203 STATIC PASS: read-only workflow review, validated steps, URL host redaction, no execution.'
