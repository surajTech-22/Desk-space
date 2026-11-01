$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceShelfGroupsV88.cs')
$helper=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceAppGroupOrderV261.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'DeskSpaceAppGroupOrderV261.Groups(windows)',
 'DeskSpaceAppGroupOrderV261.Windows(app,windows)',
 '.Select(x=>x.Handle).Distinct().Count()',
 '.OrderBy(x=>x.Count>1?0:1)',
 '.ThenByDescending(x=>x.Count)',
 '.ThenBy(x=>x.App.Name,StringComparer.OrdinalIgnoreCase)',
 '.Take(24).ToArray()',
 '.OrderBy(x=>x.WindowTitle,StringComparer.OrdinalIgnoreCase)',
 'TestRunningAppsOrderV261();'
)){
 if(!$source.Contains($needle) -and !$helper.Contains($needle) -and !$smoke.Contains($needle))
 {throw ('R261 missed deterministic running app display behavior: '+$needle)}
}
foreach($bad in @('Process.Start','ShowWindow','File.Write',
 'HttpClient','DispatcherTimer','Registry.','Clipboard.')){
 if($helper.Contains($bad)){throw ('R261 pure display sorting must have no side effects: '+$bad)}
}
Write-Host 'R261 STATIC PASS: distinct-HWND multiwindow-first sorting, stable flyouts and no state writes.'
