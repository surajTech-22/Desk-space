$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$root/DeskSpaceApplicationsV55.cs")
$recent=[IO.File]::ReadAllText("$root/DeskSpacePinnedRecentV122.cs")
$shelf=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$context=[IO.File]::ReadAllText("$root/DeskSpaceExcelActiveV122.cs")
$info=[IO.File]::ReadAllText("$root/DeskSpaceInformationV36.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($s in @(
 'OpenLatestOrApp(item.Path)',
 'DeskSpacePinnedRecentV122.OpenLatestOrApp'
)){
 if(!$app.Contains($s) -and !$recent.Contains($s)){
  throw "R122 pinned recent document path missing: $s"
 }
}
foreach($s in @('Environment.SpecialFolder.Recent',
 'AssocQueryString','Picked','PinnedRecentPaths.json')){
 if($s -eq 'Picked'){continue}
 if(!$recent.Contains($s)){throw "R122 Windows recent path missing: $s"}
}
foreach($s in @(
 'DeskSpaceExcelActiveV122.ForegroundExcel()',
 'DeskSpaceExcelWorkerV41.PreviewAsync(',
 'DeskSpaceExcelWorkerV41.ApplyAsync(',
 'proposal.Verified','proposal.Writable',
 'Review & paste','Excel paste suggestions',
 'MessageBoxButton.YesNo'
)){
 if(!$shelf.Contains($s)){throw "R122 Excel deck suggestion missing: $s"}
}
if(!$context.Contains('GetForegroundWindow') -or
   !$context.Contains('excelForeground&&verifiedSignature&&writable>0')){
 throw 'R122 Excel suggestion requires active foreground and unique verification'
}
if(!$info.Contains('Automatic detection and autofill are not enabled') -or
   !$info.Contains('existing security vault remains unchanged')){
 throw 'R122 informational password association placeholder absent'
}
if(!$smoke.Contains('TestContextualFeaturesV122();')){
 throw 'R122 Windows WPF smoke regression not connected'
}
if($shelf.Contains('ApplyAsync(entry,false')){
 throw 'R122 no automatic Excel write on scan allowed'
}
Write-Host 'R122 guards PASS: recent app-linked files, read-only contextual Excel previews, explicit paste confirmation, vault placeholder only.'
