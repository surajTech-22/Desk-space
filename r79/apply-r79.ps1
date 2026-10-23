$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function WriteSource([string]$path,[string]$data){
 [IO.File]::WriteAllText($path,$data,[Text.UTF8Encoding]::new($false))
}
function ReplaceOne([string]$text,[string]$old,[string]$new,[string]$why){
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(([regex]::Matches($text,[regex]::Escape($old))).Count -ne 1){
  throw "R79 missing unique $why in actual Windows source"}
 return $text.Replace($old,$new)
}
# The widget opens DeskSpaceInlineV36, not V11. Old R78 tested the wrong
# type, so its presence in the installer was not proof of a usable widget.
$p="$root/DeskSpaceInlineV36.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$begin=$s.IndexOf('    private void BuildReminders(StackPanel body)')
$end=$s.IndexOf('    private void BuildClipboard(StackPanel body)',$begin)
if($begin -lt 0 -or $end -le $begin){throw 'R79 actual widget editor not found'}
$fragment=[IO.File]::ReadAllText('r79/ReminderInlineV79.fragment.txt')
$s=$s.Substring(0,$begin)+$fragment+$s.Substring($end)
WriteSource $p $s

# The main Workspace is a SECOND actual, independent editor. Keep all its
# existing XAML tabs and data bindings; replace only its Reminder tab.
$p="$root/MainWindow.xaml"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$pattern='(?s)<TabItem Tag="Reminders" Header="◉  Reminders">.*?</TabItem>'
if(([regex]::Matches($s,$pattern)).Count -ne 1){
 throw 'R79 main Workspace reminder TabItem missing'}
$block=[IO.File]::ReadAllText('r79/MainRemindersV79.xaml.fragment.txt')
$block=$block.Replace('              <Button Content="Browse target"',
 '              <Button Content="Perform action" Click="ReminderChooseAction_Click" Padding="9,5" Margin="0,0,7,3"/>'+[string][char]10+'              <Button Content="Browse target"')
$s=[regex]::Replace($s,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$block})
WriteSource $p $s

$p="$root/MainWindow.xaml.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$s=ReplaceOne $s 'RemindersList.ItemsSource = data.Reminders;' @'
var activeReminders=System.Windows.Data.CollectionViewSource.GetDefaultView(data.Reminders);
        activeReminders.Filter=item=>item is ReminderItem reminder && !reminder.Done;
        RemindersList.ItemsSource=activeReminders;
'@ 'main active-only reminder view'
$begin=$s.IndexOf('    private void AddReminder_Click(object sender, RoutedEventArgs e)')
$end=$s.IndexOf('    private void NewSnippet_Click(object sender, RoutedEventArgs e)',$begin)
if($begin -lt 0 -or $end -le $begin){
 throw 'R79 existing main Reminder add/complete/delete handlers missing'}
$handlers=[IO.File]::ReadAllText('r79/MainReminderHandlersV79.fragment.txt')
$s=$s.Substring(0,$begin)+$handlers+$s.Substring($end)
WriteSource $p $s
Write-Host 'R79 APPLIED: REAL widget V36 and REAL MainWindow reminders, shared time classifier, no duplicated stores.'
