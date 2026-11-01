$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$apps=[IO.File]::ReadAllText("$root/DeskSpaceShelfGroupsV88.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$dock=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal static IReadOnlyList<Entry> DistinctLabelsV282(',
 'if(identities.Length<=1)continue;',
 'return DistinctLabelsV282(windows.GroupBy(x=>x.Handle)',
 'internal static bool UntitledTaskbarWindowV282(',
 '(extendedStyle&WsExAppWindow)!=0',
 'internal int ProcessId {get;init;}',
 'internal string WindowClass {get;init;}',
 'internal static bool CanControlV282(',
 'private static bool IsSameLiveWindowV282(',
 'DeskSpaceRunningAppsV88.DisplayTitleV282(selected)',
 'internal static Rectangle AvoidWidgetV282(',
 'internal static double ScrollHeightV282(',
 'owner.LocationChanged+=(_,_)=>{Refresh();RepairLater();};',
 'if(safe.Location!=current.Location)',
 'TestRunningAppCountAndDeckClearanceV282();',
 'R282 two distinct executables with same friendly name',
 'R282 a Deck painted onto the clock must move fully outside'
)){
 if(!$apps.Contains($token) -and !$hover.Contains($token) -and
    !$dock.Contains($token) -and !$tests.Contains($token)){
  throw ('R282 count/Deck safety not present: '+$token)
 }
}
# R289 constrains the viewport strictly to room below its own clock.
if($dock.Contains('CalculateBelowV289('))
{
 if(!$dock.Contains('int availableBelow=Math.Max(1,bounds.Bottom-widgetRect.Bottom-22);') -or
    !$dock.Contains('scroll.MaxHeight=Math.Max(12,Math.Min(')){
  throw 'R289 below-only Deck lost its monitor-bound scrolling viewport'
 }
}
elseif(!$dock.Contains('scroll.MaxHeight=Math.Min(Math.Min(520,h),'))
{
 throw 'R282 legacy scroll viewport missing'
}

foreach($bad in @('Process.Kill(','Clipboard.SetText(')){
 if($apps.Contains($bad) -or $dock.Contains($bad)){
  throw ('R282 must not terminate running apps or modify clipboard: '+$bad)
 }
}
Write-Host 'R282 STATIC PASS: no fake count, safe real app identity and monitor-local non-overlapping Deck.'
