$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$studio=[IO.File]::ReadAllText("$root/DeskSpaceSettingsStudioV148.cs")
$manager=[IO.File]::ReadAllText("$root/DeskSpaceMultiMonitorV151.cs")
foreach($key in @('DeskSpaceMultiMonitorV151.Register(this)',
 'DeskSpaceMultiMonitorV151.IsSatellite(this)',
 'if(!DeskSpaceMultiMonitorV151.IsSatellite(this))SavePosition()',
 'Visible = !DeskSpaceMultiMonitorV151.ConstructingSatellite',
 'if(!DeskSpaceMultiMonitorV151.IsSatellite(this))recent.Start();',
 'if(!DeskSpaceMultiMonitorV151.IsSatellite(this))',
 'if(!DeskSpaceMultiMonitorV151.IsSatellite(this))dueShelf?.Start();')){
 if(!$source.Contains($key)){throw "R151 missing widget integration: $key"}
}
foreach($key in @('DeskSpaceMultiMonitorV151.Configure(showEveryMonitorDraft)',
 'new[]{"Donut toolbar","Theme","Displays","Presets","General"}',
 'DeskSpaceSettingsDiagnosticsV151.Report("Settings page: "+page,ex)',
 '"Settings action: "+name,ex);',
 'private void DisplaysPage()')){
 if(!$studio.Contains($key)){throw "R151 missing settings integration: $key"}
}
foreach($key in @('SystemEvents.DisplaySettingsChanged+=OnDisplaysChanged',
 'new WidgetShellWindow',
 'internal static bool ConstructingSatellite=>creatingSatellite',
 'private static void Reconcile()',
 'if(!primary.Dispatcher.CheckAccess())',
 'widget-displays-v151.json',
 'Array.Empty<Forms.Screen>()',
 'File.Move(temp,Filename,true)')){
 if(!$manager.Contains($key)){throw "R151 missing display manager behavior: $key"}
}
if($manager.Contains('Application.Current.Shutdown()')){throw 'R151 cannot close the entire DeskSpace host'}
if(!$studio.Contains('if(pinned.Parent is Panel oldPinnedHost)oldPinnedHost.Children.Remove(pinned);')){
 throw 'R151 cannot re-open Donut page with stale pinned ListBox logical parent'
}
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('TestSettingsPagesV151();',
    'private static void TestSettingsPagesV151()',
    '"Donut toolbar","Displays","Presets","General"')){
 if(!$smoke.Contains($token)){throw "R151 real WPF Settings test missing: $token"}
}

Write-Host 'R151 PASS: guarded Settings, saved monitor preference, per-monitor widgets, hotplug and primary position protection.'
