$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Read([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$from,[string]$to,[string]$label)
{
 $from=$from.Replace([string][char]13+[char]10,[string][char]10)
 $to=$to.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($n -ne 1){throw "R82 $label anchor count=$n"}
 $script:s=$script:s.Replace($from,$to)
}
Copy-Item 'r82/DeskSpaceStartupV82.cs' "$root/DeskSpaceStartupV82.cs" -Force
Copy-Item 'r82/DeskSpaceRecentPlacementV82.cs' "$root/DeskSpaceRecentPlacementV82.cs" -Force
Copy-Item 'r82/DeskSpaceNewFileWatchV82.cs' "$root/DeskSpaceNewFileWatchV53.cs" -Force
$p="$root/WidgetHost.cs";$s=Read $p
One '            attachedShelf.Enable();' @'
            attachedShelf.Enable();
            // First use registers Windows logon startup. Later the user
            // can turn it off in Settings; on every launch refresh the
            // Run entry to the current installed executable path.
            DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);
'@ 'startup init'
Save $p $s
$p="$root/DeskSpaceDropShelfV15.cs";$s=Read $p
One 'PlacementTarget=host,Placement=PlacementMode.Bottom,' 'PlacementTarget=host,Placement=PlacementMode.Custom,' 'custom placement'
One @'
            Child=panel
        };
'@ @'
            Child=panel
        };
        popup.CustomPopupPlacementCallback=(size,target,offset)=>{
            var widget=new Rect(host.Left,host.Top,
                host.ActualWidth>0?host.ActualWidth:host.Width,
                host.ActualHeight>0?host.ActualHeight:host.Height);
            var chosen=DeskSpaceRecentPlacementV82.Choose(
                widget,SystemParameters.WorkArea,size);
            return new[]{new CustomPopupPlacement(
                chosen,PopupPrimaryAxis.None)};
        };
'@ 'popup work area'
One '        Render();
    }
    public void SetMediaOffset(' @'
        ScheduleRecentRender();
    }
    private readonly System.Windows.Threading.DispatcherTimer recentRenderTimer=
        new(){Interval=TimeSpan.FromMilliseconds(180)};
    private bool recentTimerAttached;
    private void ScheduleRecentRender()
    {
        if(disposed)return;
        if(!recentTimerAttached)
        {
            recentTimerAttached=true;
            recentRenderTimer.Tick+=(_,_)=>{
                recentRenderTimer.Stop();
                if(!disposed)Render();
            };
        }
        recentRenderTimer.Stop();
        recentRenderTimer.Start();
    }
    public void SetMediaOffset(
'@ 'coalesce new file UI'
One '        popup.IsOpen=false;
        newFiles?.Dispose();' @'
        popup.IsOpen=false;
        recentRenderTimer.Stop();
        newFiles?.Dispose();
'@ 'stop recent timer'
One '(item.AutoDetected?"NEW · ":"⋮⋮ ")+item.Name,' '(item.AutoDetected?"Recent · ":"Held · ")+item.Name,' 'meaningful label'
Save $p $s
Write-Host 'R82: logon widget startup, reliable under-circle popup placement and bounded 24-hour recent-file recovery with batched UI.'
