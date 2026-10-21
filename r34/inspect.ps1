$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p)
foreach($start in @('    private void CollapseRing(','    private void ToggleRing(','    private void BuildDonut(','    private void ClickSegment(','    private void OpenInWidget(','    private void OpenInformationArc(','    private void UpdateHighlightedSector(','    private int SectorAt(','    private void OnGestureUp('))
{
    $a=$s.IndexOf($start,[StringComparison]::Ordinal)
    if($a -lt 0){Write-Host "=== $start not found ===";continue}
    $b=$s.IndexOf('    private ', $a+$start.Length,[StringComparison]::Ordinal)
    if($b -lt 0){$b=$s.Length}
    Write-Host "=== $start ==="
    Write-Host $s.Substring($a,[Math]::Min($b-$a,9000))
}
Write-Host '=== Parent donut field/module bindings ==='
$s.Split([string][char]10) | Where-Object {
    $_ -match 'Segments\s*=|private static readonly string\[\]|ringExpanded|new TextBlock|new Path' -and $_ -notmatch 'Medi'
} | Select-Object -First 50 | ForEach-Object { Write-Host $_ }
