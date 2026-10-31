$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$palette=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceActionPaletteV197.cs')
$model=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpacePaletteQueryV259.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'DeskSpacePaletteQueryV259.Filter(Commands,query)',
 'internal const int MaxTokens=10;',
 'internal const int MaxTokenChars=64;',
 'query?.Length>768',
 'group==null||string.Equals(c.Group,group',
 'Key.PageDown || e.Key==Key.PageUp',
 'Key.Home || e.Key==Key.End',
 'No matching actions. Try @Windows',
 'TestPaletteFacetsV259();'
)){
 if(!$palette.Contains($needle) -and !$model.Contains($needle) -and !$smoke.Contains($needle))
 {throw ('R259 missing query/navigation requirement: '+$needle)}
}
foreach($forbidden in @('File.Read','File.Write','HttpClient','Clipboard.',
 'Process.Start','FileSystemWatcher','DispatcherTimer','Task.Run')){
 if($model.Contains($forbidden)){throw ('R259 scoped filter must be pure local data: '+$forbidden)}
}
Write-Host 'R259 STATIC PASS: bounded pure command search, group facets, paging and no background data access.'
