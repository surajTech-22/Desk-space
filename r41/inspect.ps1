$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace'
foreach($f in @('MainWindow.xaml.cs','WidgetHost.cs','DeskSpaceExcelV16.cs','DeskSpaceExcelBridgeV16.cs','DeskSpaceExcelClipboardV39.cs','App.xaml.cs')){
 if(!(Test-Path "$p/$f")){Write-Host "MISSING $f";continue}
 $l=[IO.File]::ReadAllLines("$p/$f")
 Write-Host ("=== R41 "+$f+" LENGTH "+$l.Length+" ===")
 for($i=0;$i -lt $l.Length;$i++)
 {
   if($l[$i] -match 'public (MainWindow|WidgetShellWindow)\(|InitializeComponent\(\)|private (void Capture\(|void LinkScreenshotToExcel\(|void CaptureSelection\(|void Run\(|static dynamic Excel\()|OpenExcel\(|SourceInitialized\+=')
   {
    for($j=[Math]::Max(0,$i-3);$j -le [Math]::Min($l.Length-1,$i+19);$j++){Write-Host ("{0}: {1}" -f ($j+1),$l[$j])}
   }
 }
}
