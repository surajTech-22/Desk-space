$ErrorActionPreference='Stop'
$host='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$info='DeskSpace_Windows_R1/DeskSpace/DeskSpaceInformationV36.cs'
function PrintMatches([string]$path,[string]$pattern,[int]$before=4,[int]$after=8,[int]$max=32)
{
 $lines=[IO.File]::ReadAllLines($path);$hits=@()
 for($i=0;$i -lt $lines.Length;$i++){
   if($lines[$i] -match $pattern){$hits+=,$i}
 }
 Write-Host "R158_SOURCE: $path pattern=$pattern totalHits=$($hits.Count)"
 foreach($i in ($hits | Select-Object -First $max)){
   Write-Host "--- match line $($i+1) ---"
   for($j=[Math]::Max(0,$i-$before);$j -le [Math]::Min($lines.Length-1,$i+$after);$j++){
     Write-Host ('{0,5} {1}' -f ($j+1),$lines[$j])
   }
 }
}
PrintMatches $host 'RightButton|MouseRight|RightClick|MouseButtons|mouseRight|MouseDown|MouseUp|MouseIn|PointerPoll|ToggleRing|MouseCapture' 4 8 35
PrintMatches $info 'RenderArc\(|DrawHiddenCounters|countView=|ChildSector\(|arcCanvas.Width|arcCanvas.Height|PositionArc\(' 6 12 22
Write-Host 'R158_SOURCE inspection finished'
