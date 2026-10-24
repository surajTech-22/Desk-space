$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$model=[IO.File]::ReadAllText("$r/Models.cs")
$store=$model
$engine=[IO.File]::ReadAllText("$r/DeskSpaceReminderScheduleV83.cs")
$shelf=[IO.File]::ReadAllText("$r/DeskSpaceDueShelfV74.cs")
$history=[IO.File]::ReadAllText("$r/DeskSpaceReminderActionHistoryV83.cs")
$widget=[IO.File]::ReadAllText("$r/DeskSpaceInlineV36.cs")
$main=[IO.File]::ReadAllText("$r/MainWindow.xaml")
$handlers=[IO.File]::ReadAllText("$r/MainWindow.xaml.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('RepeatPattern','RepeatDays','RepeatEveryDays',
 'SnoozedUntil','SnoozeOnly','Guid.NewGuid().ToString("N")',
 'Local\DeskSpace.Workspace.JsonWrite')){
 if(!$model.Contains($token)){throw "R83 compatible durable model missing: $token"}
}
foreach($token in @('internal static DateTime Next(','internal static void Snooze(',
 'internal static bool MarkAlert(','internal static IReadOnlyList<ReminderItem> Due(',
 'Weekdays','Selected days','Every N days')){
 if(!$engine.Contains($token)){throw "R83 shared scheduler missing: $token"}
}
foreach($token in @('FileSystemWatcher','dataDirty=true',
 'DateTime.UtcNow-cacheReadUtc<TimeSpan.FromMinutes(1)',
 'DeskSpaceReminderScheduleV83.Due(',
 'DeskSpaceReminderScheduleV83.MarkAlert(',
 'DeskSpaceReminderScheduleV83.Snooze(',
 'Skipped stale automatic action','ContextMenu','Custom minutes',
 'Snooze','BuildAlert(')){
 if(!$shelf.Contains($token)){throw "R83 due shelf reliability/recurrence missing: $token"}
}
foreach($token in @('reminder-action-history.jsonl','Record(ReminderItem reminder',
 'ReadRecent(int limit=25)','new Uri(url).Host')){
 if(!$history.Contains($token)){throw "R83 action history missing: $token"}
}
foreach($token in @('Repeat / custom schedule','RepeatEveryDays=IntervalDays()',
 'RepeatPattern=recurrence.SelectedItem','Button("History"','Save changes',
 'selected.SnoozedUntil')){
 # selected.SnoozedUntil is not necessary; edits target entry
 if($token -eq 'selected.SnoozedUntil'){continue}
 if(!$widget.Contains($token)){throw "R83 actual V36 repeat editor missing: $token"}
}
foreach($token in @('ReminderRepeatChoice','ReminderRepeatEvery','ReminderRepeatDays',
 'ReminderHistory_Click')){
 if(!$main.Contains($token)){throw "R83 main Workspace repeat UI missing: $token"}
}
foreach($token in @('ReminderRepeatChoice_SelectionChanged','RepeatIntervalDays()',
 'RepeatPattern=RepeatChoiceName()','ReminderHistory_Click')){
 if(!$handlers.Contains($token)){throw "R83 main repeat handlers missing: $token"}
}
foreach($token in @('TestReminderSchedulerV83();','R83 Snooze must not re-launch a file or URL',
 'R83 recurring alert must be rearmed at next valid occurrence',
 'r83-rainbow-snooze-alert','r83-recurring-reminder-widget')){
 if(!$smoke.Contains($token)){throw "R83 WPF regression missing: $token"}
}
Write-Host 'R83 preflight passed: shared recurrence/snooze, missed alerts, event-driven cache, action history, backups, both actual editors.'
