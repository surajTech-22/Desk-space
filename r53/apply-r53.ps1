$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r53/DeskSpaceNewFileWatchV53.cs' -Destination (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Force
$path=Join-Path $root 'DeskSpaceDropShelfV15.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function One([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R53 shelf patch anchor found $count times: $old"}
    $script:s=$script:s.Replace($old,$new)
}
One '    private sealed record Held(string Name,string StagedPath);' @'
    // Auto-discovered files remain ORIGINAL references until explicitly
    // dragged onto the circle; manual/copied entries remain staged copies.
    private sealed record Held(string Name,string StagedPath,bool AutoDetected=false);
'@
One '    private readonly List<Held> held=new();' @'
    private readonly List<Held> held=new();
    private readonly List<Held> detected=new();
    private DeskSpaceNewFileWatchV53? newFiles;
'@
One @'
            MinWidth=180,MaxWidth=310,
            Child=list,AllowDrop=true
'@ @'
            MinWidth=180,MaxWidth=310,
            Child=new ScrollViewer{
                Content=list,MaxHeight=295,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled
            },AllowDrop=true
'@
One '    public void Enable(){if(!disposed)Render();}' @'
    public void Enable()
    {
        if(disposed)return;
        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile);
        newFiles.Start();
        Render();
    }
    private void OfferNewFile(string path)
    {
        if(disposed||!File.Exists(path))return;
        if(held.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase))||
           detected.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase)))return;
        detected.Insert(0,new Held(Path.GetFileName(path),path,true));
        while(detected.Count>30)detected.RemoveAt(detected.Count-1);
        Render();
    }
'@
One '    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&held.Count>0;}' @'
    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&(held.Count+detected.Count)>0;} 
'@
One @'
                held.Add(new Held(Path.GetFileName(destination),destination));
                Render();
'@ @'
                // The user deliberately dragged this newly detected source
                // ONTO the circle: convert the discovery row into the same
                // durable, staged copy already used for manually dropped files.
                detected.RemoveAll(x=>string.Equals(x.StagedPath,source,
                    StringComparison.OrdinalIgnoreCase));
                held.Add(new Held(Path.GetFileName(destination),destination));
                Render();
'@
One @'
    private void Remove(Held item)
    {
        held.Remove(item);
        DeletePath(item.StagedPath);
        Render();
    }
'@ @'
    private void Remove(Held item)
    {
        if(item.AutoDetected)
            detected.Remove(item); // Never delete the user's original file
        else
        {
            held.Remove(item);
            DeletePath(item.StagedPath);
        }
        Render();
    }
'@
One '        if(held.Count==0 && !busy){popup.IsOpen=false;return;}' @'
        // Display live discovery entries AND existing manually staged files
        // on exactly the same below-circle Popup, without blocking the ring.
        detected.RemoveAll(x=>!File.Exists(x.StagedPath));
        if(held.Count+detected.Count==0 && !busy)
        {
            popup.IsOpen=false;
            return;
        }
'@
One @'
        var clear=new Button{Content="Clear",FontSize=11,Padding=new Thickness(5,2,5,2),
            ToolTip="Discard staged copies only; never delete original files"};
'@ @'
        var clear=new Button{Content="Clear copies",FontSize=11,
            Padding=new Thickness(5,2,5,2),
            ToolTip="Remove staged copies only; never delete original files"};
'@
One @'
        DockPanel.SetDock(clear,Dock.Right);title.Children.Add(clear);
        title.Children.Add(Text(busy?"Copying files…":"Temporary files · "+held.Count,
            12,Brushes.White));
        list.Children.Add(title);
        foreach(var item in held.ToArray())
'@ @'
        DockPanel.SetDock(clear,Dock.Right);title.Children.Add(clear);
        var dismissNew=new Button{Content="Dismiss new",FontSize=11,
            Padding=new Thickness(5,2,5,2),
            ToolTip="Hide the recent file suggestions; originals are untouched"};
        dismissNew.Click+=(_,_)=>{detected.Clear();Render();};
        DockPanel.SetDock(dismissNew,Dock.Right);title.Children.Add(dismissNew);
        title.Children.Add(Text(busy?"Copying files…":
            "Files · "+held.Count+" held / "+detected.Count+" new",
            12,Brushes.White));
        list.Children.Add(title);
        bool recentHeading=false;
        foreach(var item in held.Concat(detected).ToArray())
        {
            if(item.AutoDetected && !recentHeading)
            {
                recentHeading=true;
                list.Children.Add(Text(
                    "New files · drag onto circle to keep a copy",
                    11,Brushes.LightBlue));
            }
'@
# The replacement above opens the loop body; the original loop already
# contained an opening brace, so remove its immediately following brace.
One @'
        {
            var row=new Border{
'@ @'
            var row=new Border{
'@
One '                ToolTip="Drag to another application to copy · click copy for Ctrl+V"' @'
                ToolTip=item.AutoDetected
                    ?"New file; drag onto DeskSpace circle to hold a copy"
                    :"Held copy; drag to another application or click Copy"
'@
One '                ToolTip="Discard temporary copy"};' @'
                ToolTip=item.AutoDetected
                    ?"Dismiss this suggestion (original file stays)"
                    :"Discard temporary copy"};' 
'@
One '            fileInfo.Children.Add(Text("⋮⋮ "+item.Name,12,Brushes.White));' @'
            fileInfo.Children.Add(Text(
                (item.AutoDetected?"NEW · ":"⋮⋮ ")+item.Name,
                12,Brushes.White));
'@
One @'
        popup.IsOpen=false;
        host.PreviewDragOver-=DragOver;
'@ @'
        popup.IsOpen=false;
        newFiles?.Dispose();
        newFiles=null;
        host.PreviewDragOver-=DragOver;
'@
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R53: all-file-type creation events appear in existing below-circle shelf; drag to circle creates held copy; dismiss never deletes original.'
