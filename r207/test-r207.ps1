$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$model=[IO.File]::ReadAllText("$root/DeskSpaceDonutKeyboardV207.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'Navigate donut with keyboard (arrows + Enter)',
 'PreviewKeyDown+=KeyboardDonutKeyV207;',
 'Deactivated+=(_,_)=>{',
 'private bool keyboardDonutModeV207;',
 'private int keyboardDonutSectorV207=-1;',
 'DeskSpaceWindowBehavior.AllowInput(this,true);',
 'DeskSpaceWindowBehavior.AllowInput(this,false);',
 'System.Windows.Input.Keyboard.Focus(this)',
 'System.Windows.Automation.AutomationProperties.SetLiveSetting(',
 'System.Windows.Automation.AutomationProperties.SetName(content,',
 'DeskSpaceDonutKeyboardV207.Next(',
 'ClickSegment(Segments[index]);',
 'EndKeyboardDonutV207();',
 '&& !keyboardDonutModeV207'
)){
 if(!$hostSource.Contains($needle)){throw ("R207 host keyboard gate missing "+$needle)}
}
foreach($needle in @(
 'internal const int RootSectors=12;',
 'internal const int SectorsPerRing=6;',
 'Key.Left =>',
 'Key.Right =>',
 'Key.Up =>',
 'Key.Down =>',
 'Key.Home => 0,',
 'Key.End => RootSectors-1'
)){
 if(!$model.Contains($needle)){throw ("R207 keyboard routing missing "+$needle)}
}
if(!$smoke.Contains('TestKeyboardDonutV207();')){
 throw 'R207 native keyboard acceptance not installed'
}
if($hostSource.Contains('RegisterHotKey(handle,HotkeyId,Control|Alt|NoRepeat,Space)')){
 # Existing universal Search global shortcut remains untouched.
 Write-Host 'R207: existing Search shortcut preserved in its own component.'
}
if($model.Contains('Process.Start(') -or
   $model.Contains('SendKeys(') -or $model.Contains('PostMessage(') -or
   $model.Contains('static int keyboardDonutSector')){
 throw 'R207 keyboard mode must be local to widget, never global input injection'
}
Write-Host 'R207 STATIC PASS: scoped root keyboard controls and screen reader labels without global shortcuts.'
