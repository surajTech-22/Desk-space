$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r78/DeskSpaceReminderTimeV78.cs' "$root/DeskSpaceReminderTimeV78.cs" -Force
$p="$root/DeskSpaceInlineV11.cs"
$s=[IO.File]::ReadAllText($p)
$a=$s.IndexOf('    private void BuildReminders(StackPanel body)')
$b=$s.IndexOf('    private void BuildClipboard(StackPanel body)',$a)
if($a -lt 0 -or $b -le $a){throw 'R78 reminder editor anchors missing'}
$s=$s.Substring(0,$a)+[IO.File]::ReadAllText('r78/ReminderEditorV78.fragment.txt')+$s.Substring($b)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
# Keep scrollable on shorter screens, but avoid hiding options at normal sizes.
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p)
$old='name=="Notes"?256:name=="Reminders"?420:291'
if(!$s.Contains($old)){throw 'R78 missing reminder height anchor'}
$s=$s.Replace($old,'name=="Notes"?256:name=="Reminders"?525:291')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R78 source applied: clock classification, completed archive, sound/action UI.'
