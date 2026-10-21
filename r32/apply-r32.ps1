$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r32/DeskSpaceInformationV32.cs' "$root/DeskSpaceInformationV32.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$before,[string]$after)
{
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($before)){throw "R32 WidgetHost source guard failed: $before"}
    $script:s=$script:s.Replace($before,$after)
}
Must 'DeskSpaceInformationV31? informationOverlay' 'DeskSpaceInformationV32? informationOverlay'
Must 'new DeskSpaceInformationV31(this,' 'new DeskSpaceInformationV32(this,'

# The original OnGestureMove only calls SectorAt on the main donut when
# ringGesture has captured the mouse. It cannot see child Popup sectors.
# Forward GLOBAL POINTER COORDINATES before the parent SectorAt dispatch.
$moveBegin=$s.IndexOf('    private void OnGestureMove(',[StringComparison]::Ordinal)
$moveEnd=if($moveBegin -ge 0){$s.IndexOf('    private void OnGestureUp(', $moveBegin,[StringComparison]::Ordinal)}else{-1}
if($moveBegin -lt 0 -or $moveEnd -le $moveBegin){throw 'R32 mouse-move boundaries changed'}
$move=$s.Substring($moveBegin,$moveEnd-$moveBegin)
$prior=@'
        if(!ringGesture)return;
        UpdateHighlightedSector(p);
        e.Handled=true;
'@
$after=@'
        if(!ringGesture)return;
        // Parent WPF window owns CaptureMouse() while dragging. Convert
        // parent coordinates to screen coordinates and let the child popup
        // use PointFromScreen() for its real wedge geometry.
        if(informationOverlay?.IsArcOpen==true &&
           informationOverlay.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }
        informationOverlay?.EndCapturedDrag();
        UpdateHighlightedSector(p);
        e.Handled=true;
'@
$prior=$prior.Replace([string][char]13+[char]10,[string][char]10)
$after=$after.Replace([string][char]13+[char]10,[string][char]10)
if(!$move.Contains($prior)){throw 'R32 captured mouse move anchor missing'}
$move=$move.Replace($prior,$after)
$s=$s.Substring(0,$moveBegin)+$move+$s.Substring($moveEnd)

# Drop over a child wedge activates that child, rather than selecting
# the underlying main-ring segment and collapsing the donut.
$upBegin=$s.IndexOf('    private void OnGestureUp(',[StringComparison]::Ordinal)
$upEnd=if($upBegin -ge 0){$s.IndexOf('    private void ', $upBegin+26,[StringComparison]::Ordinal)}else{-1}
if($upBegin -lt 0 -or $upEnd -le $upBegin){throw 'R32 mouse-up boundaries changed'}
$up=$s.Substring($upBegin,$upEnd-$upBegin)
$beforeUp=@'
        holdTimer.Stop();
        holdPending = false;
'@
$afterUp=@'
        if(ringGesture && informationOverlay?.IsArcOpen==true &&
           informationOverlay.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return; // keep parent + child donut visible
        }
        informationOverlay?.EndCapturedDrag();
        holdTimer.Stop();
        holdPending = false;
'@
$beforeUp=$beforeUp.Replace([string][char]13+[char]10,[string][char]10)
$afterUp=$afterUp.Replace([string][char]13+[char]10,[string][char]10)
if(!$up.Contains($beforeUp) -or !$up.Contains('CollapseRing();') -or
   !$up.Contains('SectorAt(e.GetPosition(this))'))
{throw 'R32 original mouse release safety guard failed'}
$up=$up.Replace($beforeUp,$afterUp)
$s=$s.Substring(0,$upBegin)+$up+$s.Substring($upEnd)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R32: drag from the main donut into the child donut highlights under mouse capture, drop selects, simple click preserved; editor fields acquire keyboard focus while saving or editing.'
