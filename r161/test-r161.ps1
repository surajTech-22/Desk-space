$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$preview=[IO.File]::ReadAllText("$root/DeskSpaceFilePreviewV85.cs")
$diag=[IO.File]::ReadAllText("$root/DeskSpaceDiagnosticsV161.cs")
$zip=[IO.File]::ReadAllText("$root/DeskSpaceArchivePreviewV161.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'DeskSpaceDiagnosticsV161.Install();',
 'Export privacy-safe diagnostics...',
 'DeskSpaceDiagnosticsV161.ExportToDownloads()'
)){
 if(!$hostSource.Contains($token)){throw "R161 diagnostics not available from tray: $token"}
}
foreach($token in @(
 'AppDomain.CurrentDomain.UnhandledException',
 'crash-types.log',
 'ErrorKinds(',
 'DeskSpace_Diagnostics_',
 'system-summary.txt',
 'crash-categories.txt',
 'update-error-categories.txt',
 'privacy.txt'
)){
 if(!$diag.Contains($token)){throw "R161 privacy-safe support ZIP missing $token"}
}
if($diag.Contains('archive.CreateEntryFromFile(') -or
   $diag.Contains('File.Copy(')){
 throw 'R161 diagnostic ZIP must never include raw files or clipboard data'
}
foreach($token in @(
 'ZipFile.OpenRead(path)',
 '256L*1024*1024',
 'Math.Clamp(maxEntries,1,30)',
 'Archive is too large',
 'ZIP contents (read-only)'
)){
 if(!$zip.Contains($token)){throw "R161 read-only ZIP preview missing $token"}
}
if(!$preview.Contains('DeskSpaceArchivePreviewV161.Create(path)')){
 throw 'R161 archive renderer not linked to the existing Deck preview'
}
foreach($token in @(
 'private static readonly Dictionary<string,string> originalByStaged',
 'internal static bool AlreadyHeldSource(string source)',
 'if(AlreadyHeldSource(source))continue;',
 'originalByStaged[destination]=Path.GetFullPath(source);',
 'originalByStaged.Remove(item.StagedPath);'
)){
 if(!$deck.Contains($token)){throw "R161 shared Deck duplicate-safety missing $token"}
}
foreach($token in @(
 'TestDeskSpaceFoundationV161();',
 'private static void TestDeskSpaceFoundationV161()',
 'R161 first stage must be permitted',
 'R161 diagnostic export must not include personal files',
 'R161 read-only preview must never extract',
 'R161 clearing a held source must permit staging again'
)){
 if(!$ui.Contains($token)){throw "R161 WPF functional regression missing $token"}
}
Write-Host 'R161 source gates passed: diagnostics, read-only ZIP preview and two-widget duplicate-path protection.'
