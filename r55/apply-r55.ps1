$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r55/DeskSpaceNewFileWatchV55.cs' -Destination (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Force
Copy-Item -LiteralPath 'r55/DeskSpaceApplicationsV55.cs' -Destination (Join-Path $root 'DeskSpaceApplicationsV55.cs') -Force
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$source){[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))}
function Swap([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R55 patch anchor found $count times: $old"}
    $script:s=$script:s.Replace($old,$new)
}
$p=Join-Path $root 'DeskSpaceDropShelfV15.cs';$s=Read $p
Swap 'new DeskSpaceNewFileWatchV53(host,OfferNewFile);' 'new DeskSpaceNewFileWatchV53(host,OfferNewFile,onRemoved:RemoveRecentFile);'
Swap '    private void OfferNewFile(string path)' @'
    private void RemoveRecentFile(string path)
    {
        if(disposed)return;
        DeskSpaceSearchIndexV13.UnregisterLivePath(path);
        if(detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase))>0)Render();
    }
    private void OfferNewFile(string path)
'@
Swap @'
        if(held.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase))||
           detected.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase)))return;
        detected.Insert(0,new Held(Path.GetFileName(path),path,true));
'@ @'
        if(held.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase)))return;
        // Saving an existing Excel/Word/PDF should bring it to the top as
        // recently saved, not get discarded as already discovered.
        detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase));
        detected.Insert(0,new Held(Path.GetFileName(path),path,true));
'@
Save $p $s

$p=Join-Path $root 'DeskSpaceSearchIndexV13.cs';$s=Read $p
Swap '    public static void WarmStart()=>Start();' @'
    public static void WarmStart()=>Start();
    public static void UnregisterLivePath(string path)
    {
        try{LivePaths.TryRemove(Path.GetFullPath(path),out _);}
        catch(ArgumentException){}
    }
    public static IReadOnlyList<SearchHit> SnapshotApplications()
    {
        lock(Gate)return Applications.ToArray();
    }
'@
Swap @'
                if(!Matches(item.Name,fileQuery) && !Matches(item.Path,fileQuery))continue;
'@ @'
                if(!Matches(item.Name,fileQuery) && !Matches(item.Path,fileQuery))continue;
                // A deleted file must not linger in the old full-drive
                // snapshot after the live watcher removed its path.
                if(!item.Folder && !File.Exists(item.Path))continue;
                if(item.Folder && !Directory.Exists(item.Path))continue;
'@
Save $p $s

$p=Join-Path $root 'WidgetHost.cs';$s=Read $p
Swap '    private DeskSpaceExcelWheelV42? excelWheel;' @'
    private DeskSpaceExcelWheelV42? excelWheel;
    private DeskSpaceApplicationsV55? appsWheel;
'@
Swap '            excelWheel?.Dispose();' @'
            excelWheel?.Dispose();
            appsWheel?.Dispose();
'@
Swap @'
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks", "Excel Functions"
'@ @'
        "Information", "Notes", "Reminders", "Clipboard", "Applications", "Tasks", "Excel Functions"
'@
Swap @'
        "◆", "✎", "◉", "▣", "▤", "☑", "▥"
'@ @'
        "◆", "✎", "◉", "▣", "◈", "☑", "▥"
'@
Swap @'
    private void ClickSegment(string name)
    {
        if(name=="Excel Functions")
'@ @'
    private void ClickSegment(string name)
    {
        if(name=="Applications")
        {
            OpenApplicationsArc();
            return;
        }
        if(name=="Excel Functions")
'@
Swap '    private void OpenExcelArc()' @'
    private void OpenApplicationsArc()
    {
        if(activeModule!=null)return;
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        appsWheel??=new DeskSpaceApplicationsV55(this,
            ()=>prefs.PinnedApps.ToArray(),
            p=>{
                if(!prefs.PinnedApps.Any(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase)))
                {prefs.PinnedApps.Add(p);prefs.Save();}
            },
            p=>{
                var old=prefs.PinnedApps.FirstOrDefault(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase));
                if(old!=null){prefs.PinnedApps.Remove(old);prefs.Save();}
            },
            ()=>OpenInWidget("Search"));
        if(!appsWheel.IsArcOpen)appsWheel.Show();
        if(appsWheel.IsArcOpen)
            content.Text="Applications"+Environment.NewLine+
                appsWheel.PinnedCount+" pinned";
    }

    private void OpenExcelArc()
'@
Swap '        if(selected==highlightedSector)return;' @'
        // Applications replaces the duplicate Files sector at outer index 10.
        if(selected==10 && ringExpanded && activeModule==null)
            OpenApplicationsArc();
        if(selected>=0 && selected!=10 && appsWheel?.IsArcOpen==true)
            appsWheel.HideArc();
        if(selected==highlightedSector)return;
'@
Swap @'
    private void CollapseRing()
    {
'@ @'
    private void CollapseRing()
    {
        appsWheel?.HideArc();
'@
Swap @'
    private void BuildCompact()
    {
'@ @'
    private void BuildCompact()
    {
        appsWheel?.HideArc();
'@
Swap @'
        if(excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
'@ @'
        if(appsWheel?.IsArcOpen==true &&
           appsWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }
        if(excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
'@
Swap @'
        if(ringGesture && excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(
'@ @'
        if(ringGesture && appsWheel?.IsArcOpen==true &&
           appsWheel.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return;
        }
        if(ringGesture && excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(
'@
Save $p $s
Write-Host 'R55: Created/Changed/Renamed/Deleted file paths; cross-drive application Save As; live Search coherence; Applications donut with shared Search pins.'
