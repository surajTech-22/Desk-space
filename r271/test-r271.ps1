$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceReminderScheduleV83.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($v in @(
 'if(IsRecurring(item))',
 'if(item.Due<=now)item.Due=Next(item,now);',
 'item.LastAlert=null;',
 'else item.LastAlert=now;',
 'return false;',
 'TestRecurringSnoozeRecoveryV271();',
 'R271 snoozed recurring reminder must not fire a second time immediately'
)){
 if(!$s.Contains($v) -and !$t.Contains($v)){
  throw ('R271 missing rearm/safety test: '+$v)
 }
}
if($s.Contains('item.LastAlert=IsRecurring(item)?null:now;')){
 throw 'R271 cannot leave past recurring due after snooze acknowledgement'
}
Write-Host 'R271 STATIC PASS: recurring snooze is display-only and rearmed after dismissal, no double launch.'
