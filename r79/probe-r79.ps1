$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('MainWindow.xaml','MainWindow.xaml.cs','WidgetHost.cs','DeskSpaceInlineV11.cs'))
{
 $p=Join-Path $root $name
 $s=[IO.File]::ReadAllText($p)
 Write-Host "====R79 SOURCE $name ($($s.Length) chars)===="
 if($name -eq 'MainWindow.xaml'){
   $matches=[regex]::Matches($s,'(?s)<TabItem[^>]*Header="[^"]*Reminders"[^>]*>.*?</TabItem>')
   foreach($m in $matches){Write-Host "=====MAIN REMINDER XAML====";Write-Host $m.Value}
   continue
 }
 if($name -eq 'MainWindow.xaml.cs'){
   $lines=$s -split '\r?\n'
   foreach($term in @('Reminders','ReminderList','AddReminder','DoneReminder','MarkDone','RefreshReminder','LoadReminders','Reminder_Click','ReminderTime','TimeSpan.TryParse')){
     Write-Host "===MAIN occurrences '$term'==="
     for($i=0;$i -lt $lines.Length;$i++){
       if($lines[$i].Contains($term)){Write-Host ("{0}: {1}" -f ($i+1),$lines[$i])}
     }
   }
   foreach($term in @('private void AddReminder','private void MarkReminder','private void DeleteReminder','private void RefreshReminders','private void SaveReminder')){
     $at=$s.IndexOf($term,[StringComparison]::Ordinal)
     if($at -ge 0){Write-Host "====MAIN METHOD $term====";Write-Host $s.Substring($at,[Math]::Min(4800,$s.Length-$at))}
   }
   continue
 }
 if($name -eq 'WidgetHost.cs'){
   foreach($term in @('private void OpenInWidget(', 'new DeskSpaceInlineV11(', 'private static readonly string[] Segments','public static class WidgetHostLauncher')){
     $at=$s.IndexOf($term,[StringComparison]::Ordinal)
     if($at -ge 0){Write-Host "====WIDGET $term====";Write-Host $s.Substring([Math]::Max(0,$at-300),[Math]::Min(3600,$s.Length-[Math]::Max(0,$at-300)))}
   }
   continue
 }
 $at=$s.IndexOf('private void BuildReminders(')
 if($at -ge 0){Write-Host "====COMPILED INLINE SOURCE====";Write-Host $s.Substring($at,[Math]::Min(1500,$s.Length-$at))}
}
throw 'R79 deliberate diagnostic gate: forbid release until real UI paths reconciled'
