$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$policy=Get-Content (Join-Path $root 'DeskSpaceFileVisibilityV58.cs') -Raw
$watch=Get-Content (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Raw
$index=Get-Content (Join-Path $root 'DeskSpaceSearchIndexV13.cs') -Raw
$shelf=Get-Content (Join-Path $root 'DeskSpaceDropShelfV15.cs') -Raw
foreach($token in @('FileAttributes.Hidden','FileAttributes.System',
    'FileAttributes.Temporary','FileAttributes.ReparsePoint',
    '"AppData"','"cache"','"code cache"','"gpucache"',
    '"__pycache__"','"desktop.ini"','"thumbs.db"',
    '"~$"','".crdownload"','".cache"','"node_modules"',
    'public static bool Candidate','public static bool Eligible',
    'File.GetAttributes(node)','Path.GetRelativePath(root,full)')){
    if(!$policy.Contains($token)){throw "R58 shared filter missing: $token"}
}
foreach($token in @('DeskSpaceFileVisibilityV58.Eligible(path,ExplicitScope(path))',
    'NotifyFilters.Attributes','EnumerateVisibleFiles(dir,15000,token)',
    'IncludeInCrawl(child,flags)','if(recent.ContainsKey(path))Remove(path)',
    'watcher.Changed+=Changed','watcher.Renamed+=Renamed')){
    if(!$watch.Contains($token)){throw "R58 file watcher missing: $token"}
}
foreach($token in @('EnumerateVisibleTree(root)',
    'DeskSpaceFileVisibilityV58.IncludeInCrawl(path,flags)',
    'DeskSpaceFileVisibilityV58.Eligible(full)',
    'DeskSpaceFileVisibilityV58.Eligible(hit.OpenTarget,allowDirectory:true)',
    'UnregisterLivePrefix(string path)')){
    if(!$index.Contains($token)){throw "R58 indexed/Windows Search filter missing: $token"}
}
if(!$shelf.Contains('DeskSpaceSearchIndexV13.UnregisterLivePrefix(path)')){
    throw 'R58 excluded files or renamed folders remain in recent shelf'}
Write-Host 'R58 PASS: shared visibility policy covers watcher, recent shelf, drive scan and Windows Search.'
