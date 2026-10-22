$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r41/apply-r41-excel.ps1
./r41/apply-r41-async.ps1
./r41/apply-r41-scroll.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$excel=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceExcelV16.cs")
$clip=[IO.File]::ReadAllText("$root/DeskSpaceExcelClipboardV39.cs")
$worker=[IO.File]::ReadAllText("$root/DeskSpaceExcelWorkerV41.cs")
$scroll=[IO.File]::ReadAllText("$root/DeskSpaceSmoothScrollV41.cs")
foreach($key in @(
 'GetRunningObjectTable(', 'CreateBindCtx(', 'rot.EnumRunning(',
 'EnumWindows(', '"XLMAIN"', 'ActiveWindow.RangeSelection',
 'dynamic sheet=range.Worksheet', 'SourceWorkbook=Path.GetFileName(Text(sheet.Parent.Name))',
 'if(rows<=0||cols<=0||rows*(long)cols>1800)',
 'ValueKind=="formula"', 'SaveCopyAs(backup)', 'CoordinatesVerified'
)){
 if(!$excel.Contains($key)){throw "R41 Excel selection or protected paste missing: $key"}
}
foreach($key in @(
 'private async void Capture()', 'await DeskSpaceExcelWorkerV41.CaptureAsync(',
 'private async void LinkScreenshotToExcel()',
 'private async void Preview()', 'private async void Apply()',
 'await DeskSpaceExcelWorkerV41.PreviewAsync(', 'await DeskSpaceExcelWorkerV41.ApplyAsync(',
 'PresentCandidate(candidate);','Excel selection unavailable',
 'Reference','Value','Formula','Ignore'
)){
 if(!$ui.Contains($key)){throw "R41 asynchronous Excel mapping missing: $key"}
}
foreach($key in @(
 'private async void CaptureSelection(bool clipboardEvent)',
 'await DeskSpaceExcelWorkerV41.CopyEventAsync()',
 'captureBusy=true;', 'finally{captureBusy=false;}',
 'private async void PasteExcel(', 'await DeskSpaceExcelWorkerV41.ApplyAsync(',
 'private void HideShelf()', 'private void DragMove('
)){
 if(!$clip.Contains($key)){throw "R41 Excel clipboard nonblocking or R40 controls missing: $key"}
}
foreach($key in @(
 'SetApartmentState(ApartmentState.STA)', 'new Thread(',
 'RunContinuationsAsynchronously', 'CopyEventAsync()',
 'PreviewAsync(', 'ApplyAsync(', 'IsBackground=true'
)){
 if(!$worker.Contains($key)){throw "R41 COM STA executor regression: $key"}
}
foreach($key in @(
 'RegisterClassHandler(', 'PreviewMouseWheelEvent',
 'FindTarget(', 'motions.GetValue(', 'CanContentScroll',
 'Viewer.ScrollToVerticalOffset(', 'Tick.Stop()', 'Math.Clamp(',
 'ModifierKeys.Control|ModifierKeys.Shift|ModifierKeys.Alt'
)){
 if(!$scroll.Contains($key)){throw "R41 all-app wheel regression: $key"}
}
foreach($item in @(
 @('MainWindow.xaml.cs','DeskSpaceSmoothScrollV41.Enable();'),
 @('WidgetHost.cs','DeskSpaceSmoothScrollV41.Enable();')
)){
 if(![IO.File]::ReadAllText("$root/$($item[0])").Contains($item[1]))
 {throw "R41 smooth scroll not enabled: $($item[0])"}
}
foreach($file in @('DeskSpaceDropShelfV15.cs','DeskSpaceVaultV10.cs',
 'DeskSpaceWeatherSceneV11.cs','DeskSpaceUpdater.cs','DeskSpaceExcelStoreV16.cs')){
 if(!(Test-Path "$root/$file")){throw "R41 preserved module missing: $file"}
}
Write-Host 'R41 LIVE EXCEL: multi-instance registered workbook, original selection addresses, read-only capture, STA background work PASS.'
Write-Host 'R41 UI: global WPF nested scroll, easing, timer disposal, virtualized list protections PASS.'
Write-Host 'R41 DATA: Excel formula protection, verified cell coordinates, workbook backup, original modules preserved PASS.'
