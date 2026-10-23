$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function WriteSource([string]$path,[string]$data){
 [IO.File]::WriteAllText($path,$data,[Text.UTF8Encoding]::new($false))
}
function ReplaceBlock([string]$source,[string]$first,[string]$after,[string]$replacement,[string]$reason){
 $a=$source.IndexOf($first,[StringComparison]::Ordinal)
 $b=if($a -lt 0){-1}else{$source.IndexOf($after,$a,[StringComparison]::Ordinal)}
 if($a -lt 0 -or $b -le $a){throw "R80 cannot find real $reason source"}
 return $source.Substring(0,$a)+$replacement+$source.Substring($b)
}
Copy-Item 'r80/DeskSpaceReminderBrowserV80.cs' "$root/DeskSpaceReminderBrowserV80.cs" -Force
Copy-Item 'r80/DeskSpaceReminderPickerV80.cs' "$root/DeskSpaceReminderPickerV80.cs" -Force
Copy-Item 'r80/DeskSpaceDueShelfV80.cs' "$root/DeskSpaceDueShelfV74.cs" -Force
# WidgetHost opens THIS V36 class; apply only to that live editor.
$p="$root/DeskSpaceInlineV36.cs"
$s=[IO.File]::ReadAllText($p)
$s=ReplaceBlock $s '    private void BuildReminders(StackPanel body)' '    private void BuildClipboard(StackPanel body)' ([IO.File]::ReadAllText('r80/ReminderInlineV80.fragment.txt')) 'widget V36 reminders'
WriteSource $p $s
# Actual main Workspace reminder page.
$p="$root/MainWindow.xaml"
$s=[IO.File]::ReadAllText($p)
$pattern='(?s)<TabItem Tag="Reminders" Header="◉  Reminders">.*?</TabItem>'
if(([regex]::Matches($s,$pattern)).Count -ne 1){throw 'R80 expected one main reminder TabItem'}
$fragment=[IO.File]::ReadAllText('r80/MainReminderV80.xaml.fragment.txt')
$old='              <Button x:Name="ReminderSearchButton"'
$insert='              <Button Content="Perform action" Click="ReminderChooseAction_Click" Padding="9,5" Margin="0,0,7,3"/>'+[string][char]10+'              <Button x:Name="ReminderSearchButton"'
if(!$fragment.Contains($old)){throw 'R80 action picker XAML marker missing'}
$fragment=$fragment.Replace($old,$insert)
$s=[regex]::Replace($s,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$fragment})
WriteSource $p $s
# Main Workspace reminder handlers, not a legacy unused editor.
$p="$root/MainWindow.xaml.cs"
$s=[IO.File]::ReadAllText($p)
$s=ReplaceBlock $s '    private static string ReminderMode(' '    private void NewSnippet_Click(' ([IO.File]::ReadAllText('r80/MainReminderHandlersV80.fragment.txt')) 'main Workspace reminder handlers'
WriteSource $p $s
Write-Host 'R80 APPLIED: real embedded universal search, Browse file, Open URL, default-browser timed preparation in shared due scheduler.'
