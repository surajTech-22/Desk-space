$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($path in @(
  "$root/DeskSpaceMonitorDockV190.cs",
  "$root/DeskSpaceMonitorFenceV153.cs",
  "$root/DeskSpaceDropShelfV15.cs"
)){
 if(!(Test-Path -LiteralPath $path)){throw "Missing previously verified stable source: $path"}
}
# Scoped stable hotfix. Do not pull other R240-R298 development modules into
# the previously shipped R232+R233-R239 application.
$dock=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
if(!$dock.Contains('internal static void Attach(Window owner,Popup popup,Border border)') -or
   !$dock.Contains('DeskSpaceDeckOffsetV228.Offset shift=new(0,0);')){
 throw 'Unexpected Stable Deck baseline; manual review needed'
}
Copy-Item -LiteralPath 'r298/DeskSpaceMonitorDockV298.cs' -Destination "$root/DeskSpaceMonitorDockV190.cs" -Force
Copy-Item -LiteralPath 'r265/DeskSpaceDeckSaveGuardV265.cs' -Destination "$root/DeskSpaceDeckSaveGuardV265.cs" -Force
Copy-Item -LiteralPath 'r298/DeskSpaceMonitorFenceV298.cs' -Destination "$root/DeskSpaceMonitorFenceV153.cs" -Force
$path="$root/DeskSpaceDropShelfV15.cs"
$source=[IO.File]::ReadAllText($path)
function Replace-One([string]$find,[string]$new,[string]$label)
{
 $count=([regex]::Matches($script:source,[regex]::Escape($find))).Count
 if($count -ne 1){throw "R301 ${label}: expected one original anchor, found $count"}
 $script:source=$script:source.Replace($find,$new)
}
Replace-One 'HorizontalOffset=0,VerticalOffset=5,StaysOpen=true,' 'HorizontalOffset=0,VerticalOffset=0,StaysOpen=true,' 'popup initial vertical offset'
Replace-One 'DeskSpaceMonitorFenceV153.TrackPopup(host,popup);' @'
DeskSpaceMonitorFenceV153.TrackPopup(host,popup,
            (proposed,work)=>DeskSpaceMonitorDockV190.ConstrainBeforePaintV298(
                host,proposed,work));
'@ 'running apps popup native fence'
Replace-One 'popup.VerticalOffset=Math.Max(5,offset);' 'if(popup.VerticalOffset!=0)popup.VerticalOffset=0;' 'media popup vertical offset'
[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
$dock=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
$fence=[IO.File]::ReadAllText("$root/DeskSpaceMonitorFenceV153.cs")
foreach($key in @('FitBelowV292(', 'ConstrainBeforePaintV298(', 'NativeCorrectionV292(', 'AttachNativeObserverV295('))
{
 if(!$dock.Contains($key)){throw "R301 live native Deck fix not present: $key"}
}
if(!$fence.Contains('AdditionalConstraint(fit,work)') -or
   !$source.Contains('(proposed,work)=>DeskSpaceMonitorDockV190.ConstrainBeforePaintV298(')){
 throw 'R301 actual Running Apps Deck not connected to native pre-paint guard'
}
Write-Host 'R301 scoped Stable Deck hotfix: R298 placement and R295 recovery on previous Stable source; unchanged R233-R239 search, Excel, and updater code.'
