$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$oldProject=[IO.File]::ReadAllText("$root/DeskSpace.csproj")
$oldApp=[IO.File]::ReadAllText("$root/App.xaml.cs")
$oldStudio=[IO.File]::ReadAllText("$root/DeskSpaceSettingsStudioV148.cs")
foreach($token in @(
 '<UseWPF>true</UseWPF>',
 '<UseWindowsForms>true</UseWindowsForms>',
 '<ApplicationIcon>DeskSpace.ico</ApplicationIcon>'
)){if(!$oldProject.Contains($token)){throw ('R285 project baseline changed: '+$token)}}
if(!$oldApp.Contains('if(e.Args.Contains("--widget-host"') -or
   !$oldApp.Contains('DeskSpaceStartupRecoveryV183.BeginEarly()')){
 throw 'R285 must preserve primary widget startup and recovery'
}
if(!$oldStudio.Contains('private void DisplaysPage()') -or
   !$oldStudio.Contains('DeskSpaceMultiMonitorV151.Configure(showEveryMonitorDraft);')){
 throw 'R285 must preserve existing Displays settings and monitor configuration'
}
Copy-Item 'r285/DeskSpaceV285.csproj' "$root/DeskSpace.csproj" -Force
Copy-Item 'r285/app.manifest' "$root/app.manifest" -Force
Copy-Item 'r285/DeskSpaceDpiAuditV285.cs' "$root/DeskSpaceDpiAuditV285.cs" -Force
Copy-Item 'r285/AppV285.xaml.cs' "$root/App.xaml.cs" -Force
Copy-Item 'r285/DeskSpaceSettingsStudioV285.cs' "$root/DeskSpaceSettingsStudioV148.cs" -Force
Write-Host 'R285: real DeskSpace process PMv2 manifest and explicit on-demand Downloads DPI diagnostic installed.'
