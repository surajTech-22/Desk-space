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
