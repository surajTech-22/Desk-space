$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDueShelfV74.cs'
$src=[IO.File]::ReadAllText($path)
$hostCode=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
$renderer=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSubDonutStandardV62.cs')
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal sealed class DeskSpaceDueShelfV74 : IDisposable',
 'workspace.Reminders',
 'x.LastAlert==null && x.Due<=localSystemTime',
 'item.LastAlert=now;',
 'store.Save(workspace);',
 'SystemSounds.Exclamation.Play();',
 'owner.LocationChanged+=Moved;',
 'internal static Rect PlaceAtCircle(',
 'ShowInTaskbar=false,',
 'ShowActivated=false,Topmost=true',
 'if(!force && monotonic-lastScan<2000)return;',
 'Preview(awaiting,DateTime.Now,Complete,Dismiss)',
 'public void Dispose()'
)){if(!$src.Contains($token)){throw "R74 due shelf missing: $token"}}
foreach($token in @('dueShelf=new DeskSpaceDueShelfV74(this);',
 'Loaded+=(_,_)=>dueShelf?.Start();',
 'Closing+=(_,_)=>{dueShelf?.Dispose();dueShelf=null;}')){
 if(!$hostCode.Contains($token)){throw "R74 widget integration missing $token"}
}
if($renderer.Contains('whole.Children.Add(Segment(outer,topEnd));') -or
   $renderer.Contains('whole.Children.Add(Segment(lowerStart,inner));')){
 throw 'R74 duplicate decorative line segments still remain'}
foreach($token in @(
 'R74: hidden count must NOT add extra divider lines',
 'R74 due scheduling uses Windows local system time',
 'R74 due shelf stays directly below circle',
 'subdonut-r74-no-extra-lines',
 'r74-due-shelf')){
 if(!$tests.Contains($token)){throw "R74 WPF acceptance missing: $token"}
}
Write-Host 'R74 PASS: original divider only; time-driven compact owned shelf and alarm sound.'
