$ErrorActionPreference='Stop'
$path='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$lines=[IO.File]::ReadAllLines($path)
Write-Host 'R160_LAYOUT_INSPECTION_BEGIN'
foreach($pattern in @('Content\s*=','BuildCompact\(','BuildDonut\(','new Grid','new Canvas','mainGrid','rootGrid','root.Children.Add','rootCanvas','ClipToBounds','renderRoot','panel.Children.Add')){
  $hits=@()
  for($i=0;$i -lt $lines.Length;$i++){ if($lines[$i] -match $pattern){$hits+=,$i}}
  Write-Host "PATTERN=$pattern HITS=$($hits.Count)"
  foreach($i in ($hits|Select-Object -First 8)){
    $first=[Math]::Max(0,$i-3);$last=[Math]::Min($lines.Length-1,$i+5)
    Write-Host "--- $($i+1) ---"
    for($j=$first;$j -le $last;$j++){Write-Host ('{0}: {1}' -f ($j+1),$lines[$j])}
  }
}
Write-Host 'R160_LAYOUT_INSPECTION_END'
