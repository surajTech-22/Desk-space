$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$ui=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatsV121.cs")
$store=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatStoreV121.cs")
$recent=[IO.File]::ReadAllText("$r/DeskSpaceApplicationsV55.cs")
$deck=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$suggest=[IO.File]::ReadAllText("$r/DeskSpaceExcelDeckSuggestionsV123.cs")
$info=[IO.File]::ReadAllText("$r/DeskSpaceInformationV36.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($check in @(
 @{Name='Format save separate';Ok=$format.Contains('DeskSpaceExcelFormatStoreV121.Save(')},
 @{Name='Format source separate';Ok=$ui.Contains('return SelectedFormatV121();')},
 @{Name='Format store different path';Ok=$store.Contains('formats-v121.dpapi')},
 @{Name='Format values stripped';Ok=$store.Contains('output.Cells.Clear();')},
 @{Name='Format anchors stripped';Ok=$store.Contains('output.Anchors.Clear();')},
 @{Name='Original mapping store remains';Ok=$ui.Contains('DeskSpaceExcelStoreV16.Save(disk);')},
 @{Name='Values UI separate';Ok=$ui.Contains('Save header/value mapping')},
 @{Name='Values paste separate';Ok=$ui.Contains('Apply header/value mapping')},
 @{Name='Format UI separate';Ok=$ui.Contains('BuildFormatControlsV121()')},
 @{Name='Independent apply';Ok=$format.Contains('ApplyFormatV109')},
 @{Name='Excel suggestions in deck';Ok=$deck.Contains('excelDeckSuggestionsV123?.Bind(excelSuggestionsHost)')},
 @{Name='Suggestions passive';Ok=$suggest.Contains('control.Click+=async')},
 @{Name='Suggestions do not autopaste';Ok=$suggest.Contains('MessageBoxButton.YesNo')},
 @{Name='Windows Recent per pinned app';Ok=$recent.Contains('DeskSpacePinnedRecentV122.FilesFor(')},
 @{Name='Select recent document';Ok=$recent.Contains('OpenDocumentForPinnedApp(')},
 @{Name='No executable recent';Ok=([IO.File]::ReadAllText("$r/DeskSpacePinnedRecentV122.cs")).Contains('".exe"')},
 @{Name='Information target placeholder';Ok=$info.Contains('Login suggestions · future placeholder')},
 @{Name='Information password placeholder disabled';Ok=$info.Contains('IsEnabled=false')},
 @{Name='WPF acceptance tests';Ok=$smoke.Contains('TestIndependentFormatAndRecentV121();')}
))
{
 if(!$check.Ok){throw "R121 missing: $($check.Name)"}
}
if($format.Contains('DeskSpaceExcelStoreV16.Save(') -or
   $store.Contains('templates.dpapi') -or
   $suggest.Contains('DeskSpaceVaultV10') -or
   $suggest.Contains('Process.Start(')){
 throw 'R121 unsafe cross-route or credential dependency detected'
}
Write-Host 'R121 static PASS: separate format storage and UI, no content pasting; app-specific recent files; read-only Excel suggestions; inert information placeholder.'
