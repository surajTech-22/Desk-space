$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
# Earlier R93 already imports the updated live flyout source. Patch only
# the final compiled wrapper + shelf so that both levels stay synchronised.
$p="$root/DeskSpaceShelfGroupsV88.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R97 $label requires exactly one anchor, found $n"}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
    internal static void AddHoverGroup(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows);
    }
'@ @'
    // Keep this original three-argument method for callers and smoke tests.
    internal static void AddHoverGroup(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows);
    }

    // The deck uses a distinct method, avoiding reflection overload
    // ambiguity and preserving the original public method contract.
    internal static void AddHoverGroupLive(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows,
        Action<IReadOnlyList<Entry>,IReadOnlyList<Entry>> onLiveChanged,
        Action onHoverClosed)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows,
            onLiveChanged,onHoverClosed);
    }
'@ 'backwards compatible flyout callback forwarding'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))

$p="$root/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
Swap 'Interval=TimeSpan.FromSeconds(9)' 'Interval=TimeSpan.FromSeconds(2)' 'quicker idle app snapshots'
Swap @'
    private bool deckMinimized,deckDismissed;
'@ @'
    private bool deckMinimized,deckDismissed,appRenderPending;
'@ 'avoid dropping the popup during a timed refresh'
Swap @'
            runningWindows=windows;
            runningApps=snapshot;
            if(!previous.SequenceEqual(current))Render();
'@ @'
            if(!previous.SequenceEqual(current))
            {
                if(DeskSpaceStableHoverV93.IsHoverActive)
                    appRenderPending=true;
                else
                {
                    runningWindows=windows;
                    runningApps=snapshot;
                    Render();
                }
            }
'@ 'keep the hovered list alive during async scans'
Swap @'
        DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows);
'@ @'
        DeskSpaceRunningAppsV88.AddHoverGroupLive(list,runningApps,runningWindows,
            (appsNow,windowsNow)=>{
                if(disposed)return;
                runningApps=appsNow;
                runningWindows=windowsNow;
                heading.Text=busy?"Copying…":
                    "Deck · "+appsNow.Count+" apps · "+
                    held.Count+" held · "+detected.Count+" new";
                if(appsNow.Count==0)Render();
            },
            ()=>{
                if(disposed||!appRenderPending)return;
                appRenderPending=false;
                // Let WPF close the popup HWND before touching the deck.
                host.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    Render();
                    RefreshRunningApps();
                }));
            });
'@ 'live header count without hover UI teardown'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R97: 220ms live-window removal while hover is open, 2s idle snapshots, dynamic deck counts and no popup teardown on periodic scan.'
