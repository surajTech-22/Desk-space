$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widgetSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$clip=[IO.File]::ReadAllText("$root/DeskSpaceClipboardTransformsV177.cs")
$transforms=[IO.File]::ReadAllText("$root/DeskSpaceTextConversionsV198.cs")
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'Action palette (Ctrl+K in widget)',
 'DeskSpaceActionPaletteV197.Show(this,tray.Visible)',
 'this.PreviewKeyDown+=(_,e)=>{',
 'System.Windows.Input.ModifierKeys.Control'
)){
 if(!$widgetSource.Contains($token)){throw ("R197 host integration missing "+$token)}
}
foreach($token in @(
 'internal static IReadOnlyList<Command> Filter(',
 'window.PreviewKeyDown+=(_,e)=>{',
 'e.Key==Key.Escape',
 'if(e.Key==Key.Enter)',
 'AutomationProperties.SetName(input',
 'MessageBoxButton.YesNo',
 'DeskSpaceDiagnosticsV161.ExportToDownloads()',
 'DeskSpaceComponentHealthV175.Show(owner,trayVisible)',
 'DeskSpaceWindowLayoutsV179.Show(owner)',
 'DeskSpaceClipboardTransformsV177.Show(owner)'
)){
 if(!$palette.Contains($token)){throw ("R197 command palette missing "+$token)}
}
foreach($token in @('Format JSON','CSV to Markdown table','Unique lines')){
 if(!$clip.Contains($token) -or !$transforms.Contains($token)){
    throw ("R198 missing explicit clipboard transformation "+$token)
 }
}
foreach($token in @('MaxInput=262144','MaxDepth=32','Invalid RFC-style CSV quoting',
 'CSV supports at most 200 rows','CSV supports at most 24 columns')){
 if(!$transforms.Contains($token)){throw ("R198 conversion guard missing "+$token)}
}
if($palette.Contains('Process.Start("powershell') -or
   $palette.Contains('cmd.exe') -or
   $transforms.Contains('Clipboard.GetText') -or
   $transforms.Contains('File.WriteAllText')){
 throw 'R197/R198 may not run arbitrary shell commands or read/save clipboard in background'
}
foreach($token in @('TestUnifiedActionPaletteV197();','TestClipboardConversionsV198();')){
 if(!$test.Contains($token)){throw ("R197/198 native UI smoke missing "+$token)}
}
Write-Host 'R197-R198 PASS: explicit local Action Palette, user-approved diagnostics and bounded non-background text transforms.'
