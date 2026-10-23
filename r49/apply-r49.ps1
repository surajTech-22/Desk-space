$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r49/DeskSpaceWorkspaceNavigationV49.cs' (Join-Path $root 'DeskSpaceWorkspaceNavigationV49.cs') -Force
$main=Join-Path $root 'MainWindow.xaml.cs'
$s=[IO.File]::ReadAllText($main)
$anchor='InitializeComponent();'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
    throw 'R49 cannot locate unique existing Workspace window initialization'
}
$s=$s.Replace($anchor,$anchor+[Environment]::NewLine+
    '        InitializeExcelNavigationV49();')
[IO.File]::WriteAllText($main,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R49 Manage navigates to existing Excel Mapping tab, with same-user named-pipe bridge for floating widget.'
