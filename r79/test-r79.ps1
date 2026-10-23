$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hostCode=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$widget=[IO.File]::ReadAllText("$root/DeskSpaceInlineV36.cs")
$mainXaml=[IO.File]::ReadAllText("$root/MainWindow.xaml")
$mainCode=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
if(!$hostCode.Contains('activeInline=new DeskSpaceInlineV36(')){
 throw 'R79 live WidgetHost no longer points to tested V36 source'
}
foreach($t in @('REMINDERS · SOUND & ACTIONS',
    'Sound / Tune','Test sound','Browse WAV',
    'Perform action when due (optional)','Perform action','Browse target',
    'DeskSpaceReminderTimeV78.TryParse',
    'Where(x=>!x.Done)','startNew=()=>')){
 if(!$widget.Contains($t)){throw "R79 widget active editor missing $t"}
}
if($widget.Contains('var time=Field("Time, HH:mm");')){
 throw 'R79 active V36 widget still contains old time-only editor'
}
foreach($t in @('ReminderSoundChoice','ReminderSoundPath',
    'ReminderActionChoice','ReminderActionPath','ReminderTimePreview',
    'ReminderTestSound_Click','ReminderBrowseWav_Click',
    'ReminderBrowseTarget_Click','ReminderChooseAction_Click',
    'Text="09:00:00"')){
 if(!$mainXaml.Contains($t)){throw "R79 main XAML missing $t"}
}
foreach($t in @('activeReminders.Filter=item=>item is ReminderItem reminder && !reminder.Done',
 'DeskSpaceReminderTimeV78.TryParse',
 'DeskSpaceDueShelfV74.PlaySound',
 'DeskSpaceDueShelfV74.IsAllowedAction',
 'item.Done=true;',
 'ReminderTime_TextChanged')){
 if(!$mainCode.Contains($t)){throw "R79 main handler missing $t"}
}
foreach($t in @('TestRealReminderEditorsV79();',
 'r79-actual-widget-reminders','r79-actual-main-workspace-reminders',
 'R79 actual widget reminder V36 still uses old editor',
 'R79 main Workspace still has legacy Reminder form',
 'R79 main Workspace must hide completed reminders')){
 if(!$tests.Contains($t)){throw "R79 independent WPF runtime test missing $t"}
}
Write-Host 'R79 preflight PASS: both actual reminder entry-points updated, both WPF screens must render before release.'
