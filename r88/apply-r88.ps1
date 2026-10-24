$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r88/DeskSpaceShelfGroupsV88.cs' "$root/DeskSpaceShelfGroupsV88.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Swap([string]$before,[string]$after,[string]$label)
{
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($n -ne 1){throw "R88 $label expected one integration point, found $n"}
    $script:s=$script:s.Replace($before,$after)
}
Swap 'using System.Threading.Tasks;' @'
using System.Threading.Tasks;
using System.Windows.Threading;
'@ 'dispatcher import'
Swap '    private bool busy, disposed;' @'
    private bool busy, disposed;
    private bool appsExpanded=false,heldExpanded=true,recentExpanded=true;
    private IReadOnlyList<DeskSpaceRunningAppsV88.Entry> runningApps=
        Array.Empty<DeskSpaceRunningAppsV88.Entry>();
    private readonly DispatcherTimer runningAppsTimer=new(){
        Interval=TimeSpan.FromSeconds(9)
    };
    private bool runningAppsTimerConnected,refreshingRunningApps;
'@ 'running app grouping state'
Swap '        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile,onRemoved:RemoveRecentFile);' @'
        if(!runningAppsTimerConnected)
        {
            runningAppsTimerConnected=true;
            runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();
        }
        runningAppsTimer.Start();
        RefreshRunningApps();
        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile,onRemoved:RemoveRecentFile);
'@ 'nonblocking app refresh'
Swap '    private void Render()' @'
    // Scan in the background. Do not block circle hover, dragging, or file drops.
    private async void RefreshRunningApps()
    {
        if(disposed||refreshingRunningApps)return;
        refreshingRunningApps=true;
        try
        {
            var snapshot=await Task.Run(DeskSpaceRunningAppsV88.Snapshot);
            if(disposed)return;
            var previous=runningApps.Select(x=>x.Name+"|"+x.Handle).ToArray();
            var current=snapshot.Select(x=>x.Name+"|"+x.Handle).ToArray();
            runningApps=snapshot;
            if(!previous.SequenceEqual(current))Render();
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DeskSpace running apps refresh: "+ex);
        }
        finally{refreshingRunningApps=false;}
    }
    private void Render()
'@ 'background app snapshot'
Swap '        if(held.Count+detected.Count==0 && !busy)' @'
        if(held.Count+detected.Count+runningApps.Count==0 && !busy)
'@ 'allow app only shelf'
Swap '    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&(held.Count+detected.Count)>0;}' @'
    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&
        (held.Count+detected.Count+runningApps.Count)>0;}
'@ 'show app only shelf on widget visible'
Swap @'
        bool recentHeading=false;
        foreach(var item in held.Concat(detected).ToArray())
'@ @'
        var appsGroup=DeskSpaceShelfGroupsV88.Add(list,"Running Apps",
            runningApps.Count,appsExpanded,()=>{
                appsExpanded=!appsExpanded;Render();
            });
        if(appsExpanded)DeskSpaceRunningAppsV88.Fill(appsGroup,runningApps);
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
            held.Count,heldExpanded,()=>{
                heldExpanded=!heldExpanded;Render();
            });
        var recentGroup=DeskSpaceShelfGroupsV88.Add(list,"Recent Files",
            detected.Count,recentExpanded,()=>{
                recentExpanded=!recentExpanded;Render();
            });
        bool recentHeading=false;
        foreach(var item in
            (heldExpanded?held:Enumerable.Empty<Held>()).Concat(
             recentExpanded?detected:Enumerable.Empty<Held>()).ToArray())
'@ 'three compact groups'
Swap @'
                list.Children.Add(Text(
                    "New files · drag onto circle to keep a copy",
                    11,Brushes.LightBlue));
'@ @'
                // The "Recent Files" group already identifies these rows.
'@ 'remove duplicate group label'
Swap '            list.Children.Add(row);' @'
            (item.AutoDetected?recentGroup:heldGroup).Children.Add(row);
'@ 'route original file controls to group content'
Swap @'
        recentRenderTimer.Stop();
        newFiles?.Dispose();
'@ @'
        recentRenderTimer.Stop();
        runningAppsTimer.Stop();
        newFiles?.Dispose();
'@ 'stop app snapshot timer'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R88: compact running applications, held files and recent files groups on the original below-circle shelf; Excel/reminder/clipboard functions unmodified.'
