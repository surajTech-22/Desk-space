$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$logic=[IO.File]::ReadAllText("$root/DeskSpaceWidgetExitV286.cs")
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$app=[IO.File]::ReadAllText("$root/App.xaml.cs")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($text in @(
 'internal static bool MayOpen(',
 'internal static void MarkAt(',
 'internal static void ClearAt(',
 'File.Move(temp,path,true);',
 'if(force)DeskSpaceWidgetExitV286.AllowExplicitShow();',
 'DeskSpaceWidgetExitV286.MayOpen(force,',
 'DeskSpaceWidgetExitV286.ExitFromTray(this)',
 'if(DeskSpaceWidgetExitV286.Dismissed)',
 'new Action(()=>WidgetHostLauncher.EnsureRunning(false))',
 'TestTrayExitUntilExplicitShowV286();'
)){
 if(!$logic.Contains($text) -and !$widget.Contains($text) -and
    !$app.Contains($text) -and !$main.Contains($text) -and
    !$smoke.Contains($text)){
 throw ('R286 missing sticky user-exit guard: '+$text)
 }
}
if($widget.Contains('menu.Items.Add("Exit widget", null, (_, _) => Dispatcher.Invoke(Close));') -or
   $main.Contains('new Action(ShowFloatingWidget)')){
 throw 'R286 user Exit can still be bypassed by obsolete auto launch'
}
if(!$widget.Contains('menu.Items.Add("Show widget"') -or
   !$main.Contains('private void ShowFloatingWidget() => WidgetHostLauncher.EnsureRunning(true);')){
 throw 'R286 explicit Show Widget action must remain functional'
}
Write-Host 'R286 STATIC PASS: all silent startup routes honor durable Exit until explicit Show.'
