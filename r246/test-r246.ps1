$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceKeyboardHelpV246.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static IReadOnlyList<Shortcut> Supported()',
 'new Shortcut("Workspace","F1"',
 'new Shortcut("Workspace","Ctrl+K"',
 'new Shortcut("Search","F8"',
 'new Shortcut("Root donut","Home / End"',
 'new Shortcut("This guide","Escape"',
 'AutomationProperties.SetName(close',
 'if(e.Key==Key.Escape)',
 'Open.Remove(owner)',
 'DeskSpaceKeyboardHelpV246.Show(this);',
 'if(e.Key==Key.F1 && Keyboard.Modifiers==ModifierKeys.None)',
 'TestKeyboardShortcutsGuideV246();'
)){
 if(!$s.Contains($needle) -and !$hub.Contains($needle) -and !$t.Contains($needle))
 {throw ('R246 missing accessible keyboard guide binding: '+$needle)}
}
foreach($unsafe in @('RegisterHotKey','SetWindowsHookEx',
 'SendKeys','keybd_event','File.WriteAll','Clipboard.',
 'HttpClient','Process.Start')){
 if($s.Contains($unsafe)){throw ('R246 guide must not inject/record system keystrokes: '+$unsafe)}
}
Write-Host 'R246 STATIC PASS: F1 scoped Workspace help, verified keys, accessibility and no system hook.'
