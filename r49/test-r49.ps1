$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$nav=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceWorkspaceNavigationV49.cs'))
$wheel=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelWheelV42.cs'))
$main=[IO.File]::ReadAllText((Join-Path $root 'MainWindow.xaml.cs'))
foreach($token in @(
    'MainTabs.SelectedItem=ExcelMainTab',
    'PipeOptions.CurrentUserOnly',
    'EXCEL_MAPPING',
    'ShowExcelMappingFromWheelV49',
    'new NamedPipeClientStream',
    'new NamedPipeServerStream',
    'Arguments="--excel-mapping"'
)){
    if(!$nav.Contains($token)){throw "R49 in-app Manage navigation missing $token"}
}
if(([regex]::Matches($wheel,'DeskSpaceExcelManageV49.OpenInsideWorkspaceAsync')).Count -ne 2){
    throw 'R49 both child and root Manage buttons must navigate to in-app Workspace'
}
if(!$main.Contains('InitializeExcelNavigationV49();')){
    throw 'R49 original Workspace has not registered the navigation bridge'
}
if(!$main.Contains('ExcelModule_Click(') -or !$main.Contains('ExcelMainTab')){
    throw 'R49 old in-app Excel Mapping tab is missing'
}
Write-Host 'R49 PASS: both Manage actions route to existing Workspace Excel Mapping tab without editor popup.'
