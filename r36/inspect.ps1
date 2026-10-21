$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
foreach($term in @('    private void BuildCompact(','    private void UpdateCenter(','    private void OnWeatherUpdated(','    private void OpenInWidget(','    private void ClickSegment(','    private void OpenCalendarExpanded(','    private void ReturnFromModule(','    private void ResumeMediaMode(','    private void BuildDonut(','    private void ToggleRing(')){
 $a=$s.IndexOf($term,[StringComparison]::Ordinal)
 if($a -lt 0){Write-Host "=== ABSENT $term ===";continue}
 $b=$s.IndexOf('    private ', $a+$term.Length,[StringComparison]::Ordinal)
 if($b -lt 0){$b=$s.Length}
 Write-Host "=== METHOD $term ==="
 Write-Host $s.Substring($a,[Math]::Min(8500,$b-$a))
}
Write-Host '=== MEDIA/WEATHER/CALC LINES ==='
$s.Split([string][char]10) | Where-Object {
 $_ -match 'DeskSpaceWeatherScene|content.Foreground|content.Text|media|Media|Calculator|Clock|content.FontSize|new Stopwatch|Weather' -and $_ -notmatch 'comment'
} | Select-Object -First 125 | ForEach-Object {Write-Host $_}
