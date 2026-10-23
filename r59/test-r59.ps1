$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$watch=Get-Content "$root/DeskSpaceNewFileWatchV53.cs" -Raw
$apps=Get-Content "$root/DeskSpaceApplicationsV55.cs" -Raw
$shelf=Get-Content "$root/DeskSpaceDropShelfV15.cs" -Raw
$widget=Get-Content "$root/WidgetHost.cs" -Raw
foreach($t in @('DefaultRoots()','IsAllowedWatchFolder(string folder)',
    'RecentFileFolders.txt','Path.Combine(drive.RootDirectory.FullName,name)',
    'seenVersions.TryGetValue(path,out var old)',
    'seenVersions.TryRemove(path,out _)','return IsOrdinaryUserFile(path,explicitRoots);',
    'Filter="*"','NotifyFilters.LastWrite')){
    if(!$watch.Contains($t)){throw "R59 file watcher missing $t"}
}
if($watch.Contains('candidates.Add(drive.RootDirectory.FullName);') -or
    $watch.Contains('folders.Add(drive.RootDirectory.FullName);')){
    throw 'R59 still watches entire local drives'
}
foreach($t in @('BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=310',
    'TextWrapping=TextWrapping.Wrap','ApplicationsParentCenter',
    'start+i*step,start+(i+1)*step',
    'SnapshotApplications()','Path.GetDirectoryName(x.Path)')){
    if(!$apps.Contains($t)){throw "R59 Apps geometry/name fix missing $t"}
}
foreach($t in @('SelectWatchedFolder()',
    'IsAllowedWatchFolder(folder)','self.newFiles.Start()')){
    if(!$shelf.Contains($t)){throw "R59 user-root action missing $t"}
}
if(!$widget.Contains('DeskSpaceDropShelfV15.SelectWatchedFolder')){
    throw 'R59 saved folder action not linked from the widget'
}
Write-Host 'R59 PASS: no volume-wide file watch, approved user folders, versioned notifications, connected donut geometry and readable application names.'
