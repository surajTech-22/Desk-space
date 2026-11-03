$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$path=Join-Path $root 'MainWindow.xaml'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
[xml]$xml=Get-Content -LiteralPath $path -Raw
function Check([bool]$ok,[string]$name) {
    if(!$ok){throw ('R349 UI ACCEPTANCE FAILED: '+$name)}
}
$xaml=[IO.File]::ReadAllText($path)
$commands=[IO.File]::ReadAllText($palette)
Check ($xml.DocumentElement.LocalName -eq 'Window') 'WPF window root'
foreach($name in @('MainTabs','VersionButton','StatusText','DashboardTime',
 'DashboardSummary','NotesList','EventCalendar')) {
    Check ($xaml.Contains('x:Name="'+$name+'"')) ('existing binding '+$name)
}
foreach($handler in @('Closing="MainWindow_Closing"',
 'Click="AddNote_Click"','Click="ShowWidget_Click"',
 'Click="ExcelModule_Click"','Click="GitHubUpdates_Click"',
 'Click="UniversalSearch_Click"')) {
    Check ($xaml.Contains($handler)) ('existing functionality preserved '+$handler)
}
Check ($xaml.Contains('Your desktop. One clear space.')) 'new welcome hero'
Check ($xaml.Contains('DESKSPACE / CONTROL CENTER')) 'product heading'
Check ($xaml.Contains('Launch pad')) 'grouped launch pad'
Check ($xaml.Contains('CornerRadius="17" Padding="15"')) 'rounded panel'
Check ($xaml.Contains('Property="IsKeyboardFocused" Value="True"')) 'keyboard focus trigger'
Check ($commands.Contains('DESKSPACE / QUICK ACTIONS')) 'modern command center'
Check ($commands.Contains('WindowStartupLocation=WindowStartupLocation.CenterOwner')) 'owner monitor linkage'
Check ($commands.Contains('input.PreviewKeyDown')) 'keyboard navigation'
Check ($commands.Contains('Run(owner,trayVisible,command)')) 'existing action handlers'
Check (!$xaml.Contains('Click="DeleteAllFiles_Click"')) 'no destructive UI added'
Write-Host 'R349 PASS: WPF XML, Workspace bindings, navigation/hero, command picker and keyboard interactions preserved.'
