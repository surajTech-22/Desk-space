$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
foreach($needle in @('    private void OnGestureDown(','    private void OnGestureMove(','    private void OnGestureUp(','    private void OnGestureCancel(','    private void OpenInformationArc(','    private void ToggleRing(','    private void ClickSegment(','    private void OnGestureLeave(',
    '    public WidgetHost(', '    private void ReturnFromModule('))
{
    $i=$s.IndexOf($needle,[StringComparison]::Ordinal)
    if($i -ge 0){
      $next=$s.IndexOf([string][char]10+'    private ', $i+$needle.Length,[StringComparison]::Ordinal)
      if($next -lt 0 -or $next-$i -gt 8500){$next=[Math]::Min($s.Length,$i+8500)}
      Write-Host ("=== INSPECT "+$needle+" ===")
      Write-Host $s.Substring($i,$next-$i)
    }
}
foreach($needle in @('AllowsTransparency','ShowActivated','ShowInTaskbar','Topmost','NoActivate','AllowInput','ReleaseMouseCapture','CaptureMouse','Mouse.Capture',
 'DeskSpaceWindowBehavior','PreviewMouseLeftButtonUp +=','PreviewMouseLeftButtonDown +=','MouseMove +=','MouseLeave +='))
{
    Write-Host ("=== LINES "+$needle+" ===")
    $lines=$s.Split([string][char]10)
    for($i=0;$i -lt $lines.Length;$i++){if($lines[$i].Contains($needle)){
      Write-Host (($i+1).ToString()+': '+$lines[$i])}}
}
