$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function ReplaceMethod([string]$from,[string]$to,[string]$replacement)
{
    $begin=$script:s.IndexOf($from,[StringComparison]::Ordinal)
    $end=if($begin -ge 0){$script:s.IndexOf($to,$begin,[StringComparison]::Ordinal)}else{-1}
    if($begin -lt 0 -or $end -le $begin){throw "R28 method boundaries unavailable: $from"}
    $script:s=$script:s.Substring(0,$begin)+$replacement+[Environment]::NewLine+$script:s.Substring($end)
}

# R12 returned before running highlight when the mouse was not pressed,
# so R27 never saw a simple hover. Process hover before the old outside-ring
# collapse rule. Do not close the parent ring while its child arc/shelf lives.
$hoverMove=@'
    private void OnGestureMove(object sender,MouseEventArgs e)
    {
        if(activeModule!=null || draggingGesture || shortcutMoveInProgress)return;
        var p=e.GetPosition(this);
        if(Mouse.LeftButton!=MouseButtonState.Pressed)
        {
            if(ringExpanded)
            {
                // Ordinary pointer motion selects the parent donut segment.
                // No click, hold or mouse capture is needed to open children.
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsOpen==true)return;
                if(!ringGesture &&
                   Mouse.RightButton!=MouseButtonState.Pressed &&
                   DateTime.UtcNow>rightClickOpenedUntil)
                {
                    double radius=Math.Min(Width,Height)*196.0/420.0;
                    if((p-new Point(Width/2,Height/2)).Length>radius+4)
                        CollapseRing();
                }
            }
            return;
        }
        if(!ringGesture)return;
        UpdateHighlightedSector(p);
        e.Handled=true;
    }

'@
$begin=$s.IndexOf('    private void OnGestureMove(',[StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void OnGestureUp(', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R28 cannot inspect mouse move handler'}
$old=$s.Substring($begin,$end-$begin)
foreach($key in @('if(Mouse.LeftButton!=MouseButtonState.Pressed)','UpdateHighlightedSector(p)','CollapseRing()','ringGesture'))
{
    if(!$old.Contains($key)){throw "R28 old mouse gesture safety gate missing: $key"}
}
$s=$s.Substring(0,$begin)+$hoverMove+$s.Substring($end)

# Both hovered and held-ring selectors pass through this one method. Index 6
# is Information, the first outer-ring segment of the existing 12-sector menu.
$paint=@'
    private void UpdateHighlightedSector(Point p)
    {
        int selected=SectorAt(p);
        if(selected==6 && ringExpanded && activeModule==null)
        {
            // Parent wedge is reached: open contextual sub-donut on hover.
            // OpenInformationArc is idempotent to avoid popup flicker from
            // repeated MouseMove events.
            OpenInformationArc();
        }
        if(selected==highlightedSector)return;
        highlightedSector=selected;
        for(int i=0;i<ringPaths.Count;i++)
            ringPaths[i].Fill=i==selected?accent:
                (i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)));
    }

'@
ReplaceMethod '    private void UpdateHighlightedSector(' '    private void BuildCompact()' $paint

# Click and hover should be equivalent for the outer Information wedge.
# Previously clicking again CLOSED the arc. Repeated hover must never close.
$openStart=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
$openEnd=if($openStart -ge 0){$s.IndexOf('    private void ClickSegment(string name)', $openStart,[StringComparison]::Ordinal)}else{-1}
if($openStart -lt 0 -or $openEnd -le $openStart){throw 'R28 information arc lifecycle unavailable'}
$old=$s.Substring($openStart,$openEnd-$openStart)
foreach($key in @('informationOverlay.Hide()','Dispatcher.BeginInvoke','informationOverlay.Show()','ToggleRing()'))
{
    if(!$old.Contains($key)){throw "R28 old info lifecycle safety gate missing: $key"}
}
$info=@'
    private bool informationArcOpening;
    private void OpenInformationArc()
    {
        if(activeModule!=null ||
           informationOverlay?.IsOpen==true ||
           informationArcOpening)return;
        informationArcOpening=true;
        void ShowAttachedArc()
        {
            informationArcOpening=false;
            if(activeModule!=null || informationOverlay?.IsOpen==true)return;
            if(!ringExpanded)ToggleRing();
            if(!ringExpanded)return;
            var work=SystemParameters.WorkArea;
            // Retain existing R27 visibility constraints near screen edges.
            Left=Math.Clamp(Left,work.Left+42,
                Math.Max(work.Left+42,work.Right-OpenDiameter-44));
            Top=Math.Clamp(Top,work.Top+103,
                Math.Max(work.Top+103,work.Bottom-OpenDiameter));
            KeepOnVisibleDesktop();
            informationOverlay??=new DeskSpaceInformationV27(this,()=>{
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
                else UpdateCenter();
            });
            informationOverlay.Show();
            if(informationOverlay.IsOpen)
                content.Text="Information"+Environment.NewLine+
                    informationOverlay.SavedCount+" saved";
        }
        // Hover opens immediately. Only a press/release gesture waits until
        // the handler has finished so it cannot collapse the parent ring.
        if(ringExpanded &&
           Mouse.LeftButton!=MouseButtonState.Pressed &&
           Mouse.RightButton!=MouseButtonState.Pressed)
            ShowAttachedArc();
        else
            Dispatcher.BeginInvoke(new Action(ShowAttachedArc),
                System.Windows.Threading.DispatcherPriority.Background);
    }

'@
$s=$s.Substring(0,$openStart)+$info+$s.Substring($openEnd)
# The existing ClickSegment branch remains for accessibility and touch.
if(!$s.Contains('if(name=="Information")')){throw 'R28 click/touch fallback lost'}
if(!$s.Contains('informationOverlay?.IsOpen!=true && !ringGesture')){throw 'R28 submenu mouse leave guard lost'}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R28: hovering the Information wedge immediately opens its attached partial donut; click remains optional and gestures remain intact.'
