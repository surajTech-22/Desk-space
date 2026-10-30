$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$nav=[IO.File]::ReadAllText("$root/DeskSpaceFeatureNavigationV200.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceWorkspaceNavigationV49.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'Pipe="DeskSpace.Workspace.ExcelMapping.V49"',
 'PipeOptions.CurrentUserOnly',
 'ConnectAsync(timeout.Token)',
 'IsAllowed(destination)',
 'Allowed.Contains(destination,StringComparer.Ordinal)',
 'Arguments=Argument(destination)',
 'ShowFeatureDestinationV200(destination)',
 'OpenExcelAsync(Window owner)'
)){
 if(!$nav.Contains($needle)){throw ("R200 navigation missing "+$needle)}
}
foreach($needle in @(
 'DeskSpaceFeatureNavigationV200.IsAllowed(command)',
 'ShowFeatureDestinationV200(command!)',
 '--features',
 '--notes',
 '--reminders'
)){
 if(!$bridge.Contains($needle)){throw ("R200 Workspace bridge missing "+$needle)}
}
foreach($needle in @(
 'new("features"',
 'new("excel"',
 'new("reminders"',
 'new("notes"',
 'DeskSpaceFeatureNavigationV200.OpenAsync(owner,"FEATURES")',
 'DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner)'
)){
 if(!$palette.Contains($needle)){throw ("R200 Action Palette missing "+$needle)}
}
if(!$tests.Contains('TestCrossProcessFeatureNavigationV200();')){
 throw 'R200 native IPC validation not installed'
}
if($nav.Contains('Process.Start("cmd') -or $nav.Contains('Process.Start("powershell') -or
   $nav.Contains('Clipboard.GetText(') -or $nav.Contains('File.Delete(')){
 throw 'R200 navigation must not run shell or read/write user data'
}
Write-Host 'R200 PASS: fixed verbs, current-user IPC, reused Workspace, explicit launch fallback and no content mutation.'
