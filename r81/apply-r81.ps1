$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function WriteSource([string]$path,[string]$data){
 [IO.File]::WriteAllText($path,$data,[Text.UTF8Encoding]::new($false))
}
function ReplaceBlock([string]$source,[string]$first,[string]$after,[string]$replacement,[string]$reason){
 $a=$source.IndexOf($first,[StringComparison]::Ordinal)
 $b=if($a -lt 0){-1}else{$source.IndexOf($after,$a,[StringComparison]::Ordinal)}
 if($a -lt 0 -or $b -le $a){throw "R81 cannot find $reason"}
 return $source.Substring(0,$a)+$replacement+$source.Substring($b)
}
Copy-Item 'r81/DeskSpaceReminderPickerV81.cs' "$root/DeskSpaceReminderPickerV81.cs" -Force
Copy-Item 'r81/DeskSpaceReminderPreferencesV81.cs' "$root/DeskSpaceReminderPreferencesV81.cs" -Force
# The real floating editor, opened by WidgetHost:
$p="$root/DeskSpaceInlineV36.cs"
$s=[IO.File]::ReadAllText($p)
$s=ReplaceBlock $s '    private void BuildReminders(StackPanel body)' '    private void BuildClipboard(StackPanel body)' ([IO.File]::ReadAllText('r81/ReminderInlineV81.fragment.txt')) 'floating reminders V36'
WriteSource $p $s
# Main Workspace Reminders tab is independent of the floating editor.
$p="$root/MainWindow.xaml"
$s=[IO.File]::ReadAllText($p)
$pattern='(?s)<TabItem Tag="Reminders" Header="◉  Reminders">.*?</TabItem>'
if(([regex]::Matches($s,$pattern)).Count -ne 1){throw 'R81 expected one main Reminders tab'}
$fragment=[IO.File]::ReadAllText('r81/MainReminderV81.xaml.fragment.txt')
$old='              <Button x:Name="ReminderBrowseFileButton"'
$insert='              <Button Content="Perform action" Click="ReminderChooseAction_Click" Padding="9,5" Margin="0,0,7,3"/>'+[string][char]10+'              <Button x:Name="ReminderBrowseFileButton"'
if(!$fragment.Contains($old)){throw 'R81 action selection button marker missing'}
$fragment=$fragment.Replace($old,$insert)
$s=[regex]::Replace($s,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$fragment})
WriteSource $p $s
# Main window's event handlers; one existing reminder is updated in place.
$p="$root/MainWindow.xaml.cs"
$s=[IO.File]::ReadAllText($p)
$s=ReplaceBlock $s '    private static string ReminderMode(' '    private void NewSnippet_Click(' ([IO.File]::ReadAllText('r81/MainReminderHandlersV81.fragment.txt')) 'main reminder handlers'
WriteSource $p $s
Write-Host 'R81 APPLIED: always-present action/file search; named results with real icons and exterior popup; optional action title, per-item and default sound persistence.'
