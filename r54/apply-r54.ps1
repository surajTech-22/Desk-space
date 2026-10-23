$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Read([string]$path) { [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10) }
function Save([string]$path,[string]$source) { [IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false)) }
function Swap([string]$old,[string]$replacement) {
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1) { throw "R54 patch anchor count $count : $old" }
    $script:s=$script:s.Replace($old,$replacement)
}

# The R53 temporary staging path was session-random, with no recovery index.
# Keep files within the original DeskSpace local app-data boundary but use a
# stable Retained directory. Existing user files are never moved/deleted.
$p=Join-Path $root 'DeskSpaceDropShelfV15.cs'; $s=Read $p
Swap '"DeskSpace","TemporaryShelf",Guid.NewGuid().ToString("N"));' '"DeskSpace","TemporaryShelf","Retained");'
Swap @'
    public void Enable()
    {
        if(disposed)return;
        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile);
'@ @'
    private bool restoredHeldFiles;
    public void Enable()
    {
        if(disposed)return;
        if(!restoredHeldFiles)
        {
            restoredHeldFiles=true;
            RestoreHeldFiles();
        }
        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile);
'@
Swap '    private void OfferNewFile(string path)' @'
    // Reconstruct retained copies after restart without touching originals.
    // Each GUID staging folder holds exactly one copied file or directory.
    private void RestoreHeldFiles()
    {
        try
        {
            if(!Directory.Exists(staging))return;
            foreach(var slot in Directory.EnumerateDirectories(staging)
                .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                if(held.Count>=25)break;
                try
                {
                    if((File.GetAttributes(slot)&FileAttributes.ReparsePoint)!=0)continue;
                    var items=Directory.EnumerateFileSystemEntries(slot).Take(2).ToArray();
                    if(items.Length!=1)continue;
                    if((File.GetAttributes(items[0])&FileAttributes.ReparsePoint)!=0)continue;
                    held.Add(new Held(Path.GetFileName(items[0]),items[0]));
                }
                catch(IOException){}
                catch(UnauthorizedAccessException){}
            }
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
    private void OfferNewFile(string path)
'@
Swap '        if(disposed||!File.Exists(path))return;' @'
        if(disposed||!File.Exists(path))return;
        // Register the name/path with Search before any Search window opens.
        DeskSpaceSearchIndexV13.RegisterLivePath(path);
'@
Swap '        e.Effects=busy?DragDropEffects.None:DragDropEffects.Copy;' '        e.Effects=(busy||disposed||held.Count>=25)?DragDropEffects.None:DragDropEffects.Copy;'
Swap '                    if(File.Exists(source))File.Copy(source,destination,false);' '                    if(File.Exists(source))CopyFileWhenReady(source,destination);'
Swap '    private static void CopyFolder(string source,string destination)' @'
    // FileSystemWatcher.Created can arrive before Save As / browser writes
    // finish. An exclusive read prevents retaining an incomplete copy.
    // Only the background Task.Run worker waits; WPF remains responsive.
    private static void CopyFileWhenReady(string source,string destination)
    {
        IOException? last=null;
        for(int attempt=0;attempt<20;attempt++)
        {
            try
            {
                using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None);
                using var output=new FileStream(destination,FileMode.Create,FileAccess.Write,FileShare.None);
                input.CopyTo(output);
                output.Flush(true);
                return;
            }
            catch(IOException ex)
            {
                last=ex;
                try{if(File.Exists(destination))File.Delete(destination);}
                catch(IOException){}
                catch(UnauthorizedAccessException){}
                if(attempt<19)System.Threading.Thread.Sleep(200);
            }
        }
        throw new IOException("File is still being saved. Finish saving and drag it again.",last);
    }
    private static void CopyFolder(string source,string destination)
'@
Swap '        // Keep staged bytes after app shutdown so delayed external paste reads' @'
        // Retained bytes survive restart; Remove and Clear explicitly discard
        // only the staged copy, not its original source path.
        // Keep staged bytes after app shutdown so delayed external paste reads
'@
Save $p $s

# Warm the existing shared Search index on widget startup, in parallel with
# the directory watchers. Searches consume the same live paths without
# waiting for Search/Files to be opened.
$p=Join-Path $root 'DeskSpaceSearchIndexV13.cs'; $s=Read $p
Swap @'
    private static readonly List<Entry> Entries = new();
'@ @'
    private static readonly List<Entry> Entries = new();
    private static readonly ConcurrentDictionary<string,byte> LivePaths=
        new(StringComparer.OrdinalIgnoreCase);
'@
Swap '    private static void Start()' @'
    public static void WarmStart()=>Start();
    public static void RegisterLivePath(string path)
    {
        try
        {
            var full=Path.GetFullPath(path);
            if(File.Exists(full))LivePaths.TryAdd(full,0);
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
        catch(ArgumentException){}
    }
    private static void Start()
'@
Swap @'
        foreach(var hit in indexed)
            if(!scoped || hit.OpenTarget.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))
                Add(hit);
'@ @'
        foreach(var hit in indexed)
            if(!scoped || hit.OpenTarget.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))
                Add(hit);
        // Immediately searchable even if the full-drive scan is incomplete
        // or Windows Search has not indexed a newly created filename.
        if(!fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
            foreach(var path in LivePaths.Keys)
            {
                cancellation.ThrowIfCancellationRequested();
                if(!File.Exists(path))
                {
                    LivePaths.TryRemove(path,out _);
                    continue;
                }
                if(scoped && !path.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))continue;
                if(Matches(Path.GetFileName(path),fileQuery)||Matches(path,fileQuery))
                    Add(new SearchHit{Category="Files",Name=Path.GetFileName(path),
                        OpenTarget=path,Location=path});
            }
'@
Save $p $s
$p=Join-Path $root 'WidgetHost.cs'; $s=Read $p
Swap '            attachedShelf.Enable();' @'
            attachedShelf.Enable();
            DeskSpaceSearchIndexV13.WarmStart();
'@
Save $p $s
Write-Host 'R54: retained all-format files survive restart; new paths shared with always-warm Search; safe copy waits for finished writes.'
