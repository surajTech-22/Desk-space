$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
foreach($name in @('    private void CloseInformationContext(','    private void ToggleRing(','    private void OpenInformationArc(','    private void UpdateHighlightedSector(','    private void OnGestureMove(','    private void OnGestureUp(','    private void OnGestureDown(','    private void OpenInWidget(','    private void BuildDonut('))
{
  $start=$s.IndexOf($name,[StringComparison]::Ordinal)
  if($start -lt 0){Write-Host "R35 INSPECT $name MISSING";continue}
  $end=$s.IndexOf('    private ', $start+$name.Length,[StringComparison]::Ordinal)
  if($end -lt 0){$end=$s.Length}
  Write-Host "R35 INSPECT $name"
  Write-Host $s.Substring($start,[Math]::Min($end-$start,5300))
}
