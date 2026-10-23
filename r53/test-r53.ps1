$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$watch=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceNewFileWatchV53.cs'))
$shelf=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceDropShelfV15.cs'))
foreach($token in @(
    'Filter="*"','IncludeSubdirectories=true',
    'FileSystemWatcher(path)','watcher.Created+=Created',
    'watcher.Renamed+=Renamed','Queue(e.FullPath)',
    'DeskSpaceNewFileWatchV53(Window owner,Action<string> onNewFile,',
    'Environment.SpecialFolder.MyDocuments','Path.Combine(home,"Downloads")',
    'Environment.SpecialFolder.DesktopDirectory',
    'onNewFile(full)','new FileSystemWatcher')){
    if(!$watch.Contains($token)){
        throw "R53 file watcher missing required all-extension detection: $token"
    }
}
if($watch.Contains('Filter="*.pdf"') -or $watch.Contains('Filter="*.xlsx"')){
    throw 'R53 must monitor every file extension'
}
foreach($token in @(
    'new DeskSpaceNewFileWatchV53(host,OfferNewFile)',
    'private void OfferNewFile(string path)',
    'detected.Insert(0,new Held(Path.GetFileName(path),path,true))',
    'foreach(var item in held.Concat(detected).ToArray())',
    'New files · drag onto circle to keep a copy',
    'int removed=detected.RemoveAll(x=>string.Equals(x.StagedPath,source,',
    'if(removed>0)pinnedSourcePaths.Add(Path.GetFullPath(source));',
    'if(pinnedSourcePaths.Contains(Path.GetFullPath(path)))return;',
    'if(disposed||busy||held.Count>=25)',
    'e.Effects=DragDropEffects.None;',
    'else CopyFolder(source,destination)',
    'File.Copy(source,destination,false)',
    'DragDrop.DoDragDrop(row,data,DragDropEffects.Copy)',
    'if(item.AutoDetected)',
    'detected.Remove(item);',
    'newFiles?.Dispose()',
    'Remove(item)',
    'if(effect==DragDropEffects.Copy)Remove(item);'
    )){
    # The R16 safety gate removed the final unsafe Remove-on-drop line.
    if($token -eq 'if(effect==DragDropEffects.Copy)Remove(item);'){
        if($shelf.Contains($token)){throw 'R53 held copies deleted on external drop'}
        continue
    }
    if(!$shelf.Contains($token)){throw "R53 shared shelf path missing $token"}
}
if($shelf.Contains('DeletePath(item.StagedPath);') -and
   !$shelf.Contains('if(item.AutoDetected)')){
    throw 'R53 discovered files must never be deleted by shelf dismiss'
}
if($shelf.Contains('newFiles?.Dispose()') -eq $false){
    throw 'R53 watchers not disposed with widget'
}
Write-Host 'R53 source PASS: Desktop/Documents/Downloads/all extensions, existing drag-to-circle file copies, original-file safety.'
