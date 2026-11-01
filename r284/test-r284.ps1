$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$design=[IO.File]::ReadAllText("$root/DeskSpaceDesignV283.cs")
$panel=[IO.File]::ReadAllText("$root/WidgetPanels.cs")
$smart=[IO.File]::ReadAllText("$root/DeskSpaceSmartContextDeckV280.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($must in @(
 'Property=UIElement.IsKeyboardFocusedProperty',
 'Property=UIElement.IsMouseOverProperty',
 'template.Triggers.Add(keyboard);',
 'DeskSpaceDesignV283.ApplyWindow(this,DeskSpaceDesignV283.Current);',
 'Content=new ScrollViewer{Content=panel,',
 'DeskSpaceDesignV283.Selection(pins);',
 'DeskSpaceDesignV283.Button(save,true);',
 'DeskSpaceDesignV283.Button(toggle);',
 'DeskSpaceDesignV283.Card(content)',
 'DeskSpaceDesignV283.Button(button);',
 'DeskSpaceDesignV283.Gradient(',
 'TestModernDialogAccessibilityV284();',
 'R284 small-height widget options must scroll'
)){
 if(!$design.Contains($must) -and !$panel.Contains($must) -and
    !$smart.Contains($must) -and !$hover.Contains($must) -and
    !$native.Contains($must)){throw ('R284 modern dialog safety missing: '+$must)}
}
if(!$panel.Contains('prefs.PinnedApps.Remove(s)') -or
   !$panel.Contains('prefs.Save()') -or
   !$smart.Contains('if(state.Listening)Stop();') -or
   !$hover.Contains('DeskSpaceMonitorFenceV153.TrackPopup(')){
 throw 'R284 cannot lose existing pin actions, opt-in Stop or hover popup fencing'
}
Write-Host 'R284 STATIC PASS: advanced dialog redesign retains native controls, privacy and monitor-local flyout.'
