$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$shelf=Get-Content (Join-Path $root 'DeskSpaceDropShelfV15.cs') -Raw
$search=Get-Content (Join-Path $root 'DeskSpaceSearchIndexV13.cs') -Raw
$hostCode=Get-Content (Join-Path $root 'WidgetHost.cs') -Raw
$watch=Get-Content (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Raw
foreach($required in @(
  '"DeskSpace","TemporaryShelf","Retained"',
  'if(!restoredHeldFiles)',
  'RestoreHeldFiles();',
  'Directory.EnumerateDirectories(staging)',
  'held.Add(new Held(Path.GetFileName(items[0]),items[0]))',
  'if(item.AutoDetected)',
  'detected.Remove(item);',
  'DeletePath(item.StagedPath);',
  'if(removed>0)pinnedSourcePaths.Add(Path.GetFullPath(source))',
  'FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None)',
  'CopyFileWhenReady(source,destination)',
  'DeskSpaceSearchIndexV13.RegisterLivePath(path)',
  'e.Effects=(busy||disposed||held.Count>=25)?DragDropEffects.None:DragDropEffects.Copy'
)){
 if(!$shelf.Contains($required)){throw "R54 shelf safety/preloading regression: $required"}
}
if($shelf.Contains('Guid.NewGuid().ToString("N"));') -and
   $shelf.Contains('"DeskSpace","TemporaryShelf",Guid.NewGuid().ToString("N"));')){
 throw 'R54 still uses a transient session shelf'
}
foreach($required in @(
  'public static void WarmStart()=>Start();',
  'public static void RegisterLivePath(string path)',
  'ConcurrentDictionary<string,byte> LivePaths',
  'foreach(var path in LivePaths.Keys)',
  'LivePaths.TryRemove(path,out _)',
  'Name=Path.GetFileName(path)',
  'OpenTarget=path,Location=path'
)){
 if(!$search.Contains($required)){throw "R54 search missing: $required"}
}
if(!$hostCode.Contains('DeskSpaceSearchIndexV13.WarmStart();')){
 throw 'R54 search does not start until UI is opened'
}
if(!$watch.Contains('Filter="*"') -or !$watch.Contains('watcher.Renamed+=Renamed')){
 throw 'R54 must retain all-file-type watchers and rename detection'
}
if($shelf.Contains('if(effect==DragDropEffects.Copy)Remove(item);')){
 throw 'R54 must retain held files after a successful external drag'
}
Write-Host 'R54 PASS: retained copies, all-format source watchers, safe finished-file copy, live Search feed and startup warmup.'
