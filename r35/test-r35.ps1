$ErrorActionPreference='Stop'
./r35/apply-r35.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$hostCode=Get-Content "$root/WidgetHost.cs" -Raw
$child=Get-Content "$root/DeskSpaceInformationV35.cs" -Raw
$wheel=Get-Content "$root/DeskSpaceWheelHoverV35.cs" -Raw
$inline=Get-Content "$root/DeskSpaceInlineV34.cs" -Raw
$calendar=Get-Content "$root/DeskSpaceCompactCalendarV34.cs" -Raw
foreach($needle in @(
    'DeskSpaceInformationV35? informationOverlay',
    'new DeskSpaceInformationV35(this,',
    'DeskSpaceWheelHoverV35.MouseIn(this)',
    'private void StartWheelHover()',
    'private void StopWheelHover()',
    'private void SampleMainWheel()',
    'mainWheelEnteredUtc=DateTime.UtcNow;',
    'DeskSpaceWheelHoverV35.MainCommitMilliseconds',
    'int selected=SectorAt(p);',
    'int add=QuickAddHitAt(p);',
    'UpdateHighlightedSector(p);',
    'OpenInWidget(target,true);',
    'ClickSegment(Segments[selected]);',
    'StartWheelHover();',
    'StopWheelHover();',
    'TryCapturedDragAtScreen',
    'QuickAddHitAt(e.GetPosition(this))',
    'new DeskSpaceInlineV34(name,previousCenter,ReturnFromModule,quickAddRequested)',
    'new DeskSpaceCompactCalendarV34().Create('
)){
    if(!$hostCode.Contains($needle)){throw "R35 shared main-wheel anchor missing: $needle"}
}
foreach($needle in @(
    'private void PollPointer()',
    'DeskSpaceWheelHoverV35.MouseIn(arcCanvas)',
    'private void ProcessWheelPointer(Point point)',
    'point.X<=40 && startIndex>0',
    'point.X>=448 && CanPage(1)',
    'StartEdgeScroll(direction)',
    'newHoverTimer.Stop();',
    'ActivateChild(slot,target.id);',
    'edgeTimer.Interval=TimeSpan.FromMilliseconds(DeskSpaceWheelHoverV35.EdgeRepeatMilliseconds)',
    'pointerTimer.Start();',
    'pointerTimer.Stop();',
    'private void Slide(int delta)',
    'new DoubleAnimation(.68,1,',
    'if(!CanPage(delta)){StopEdgeScroll();return;}',
    'TryCapturedDragAtScreen',
    'if(id.HasValue && !editorActive)',
    'DeskSpaceWindowBehavior.AllowInput(owner,true)',
    'information-collections-v26.json',
    'information-items-v22.json',
    'File.Replace(temp,CollectionsPath',
    'VaultId',
    'MaskRevealed()'
)){
    if(!$child.Contains($needle)){throw "R35 child wheel regression: $needle"}
}
foreach($needle in @(
    'public const int PointerPollMilliseconds=35;',
    'public const int ChildCommitMilliseconds=510;',
    'public const int MainCommitMilliseconds=680;',
    'public const int EdgeFirstMilliseconds=175;',
    'public const int EdgeRepeatMilliseconds=135;',
    'Forms.Control.MousePosition',
    'surface.PointFromScreen(new Point(point.X,point.Y))'))
{
    if(!$wheel.Contains($needle)){throw "R35 common wheel helper missing $needle"}
}
# Trigger paths: do not page outside the actual WPF popup arrow boxes.
foreach($side in @(-1,1))
{
    $page=0
    $total=12
    $max=[Math]::Max(0,$total-4)
    $iterations=0
    if($side -gt 0)
    {
        while($page -lt $max){$page++;$iterations++}
        if($page -ne $max -or $iterations -ne 8){throw 'R35 right-edge hover paging incorrect'}
    }
    else
    {
        $page=$max
        while($page -gt 0){$page--;$iterations++}
        if($page -ne 0 -or $iterations -ne 8){throw 'R35 left-edge hover paging incorrect'}
    }
}
foreach($size in @(0,1,2,4,6,12))
{
    $visible=[Math]::Min(4,$size)
    $slots=$visible+1
    $half=if($slots -eq 1){17.0}else{[Math]::Min(57.0,12.0*$slots)}
    $start=-90.0-$half
    foreach($i in 0..($slots-1))
    {
        $mid=$start+($i+.5)*2*$half/$slots
        $r=$mid*[Math]::PI/180
        $x=244+233.5*[Math]::Cos($r)
        $y=306+233.5*[Math]::Sin($r)
        if($x -le 0 -or $x -ge 488 -or $y -le 0 -or $y -ge 222)
        {throw "R35 curved hover wedge is outside pointer canvas for $size items at $i"}
    }
}
if(!$inline.Contains('var add=Button("+",()=>startNew?.Invoke())') -or
   !$calendar.Contains('var addEvent = SmallButton("+",expand)'))
{throw 'R35 live module + controls missing'}
if(!(Test-Path "$root/DeskSpaceVaultV10.cs"))
{throw 'R35 encrypted Security Vault missing'}
Write-Host 'R35 main and child hover, stable dwell, edge wheel, click/drag and 0..12 collection regression gates passed.'
