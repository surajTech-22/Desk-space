$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function ReplaceMethod([string]$from,[string]$to,[string]$replacement) {
    $a=$script:s.IndexOf($from,[StringComparison]::Ordinal)
    $b=if($a -ge 0){$script:s.IndexOf($to,$a,[StringComparison]::Ordinal)}else{-1}
    if($a -lt 0 -or $b -le $a){throw "R29 missing source method: $from"}
    $script:s=$script:s.Substring(0,$a)+$replacement+[Environment]::NewLine+$script:s.Substring($b)
}
$start=$s.IndexOf('    private void UpdateHighlightedSector(',[StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    private void BuildCompact()', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R29 hover selector missing'}
$old=$s.Substring($start,$end-$start)
foreach($anchor in @('SectorAt(p)','selected==6 && ringExpanded','OpenInformationArc();','ringPaths.Count'))
{if(!$old.Contains($anchor)){throw "R29 hover source safety mismatch: $anchor"}}
$selector=@'
    private void UpdateHighlightedSector(Point p)
    {
        int selected=SectorAt(p);
        // Exactly one contextual parent is active at any time. Moving into
        // ANY other real module sector immediately closes Information's
        // child donut and lower shelf. A separator (-1) does not close it:
        // users must cross the small blank gap to reach the child arc.
        if(selected>=0 && selected!=6)
            CloseInformationContext();
        else if(selected==6 && ringExpanded && activeModule==null)
            OpenInformationArc();

        if(selected==highlightedSector)return;
        highlightedSector=selected;
        for(int i=0;i<ringPaths.Count;i++)
            ringPaths[i].Fill=i==selected?accent:
                (i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)));
    }

'@
$s=$s.Substring(0,$start)+$selector+$s.Substring($end)

$begin=$s.IndexOf('    private bool informationArcOpening;', [StringComparison]::Ordinal)
$finish=if($begin -ge 0){$s.IndexOf('    private void ClickSegment(string name)', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $finish -le $begin){throw 'R29 Information lifecycle boundaries missing'}
$old=$s.Substring($begin,$finish-$begin)
foreach($anchor in @('informationArcOpening','informationOverlay.Show()',
 'Dispatcher.BeginInvoke','if(!ringExpanded)ToggleRing()','informationOverlay?.IsOpen==true'))
{if(!$old.Contains($anchor)){throw "R29 arc lifecycle changed unexpectedly: $anchor"}}
$new=@'
    private bool informationArcOpening;
    private int informationArcVersion;

    private void CloseInformationContext()
    {
        // Also cancel any deferred opening from an earlier press/release.
        // Otherwise the old submenu reappears after another module is hovered.
        if(informationOverlay?.IsOpen!=true && !informationArcOpening)return;
        informationArcVersion++;
        informationArcOpening=false;
        if(informationOverlay?.IsOpen==true)
            informationOverlay.Hide();
    }

    private void OpenInformationArc()
    {
        if(activeModule!=null ||
           informationOverlay?.IsOpen==true ||
           informationArcOpening)return;
        informationArcOpening=true;
        int version=++informationArcVersion;
        void ShowAttachedArc()
        {
            // Stale dispatcher operations must not reopen a submenu after the
            // pointer has already moved to Notes, Calendar, Search, etc.
            if(version!=informationArcVersion)return;
            informationArcOpening=false;
            if(activeModule!=null || informationOverlay?.IsOpen==true)return;
            if(!ringExpanded)ToggleRing();
            if(!ringExpanded)return;
            var work=SystemParameters.WorkArea;
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
        // Hover opens instantly; only pressed gestures need deferred opening.
        if(ringExpanded &&
           Mouse.LeftButton!=MouseButtonState.Pressed &&
           Mouse.RightButton!=MouseButtonState.Pressed)
            ShowAttachedArc();
        else
            Dispatcher.BeginInvoke(new Action(ShowAttachedArc),
                System.Windows.Threading.DispatcherPriority.Background);
    }

'@
$s=$s.Substring(0,$begin)+$new+$s.Substring($finish)
$clickStart=$s.IndexOf('    private void ClickSegment(string name)',[StringComparison]::Ordinal)
$clickEnd=if($clickStart -ge 0){$s.IndexOf('    private void ApplySettings()', $clickStart,[StringComparison]::Ordinal)}else{-1}
if($clickStart -lt 0 -or $clickEnd -le $clickStart){throw 'R29 click handler missing'}
$click=$s.Substring($clickStart,$clickEnd-$clickStart)
$oldClick='        informationOverlay?.Hide();'
if(!$click.Contains($oldClick)){throw 'R29 click cleanup anchor missing'}
$click=$click.Replace($oldClick,'        CloseInformationContext();')
$s=$s.Substring(0,$clickStart)+$click+$s.Substring($clickEnd)

# R27 parent-collapse cleanup additionally invalidates any scheduled hover
# open. A submenu that has not rendered yet must not resurrect after collapse.
$oldToggle='        if(ringExpanded && informationOverlay?.IsOpen==true)informationOverlay.Hide();'
$newToggle=@'
        if(ringExpanded)
            CloseInformationContext();
'@
if(!$s.Contains($oldToggle)){throw 'R29 parent collapse cleanup anchor missing'}
$s=$s.Replace($oldToggle,$newToggle)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R29: hover other main-module sector closes child donut immediately; child-arc gap stays navigable; stale queued opens are cancelled.'
