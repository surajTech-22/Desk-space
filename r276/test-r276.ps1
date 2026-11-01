$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$root/App.xaml.cs")
$startup=[IO.File]::ReadAllText("$root/DeskSpaceStartupRecoveryV183.cs")
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$fallback=[IO.File]::ReadAllText("$root/DeskSpaceStartupFallbackV276.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($must in @(
 'DeskSpaceStartupRecoveryV183.BeginEarly();',
 'var window=new WidgetShellWindow();',
 'widgetStarted=true;',
 'DeskSpaceStartupFallbackV276.Show(ex.GetType().Name);',
 'if(widgetStarted)DeskSpaceStartupRecoveryV183.MarkCleanExit();',
 'internal static void BeginEarly()',
 'internal static void MarkCleanExit()',
 'if(attached)return;',
 'BeginEarly();',
 'owner.Closed+=closed;',
 'if(!DeskSpaceStartupRecoveryV183.SafeMode)',
 'internal static Window Create(string reason)',
 'Forms.NotifyIcon?',
 'Content="Open DeskSpace Workspace"',
 'Content="Close recovery window"',
 'TestEarlyWidgetStartupRecoveryV276();'
)){
 if(!$app.Contains($must) -and !$startup.Contains($must) -and
    !$widget.Contains($must) -and !$fallback.Contains($must) -and
    !$native.Contains($must)){
  throw ('R276 missing early recovery guarantee: '+$must)
 }
}
if($app.IndexOf('DeskSpaceStartupRecoveryV183.BeginEarly();') -ge
   $app.IndexOf('var window=new WidgetShellWindow();')){
 throw 'R276 early marker must happen before widget constructor'
}
if($fallback.Contains('WorkspaceStore') -or
   $fallback.Contains('Directory.Delete(') -or
   $fallback.Contains('File.WriteAll')){
 throw 'R276 fallback must not modify users workspace or files'
}
Write-Host 'R276 STATIC PASS: preconstructor crash evidence, safe mode gate, explicit recovery tray and Workspace button.'
