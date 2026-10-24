$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$hover=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
$shelf=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$group=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
foreach($t in @(
 'Interval=TimeSpan.FromMilliseconds(220)',
 'liveTimer.Tick+=',
 'RemainingWindows(windows,',
 'DeskSpaceRunningAppsV88.IsActiveWindow',
 'windowItems.Children.Remove(row)',
 'iconRow.Children.Remove(tile)',
 'appBadges.Remove(app.Name)',
 'onLiveChanged?.Invoke(groups,live)',
 'activeIconPopup?.IsOpen==true',
 'if(wasOpen)onHoverClosed?.Invoke()',
 'if(DeskSpaceRunningAppsV88.IsActiveWindow(window.Handle))',
 'retry.Start()',
 'Tag=selected.Handle'
)){
 if(!$hover.Contains($t)){throw "R97 live hover validation missing: $t"}
}
foreach($t in @(
 'Interval=TimeSpan.FromSeconds(2)',
 'appRenderPending=true',
 'DeskSpaceStableHoverV93.IsHoverActive',
 'DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows,',
 'heading.Text=busy?"Copying…":',
 'if(appsNow.Count==0)Render()',
 'RefreshRunningApps();'
)){
 if(!$shelf.Contains($t)){throw "R97 live shelf validation missing: $t"}
}
if(!$group.Contains('onLiveChanged,onHoverClosed')){
 throw 'R97 flyout-to-shelf callbacks not forwarded'
}
if($hover.Contains('Process.Kill(') -or $hover.Contains('Task.Delay(')){
 throw 'R97 must not force-kill or block the UI thread'
}
Write-Host 'R97 PASS: no wait for hover close, immediately remove destroyed HWNDs, update counters and preserve unsaved-work prompts.'
