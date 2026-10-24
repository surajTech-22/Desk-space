$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r88/DeskSpaceShelfGroupsV88.cs' "$r/DeskSpaceShelfGroupsV88.cs" -Force
$p="$r/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw ('R92 '+$label+': expected one anchor, got '+$count)}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
    private bool appsExpanded=false,heldExpanded=true,recentExpanded=true;
    private bool deckMinimized;
'@ @'
    private bool heldExpanded=true,recentExpanded=true;
    private bool deckMinimized,deckDismissed;
    private string dismissedNonAppSignature="";
    private IReadOnlyList<DeskSpaceRunningAppsV88.Entry> runningWindows=
        Array.Empty<DeskSpaceRunningAppsV88.Entry>();
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath));
'@ 'separate transient deck dismissal and all-window state'
Swap @'
            var snapshot=await Task.Run(DeskSpaceRunningAppsV88.Snapshot);
            if(disposed)return;
            var previous=runningApps.Select(x=>x.Name+"|"+x.Handle).ToArray();
            var current=snapshot.Select(x=>x.Name+"|"+x.Handle).ToArray();
            runningApps=snapshot;
            if(!previous.SequenceEqual(current))Render();
'@ @'
            var windows=await Task.Run(DeskSpaceRunningAppsV88.SnapshotWindows);
            if(disposed)return;
            var snapshot=DeskSpaceRunningAppsV88.GroupVisible(windows);
            var previous=runningWindows.Select(x=>x.Name+"|"+x.Handle+
                "|"+x.WindowTitle).ToArray();
            var current=windows.Select(x=>x.Name+"|"+x.Handle+
                "|"+x.WindowTitle).ToArray();
            runningWindows=windows;
            runningApps=snapshot;
            if(!previous.SequenceEqual(current))Render();
'@ 'retain every live HWND, group for the app count'
Swap @'
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            popup.IsOpen=false;
            return;
        }
'@ @'
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            popup.IsOpen=false;
            return;
        }
        // Opening new apps never resurrects a dismissed deck. A new
        // held or recently created file does.
        if(deckDismissed)
        {
            string currentNonApp=NonAppSignature();
            if(held.Count+detected.Count>0 &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
                   StringComparison.Ordinal))
                deckDismissed=false;
            else
            {
                popup.IsOpen=false;
                return;
            }
        }
'@ 'suppress app-only reopening'
Swap 'if(held.Count+detected.Count+runningApps.Count==0 && !busy && !deckMinimized)' @'
if(held.Count+detected.Count+runningApps.Count==0 && !busy)
'@ 'collapse truly empty deck even if minimized'
Swap @'
        var close=DeckHeaderButton("×",
            "Hide deck (restore via circle > Deck Visibility)",()=>{
            if(!DeskSpaceDeckVisibilityV89.TrySet(false,out var problem))
                MessageBox.Show("Could not hide the deck: "+problem,
                    "DeskSpace deck visibility");
        });
'@ @'
        var close=DeckHeaderButton("×",
            "Dismiss deck until a new non-app action occurs",()=>{
            dismissedNonAppSignature=NonAppSignature();
            deckDismissed=true;
            popup.IsOpen=false;
        });
'@ 'transient close rather than permanent hide'
Swap @'
        var appsGroup=DeskSpaceShelfGroupsV88.Add(list,"Running Apps",
            runningApps.Count,appsExpanded,()=>{
                appsExpanded=!appsExpanded;Render();
            });
        if(appsExpanded)DeskSpaceRunningAppsV88.Fill(appsGroup,runningApps);
'@ @'
        // Running Apps is hover-only: first hover reveals app icons,
        // then app hover shows any additional windows.
        DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows);
'@ 'replace running app click expansion with hover'
Swap @'
        else
        {
            runningAppsTimer.Start();
            RefreshRunningApps();
            Render();
        }
'@ @'
        else
        {
            deckDismissed=false;
            runningAppsTimer.Start();
            RefreshRunningApps();
            Render();
        }
'@ 'explicit user visibility restores dismissed deck'
Swap @'
    {if(disposed)return;popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible &&
                (bool)e.NewValue&&
        ((held.Count+detected.Count+runningApps.Count)>0 || deckMinimized);}
'@ @'
    {if(disposed)return;popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible &&
        !deckDismissed && (bool)e.NewValue &&
        (held.Count+detected.Count+runningApps.Count)>0;}
'@ 'do not reopen dismissed or empty deck from widget movement'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R92 hover-only app/window picker, taskbar-style minimize, close suppressed for app-only events and reactivated on new non-app files.'
