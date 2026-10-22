$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$lines=[IO.File]::ReadAllLines($p)
foreach($scope in @(
 @{name='CLASS_INIT';start=150;end=300},
 @{name='BUILD_RING';start=1140;end=1295},
 @{name='CLICK_AND_OPEN';start=1350;end=1450}
)){
 Write-Host ('=== R39 '+$scope.name+' BEGIN ===')
 for($i=$scope.start-1;$i -lt [Math]::Min($scope.end,$lines.Length);$i++){
    Write-Host ('{0,4}: {1}' -f ($i+1),$lines[$i])
 }
 Write-Host ('=== R39 '+$scope.name+' END ===')
}
foreach($target in @('DeskSpaceUpdater.cs','DeskSpaceExcelV16.cs')){
 $src=[IO.File]::ReadAllLines("DeskSpace_Windows_R1/DeskSpace/$target")
 Write-Host ("=== R39 $target PROBES ===")
 for($i=0;$i -lt $src.Length;$i++){
   if($src[$i] -match 'StartAutomaticChecks|GetActiveObject|OpenScreenshot|Capture\(|Preview\(|Apply\(|SaveChecked|public void Enable'){
     $begin=[Math]::Max(0,$i-4);$end=[Math]::Min($src.Length-1,$i+15)
     for($j=$begin;$j -le $end;$j++){Write-Host ('{0,4}: {1}' -f ($j+1),$src[$j])}
   }
 }
}
Write-Host 'R39 source diagnostics finished'

$lines=[IO.File]::ReadAllLines('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
Write-Host '=== R39 RING HIT MATH BEGIN ==='
for($i=0;$i -lt $lines.Length;$i++)
{
 if($lines[$i] -match '(?<word>i<12|i\s*<\s*12|i>=6|i\s*>=\s*6|i%6|slot\*60|slot \* 60|360\s*/\s*6|Math.PI\s*/\s*6|Segments\[|SegmentIcons\[|ringPaths\[|SectorAt\(|QuickAddHitAt\(|ClickSegment\(|Excel)' -or $lines[$i] -match 'for\s*\(int i=0;i<12')
 {
   if($i -lt 175 -or ($i -gt 1050 -and $i -lt 1450) -or ($i -gt 330 -and $i -lt 820))
   {
     $a=[Math]::Max(0,$i-2);$b=[Math]::Min($lines.Length-1,$i+2)
     for($j=$a;$j -le $b;$j++){Write-Host ('{0,4}: {1}' -f ($j+1),$lines[$j])}
   }
 }
}
Write-Host '=== R39 RING HIT MATH END ==='
