$ErrorActionPreference='Stop'
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
foreach($term in @('private void CollapseRing(','private void ExpandRing(','private void ShowRing(','private void ClickSegment(','private void OpenInformationArc(','OnMouseLeftButtonUp(','MouseLeftButtonUp +=','MouseLeave +=','BuildDonut();'))
{
    $at=$s.IndexOf($term,[StringComparison]::Ordinal)
    if($at -ge 0){
        $length=[Math]::Min(1900,$s.Length-$at)
        Write-Host "=== R27 INSPECT: $term ==="
        Write-Host $s.Substring($at,$length)
    }else{
        Write-Host "=== R27 INSPECT ABSENT: $term ==="
    }
}
