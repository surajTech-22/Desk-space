$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$src=[IO.File]::ReadAllText("$root/DeskSpaceSearchIndexV13.cs")
$rank=[IO.File]::ReadAllText("$root/DeskSpaceSearchRankingV192.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal static bool IsSearchableFileV288(string path)',
 'internal static IReadOnlyList<SearchHit> QuickFilesV288(',
 'internal static IEnumerable<string> QuickRootsV288()',
 'Environment.SpecialFolder.MyDocuments',
 '"Downloads"',
 '"OneDrive"',
 'if(item.Folder != (pass==1))continue;',
 'for(int pass=0;pass<2;pass++)',
 'if(already>=(item.Folder?12:48))continue;',
 'if(!item.Folder && !IsSearchableFileV288(item.Path))continue;',
 'if(fileA!=fileB)return fileA?-1:1;',
 'TestFileFirstSearchV288();',
 'R288 real filesystem quick search must return both'
)){
 if(!$src.Contains($token) -and !$rank.Contains($token) -and
    !$native.Contains($token)){throw ('R288 lost file search: '+$token)}
}
if($src.Contains('!item.Folder && !DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(item.Path)')){
 throw 'R288 indexed files still incorrectly use recent-file selection'
}
if(!$src.Contains('DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);')){
 throw 'R288 must preserve Windows Search and original shared index'
}
Write-Host 'R288 STATIC PASS: file-first search, bounded discovery, no recent-file filter on ordinary matches.'
