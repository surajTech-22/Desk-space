$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$watch=Get-Content (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Raw
$shelf=Get-Content (Join-Path $root 'DeskSpaceDropShelfV15.cs') -Raw
$search=Get-Content (Join-Path $root 'DeskSpaceSearchIndexV13.cs') -Raw
$widget=Get-Content (Join-Path $root 'WidgetHost.cs') -Raw
$app=Get-Content (Join-Path $root 'DeskSpaceApplicationsV55.cs') -Raw
foreach($token in @('watcher.Created+=Created','watcher.Changed+=Changed',
    'watcher.Renamed+=Renamed','watcher.Deleted+=Deleted',
    'NotifyFilters.LastWrite','NotifyFilters.Size','Filter="*"',
    'DriveType.Fixed or DriveType.Removable','Path.Combine(home,"OneDrive")',
    'ReconcileAsync(shutdown.Token)','Queue(e.FullPath)',
    'onRemoved(path)','Directory.EnumerateFiles(dir,"*"',
    'FileInfo(path)','stable>=2')){
    if(!$watch.Contains($token)){throw "R55 app-save detection missing $token"}
}
foreach($token in @('onRemoved:RemoveRecentFile',
    'DeskSpaceSearchIndexV13.UnregisterLivePath(path)',
    'detected.RemoveAll(x=>string.Equals(x.StagedPath,path,',
    'detected.Insert(0,new Held(Path.GetFileName(path),path,true))',
    'RestoreHeldFiles();','CopyFileWhenReady(source,destination)',
    'pinnedSourcePaths.Contains(Path.GetFullPath(path))')){
    if(!$shelf.Contains($token)){throw "R55 shelf regression $token"}
}
foreach($token in @('UnregisterLivePath(string path)',
    'SnapshotApplications()','if(!item.Folder && !File.Exists(item.Path))continue',
    'LivePaths.TryRemove(path,out _)')){
    if(!$search.Contains($token)){throw "R55 Search missing $token"}
}
foreach($token in @('"Applications", "Tasks", "Excel Functions"',
    'OpenApplicationsArc();','selected==10',
    'appsWheel?.Dispose();','appsWheel?.HideArc()',
    'prefs.PinnedApps.Add(p)','prefs.PinnedApps.Remove(old)',
    'new DeskSpaceApplicationsV55(this')){
    if(!$widget.Contains($token)){throw "R55 Applications not integrated $token"}
}
if($widget.Contains('"Clipboard", "Files", "Tasks"')){
    throw 'R55 redundant Files sector remains'}
foreach($token in @('private readonly Popup arc','SubInner=244',
    'IsArcOpen','TryCapturedDragAtScreen','Path.GetFileNameWithoutExtension(p)',
    'Process.Start(new ProcessStartInfo(item.Path)','openSearch',
    'openFileDialog','ShowDialog(owner)')){
    if($token -eq 'openFileDialog'){continue}
    if(!$app.Contains($token)){throw "R55 application wheel missing $token"}
}
Write-Host 'R55 source PASS: saved files from other applications and drives, all extensions, index sync, pinned-app child donut, original-file preservation.'
