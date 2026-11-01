$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$app=[IO.File]::ReadAllText("$root/App.xaml.cs")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
foreach($token in @(
 'public static void EnsureRunning(bool force = false)',
 'menu.Items.Add("Exit widget", null, (_, _) => Dispatcher.Invoke(Close));',
 'menu.Items.Add("Show widget", null, (_, _) => Dispatcher.Invoke(Reveal));',
 'if (!force && !WidgetSettingsV3.Load().ShowAtLaunch) return;'
)){
 if(!$widget.Contains($token)){throw ('R286 Widget baseline drift: '+$token)}
}
if(!$app.Contains('if(e.Args.Contains("--widget-host"') -or
   !$main.Contains('new Action(ShowFloatingWidget)')){
 throw 'R286 startup/Workspace auto-show baseline drift'
}
Copy-Item 'r286/DeskSpaceWidgetExitV286.cs' "$root/DeskSpaceWidgetExitV286.cs" -Force
Copy-Item 'r286/WidgetHost_cs' "$root/WidgetHost.cs" -Force
Copy-Item 'r286/App_xaml_cs' "$root/App.xaml.cs" -Force
Copy-Item 'r286/MainWindow_xaml_cs' "$root/MainWindow.xaml.cs" -Force
Write-Host 'R286: tray Exit persists user intent; Windows Run and Workspace autoshow cannot reopen before explicit Show.'
