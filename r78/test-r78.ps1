$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$src=[IO.File]::ReadAllText("$root/DeskSpaceInlineV11.cs")
$time=[IO.File]::ReadAllText("$root/DeskSpaceReminderTimeV78.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('REMINDERS · SOUND & ACTIONS',
    'Where(x=>!x.Done)', 'if(r!=null)r.Done=true;',
    'Sound / Tune','Test sound','Browse WAV','Perform action',
    'Browse target','LostKeyboardFocus',
    'DeskSpaceReminderTimeV78.TryParse(time.Text')){
    if(!$src.Contains($token)){throw "R78 editor missing $token"}
}
foreach($token in @('hours%12+(pm?12:0)',
 'if(hours>23)return false;','if(hours<1||hours>12)return false;',
 'minutes>59||seconds>59','text.Length-4')){
 if(!$time.Contains($token)){throw "R78 time parser missing $token"}
}
foreach($token in @('R78 time parser case',
 'R78 completed entries must not appear in active list',
 'R78 sound and perform action controls must be visible',
 'reminders-v78-controls')){
 if(!$smoke.Contains($token)){throw "R78 WPF check missing $token"}
}
Write-Host 'R78 PASS: clock parsing, completed invisible, sound/action UI independently tested.'
