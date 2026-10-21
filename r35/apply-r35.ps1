$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r35/DeskSpaceInformationV35.cs' "$root/DeskSpaceInformationV35.cs" -Force
Copy-Item 'r35/DeskSpaceWheelHoverV35.cs' "$root/DeskSpaceWheelHoverV35.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$before,[string]$after){
  $before=$before.Replace([string][char]13+[char]10,[string][char]10)
  $after=$after.Replace([string][char]13+[char]10,[string][char]10)
  if(!$script:s.Contains($before)){throw "R35 parent wheel integration missing anchor: $before"}
  $script:s=$script:s.Replace($before,$after)
}
Must 'DeskSpaceInformationV34? informationOverlay' 'DeskSpaceInformationV35? informationOverlay'
Must 'new DeskSpaceInformationV34(this,' 'new DeskSpaceInformationV35(this,'

# Every main ring module, including Notes, Reminders, Calendar and Tasks,
# receives the same non-click hover semantics. The separate Information arc
# uses the same DPI-aware cursor source with its own child hit regions.
$common=@'
    private DispatcherTimer? wheelHoverTimer;
    private int mainWheelSector=-1;
    private int mainWheelQuick=-1;
    private DateTime mainWheelEnteredUtc=DateTime.MinValue;

    private void StartWheelHover()
    {
        if(wheelHoverTimer==null)
        {
            wheelHoverTimer=new DispatcherTimer{
                Interval=TimeSpan.FromMilliseconds(
                    DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
            wheelHoverTimer.Tick+=(_,_)=>SampleMainWheel();
        }
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
        wheelHoverTimer.Start();
    }
    private void StopWheelHover()
    {
        wheelHoverTimer?.Stop();
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
    }
    private void SampleMainWheel()
    {
        if(!ringExpanded || activeModule!=null || hiddenAtEdge ||
            draggingGesture || shortcutMoveInProgress)return;
        if(ringGesture || Mouse.LeftButton==MouseButtonState.Pressed ||
            Mouse.RightButton==MouseButtonState.Pressed)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            return; // drag release still uses existing captured input path
        }
        Point p;
        try{p=DeskSpaceWheelHoverV35.MouseIn(this);}
        catch(InvalidOperationException){return;}
        int selected=SectorAt(p);
        int add=QuickAddHitAt(p);
        if(selected<0)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            return;
        }
        // Leaving Information and moving into any other main sector closes
        // its old arc and passive shelf. Never discard an active editor.
        if(selected!=6 && informationOverlay?.IsOpen==true &&
            informationOverlay.IsEditing==false)informationOverlay.Hide();
        UpdateHighlightedSector(p);
        if(selected==6)
        {
            mainWheelSector=selected;mainWheelQuick=-1;
            return; // Information opens its own child wheel immediately
        }
        if(selected!=mainWheelSector || add!=mainWheelQuick)
        {
            mainWheelSector=selected;
            mainWheelQuick=add;
            mainWheelEnteredUtc=DateTime.UtcNow;
            return;
        }
        if(!DeskSpaceWheelHoverV35.Dwelled(mainWheelEnteredUtc,
            DeskSpaceWheelHoverV35.MainCommitMilliseconds))return;
        // The pointer has been stable on this specific sector / + badge.
        // Commit once; a new ring expansion starts with a fresh timer.
        StopWheelHover();
        if(add>=0)
        {
            string target=Segments[add]=="Tasks"?"Reminders":Segments[add];
            if(target=="Clipboard")
                return; // capture into clipboard shelf still requires click
            OpenInWidget(target,true);
        }
        else
            ClickSegment(Segments[selected]);
    }

'@
$common=$common.Replace([string][char]13+[char]10,[string][char]10)
$start=$s.IndexOf('    private void ToggleRing()', [StringComparison]::Ordinal)
if($start -lt 0){throw 'R35 ToggleRing not found'}
$s=$s.Substring(0,$start)+$common+$s.Substring($start)

# Start/stop the same cursor polling on real donut lifecycle, not on
# transient popup transitions; idle compact circle gets zero polling.
$begin=$s.IndexOf('    private void ToggleRing()', [StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void CollapseRing()', $begin,[StringComparison]::Ordinal)}else{-1}
if($end -lt 0 -or $end -le $begin){
  $end=$s.IndexOf('    private void BuildDonut()', $begin,[StringComparison]::Ordinal)
}
if($begin -lt 0 -or $end -le $begin){throw 'R35 ToggleRing boundaries changed'}
$toggle=$s.Substring($begin,$end-$begin)
$old='        if (ringExpanded) BuildDonut(); else BuildCompact();'
$new=@'
        if (ringExpanded)
        {
            BuildDonut();
            StartWheelHover();
        }
        else
        {
            StopWheelHover();
            BuildCompact();
        }
'@
$new=$new.Replace([string][char]13+[char]10,[string][char]10)
if(!$toggle.Contains($old)){throw 'R35 ToggleRing drawable branch missing'}
$toggle=$toggle.Replace($old,$new)
$s=$s.Substring(0,$begin)+$toggle+$s.Substring($end)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R35: shared DPI-aware hover wheel samples main ring and Information subdonut at 35ms; stationary edge navigation, stable hover selection and existing click/drag supported.'
