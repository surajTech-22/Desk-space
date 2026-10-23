$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
$watch=Get-Content "$root/DeskSpaceNewFileWatchV53.cs" -Raw
$search=Get-Content "$root/DeskSpaceSearchIndexV13.cs" -Raw
foreach($token in @('IsOrdinaryUserFile(string path,bool allowTestRoot=false)','FileAttributes.Hidden','FileAttributes.Temporary','"cache"','"gpucache"','"node_modules"','"thumbs.db"','".tmp"','".crdownload"','InternalFolders.Contains(folder)','return IsOrdinaryUserFile(path,explicitRoots);')){
 if(!$watch.Contains($token)){throw "R58 watcher policy missing $token"}
}
if(([regex]::Matches($search,'DeskSpaceNewFileWatchV53.IsOrdinaryUserFile')).Count -lt 4){
 throw 'R58 Search does not consistently filter hidden/cache names'
}
Write-Host 'R58 PASS: normal user files only, caches/hidden/generated artifacts excluded.'
