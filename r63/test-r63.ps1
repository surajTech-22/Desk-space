$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$middle=Get-Content "$root/DeskSpaceMiddleScrollV63.cs" -Raw
$info=Get-Content "$root/DeskSpaceInformationV36.cs" -Raw
$apps=Get-Content "$root/DeskSpaceApplicationsV55.cs" -Raw
$excel=Get-Content "$root/DeskSpaceExcelWheelV42.cs" -Raw
$smoke=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($t in @('ChangedButton==MouseButton.Middle','IsActive',
    'PreviewMouseDown+=OnMouseDown','PreviewKeyDown+=OnKeyDown',
    'MouseIn(surface)','Math.Abs(dy)>=18','Math.Abs(dx)>=34',
    'timer.Interval=TimeSpan.FromMilliseconds','timer.Tick+=OnTick',
    'internal void Pulse(Point pointer)','internal void Stop()',
    'public void Dispose()')){
 if(!$middle.Contains($t)){throw "R63 shared middle-button autoscroll missing: $t"}
}
foreach($t in @('if(shelf.IsOpen && !editorActive)CloseShelf();',
    'middleScroll.Stop();','middleScroll.Dispose();',
    'middleScroll=new DeskSpaceMiddleScrollV63.Controller',
    'Only mouse click, explicit drag-release or Enter can')){
 if(!$info.Contains($t)){throw "R63 information preview gate missing: $t"}
}
$hoverBegin=$info.IndexOf('    private void HoverChild(')
$hoverEnd=$info.IndexOf('    private void EndChildHover(',[Math]::Max(0,$hoverBegin))
if($hoverBegin -lt 0 -or $hoverEnd -le $hoverBegin){
 throw 'R63 Information HoverChild boundaries not found'
}
$hover=$info.Substring($hoverBegin,$hoverEnd-$hoverBegin)
if($hover.Contains('OpenCollection(') -or $hover.Contains('newHoverTimer.Start()') -or
   $hover.Contains('ActivateChild(')){
 throw 'R63 HoverChild can still open/commit unselected information previews'
}
foreach($t in @('middleScroll=new DeskSpaceMiddleScrollV63.Controller',
    'ring.MiddleScroll=new DeskSpaceMiddleScrollV63.Controller',
    'ring.MiddleScroll?.Dispose()','middleScroll.Dispose()',
    'middleScroll.Stop();')){
 if(!$excel.Contains($t)){throw "R63 Excel root or nested scroll missing: $t"}
}
foreach($t in @('middleScroll=new DeskSpaceMiddleScrollV63.Controller',
    'middleScroll.Stop();','middleScroll.Dispose();')){
 if(!$apps.Contains($t)){throw "R63 Applications scroll missing: $t"}
}
foreach($t in @('TestMiddleButtonSubDonutScroll()',
    'R63 WPF PASS: middle-click scroll activation')){
 if(!$smoke.Contains($t)){throw "R63 Windows smoke missing: $t"}
}
Write-Host 'R63 PASS: Information only previews after explicit selection and middle-button autoscroll is shared across every nested donut level.'
