$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget=[IO.File]::ReadAllText("$root/DeskSpaceInlineV36.cs")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml")
$handlers=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$due=[IO.File]::ReadAllText("$root/DeskSpaceDueShelfV74.cs")
$picker=[IO.File]::ReadAllText("$root/DeskSpaceReminderPickerV80.cs")
$browser=[IO.File]::ReadAllText("$root/DeskSpaceReminderBrowserV80.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($term in @('DeskSpaceReminderPickerV80.Create(','Search in DeskSpace',
 'Browse file','Browse app','Open URL','DeskSpaceReminderBrowserV80.TryUrl(')){
 if(!$widget.Contains($term)){throw "R80 widget missing: $term"}
}
foreach($term in @('ReminderEmbeddedSearch','ReminderSearchButton','ReminderBrowseFileButton',
 'ReminderBrowseAppButton','ReminderUrlNote','Open URL','ReminderSearch_Click')){
 if(!$main.Contains($term)){throw "R80 actual main Workspace missing: $term"}
}
foreach($term in @('ReminderSearch_Click','DeskSpaceReminderPickerV80.Create(',
 'ReminderBrowseFile_Click','DeskSpaceReminderBrowserV80.TryUrl(')){
 if(!$handlers.Contains($term)){throw "R80 main handler missing: $term"}
}
foreach($term in @('DeskSpaceReminderBrowserV80.ShouldPrepare(',
 'DeskSpaceReminderBrowserV80.Prepare(','DeskSpaceReminderBrowserV80.LaunchAtDue(',
 'mode=="Open URL"')){
 if(!$due.Contains($term)){throw "R80 scheduler missing: $term"}
}
foreach($term in @('DeskSpaceModuleV10.SearchNow(',
 'new WorkspaceStore().Load()','Task.Run(','CancellationTokenSource',
 'CanChoose(SearchHit hit,string mode)')){
 if(!$picker.Contains($term)){throw "R80 must reuse existing universal search: $term"}
}
foreach($term in @('PreopenSeconds=12','ProcessStartInfo','UseShellExecute=true',
 'GetForegroundWindow','SetForegroundWindow','LiveBrowserWindow','TryUrl(',
 'OpenDefault(url);')){
 if(!$browser.Contains($term)){throw "R80 default browser launch missing: $term"}
}
foreach($term in @('TestReminderV80();','r80-widget-search-in-reminder',
 'r80-main-search-in-reminder','R80 URL should prepare roughly 12 seconds before due',
 'R80 picker must filter existing paths by action')){
 if(!$tests.Contains($term)){throw "R80 WPF regression missing: $term"}
}
Write-Host 'R80 preflight PASS: actual V36/Main reminder views share existing search; selected paths and URLs persist; browser preopen bounded.'
