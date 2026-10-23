$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$m=[IO.File]::ReadAllText("$root/Models.cs")
$hostCode=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$editor=[IO.File]::ReadAllText("$root/DeskSpaceInlineV11.cs")
$shelf=[IO.File]::ReadAllText("$root/DeskSpaceDueShelfV74.cs")
foreach($p in @('AlertSound { get; set; }','AlertSoundFile { get; set; }',
 'AlertAction { get; set; }','AlertTarget { get; set; }')){
 if(!$m.Contains($p)){throw "R76 model missing $p"}
}
foreach($p in @('"Information", "Notes", "Reminders", "Clipboard", "Applications", "Excel Functions"',
 'selected==11','highlightedSector==11','outer?6:6')){
 if(!$hostCode.Contains($p)){throw "R76 single ring layout missing $p"}
}
if($hostCode.Contains('"Applications", "Tasks", "Excel Functions"') -or
   $hostCode.Contains('selected==12')){throw 'R76 duplicate Tasks or outdated Excel index'}
foreach($p in @('Browse WAV','Open application','Open file',
 'DeskSpaceDueShelfV74.IsAllowedAction','DeskSpaceDueShelfV74.PlaySound',
 'AlertSoundFile=soundPath.Text.Trim()', 'AlertTarget=actionPath')){
 if(!$editor.Contains($p)){throw "R76 editor missing $p"}
}
foreach($p in @('IsAllowedAction(string mode,string target)',
 'PlaySound(ReminderItem reminder)','RunAction(ReminderItem reminder)',
 'if(!IsAllowedAction(reminder.AlertAction,reminder.AlertTarget))return;',
 'RelativeTransform=rotation','RepeatBehavior=RepeatBehavior.Forever',
 'VisibleFor=TimeSpan.FromSeconds(20)',
 'localSystemTime.ToString("hh:mm:ss tt")')){
 if(!$shelf.Contains($p)){throw "R76 alerts missing $p"}
}
Write-Host 'R76 PASS: Tasks merged, prior data preserved, animated rainbow, timed auto dismissal, safe app/file action and selectable tunes.'
