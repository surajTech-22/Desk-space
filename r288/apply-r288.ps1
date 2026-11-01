$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceSearchIndexV13.cs")
$rank=[IO.File]::ReadAllText("$root/DeskSpaceSearchRankingV192.cs")
foreach($text in @(
 'private static IEnumerable<string> EnumerateTree(string root)',
 'private static void ScanVolume(string root)',
 'var perVolume=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);',
 'DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(item.Path)',
 'public static List<SearchHit> Search(string term,Workspace workspace,CancellationToken cancellation)'
)){
 if(!$source.Contains($text)){throw ('R288 search baseline drift '+$text)}
}
foreach($text in @(
 'private int Compare(SearchHit first,SearchHit second)',
 'int quality=Quality(first,query).CompareTo(Quality(second,query));'
)){
 if(!$rank.Contains($text)){throw ('R288 ranking baseline drift '+$text)}
}
Copy-Item 'r288/DeskSpaceSearchIndexV288.cs' "$root/DeskSpaceSearchIndexV13.cs" -Force
Copy-Item 'r288/DeskSpaceSearchRankingV288.cs' "$root/DeskSpaceSearchRankingV192.cs" -Force
Write-Host 'R288: actual user file names appear before directories, while full-drive index warms.'
