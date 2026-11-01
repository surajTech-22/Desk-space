$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$w=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
if($w.Contains('if(IsNearScreenEdge())DockToEdge(manual:true)') -or
   $w.Contains('private bool IsNearScreenEdge()')){
 throw 'R231 implicit monitor seam attraction still exists'
}
if($w.Contains('Left=prefs.Left??Left;Top=prefs.Top??Top;')){
 throw 'R231 secondary Reveal still restores the primary widget position'
}
foreach($line in @(
 'KeepOnVisibleDesktop();',
 'SavePosition();',
 'edgeHandle.Hide();',
 'DeskSpaceMonitorFenceV152.BoundScreen(this) ??',
 'Forms.Screen.FromHandle(hwnd)).WorkingArea;',
 'DeskSpaceMonitorFenceV152.ClampNow(this);',
 'menu.Items.Add("Hide at screen edge"'
)){
 if(!$w.Contains($line)){throw ('R231 monitor isolation behavior missing '+$line)}
}
if($w.Contains('var work=SystemParameters.WorkArea;')){
 throw 'R231 Information expansion still clamps secondary to primary WorkArea'
}
$drag=$w.IndexOf('private void StartShortcutDrag()')
$dragEnd=$w.IndexOf('private void OnGestureDown(', $drag)
if($drag -lt 0 -or $dragEnd -le $drag){throw 'R231 drag behavior missing'}
$section=$w.Substring($drag,$dragEnd-$drag)
if($section.Contains('DockToEdge(') -or !$section.Contains('SavePosition();')){
 throw 'R231 drop must save position, never snap to screen seam'
}
Write-Host 'R231 STATIC PASS: no seam magnet, no primary-position Restore, owned monitor dock and Information fence.'
