$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('Models.cs','WidgetHost.cs','DeskSpaceModuleV10.cs')){
 $s=[IO.File]::ReadAllText((Join-Path $root $name))
 if($name -eq 'Models.cs'){Write-Host '=====FULL MODELS=====';Write-Host $s;continue}
 if($name -eq 'WidgetHost.cs'){
  foreach($anchor in @('private static readonly string[] Segments','private static readonly string[] SegmentIcons','private int SectorAt(','private int QuickAddHitAt(','private void BuildDonut(','private void ClickSegment(','private void BuildCompact(')){
   $index=$s.IndexOf($anchor,[StringComparison]::Ordinal)
   if($index -ge 0){$end=$s.IndexOf('    private ', $index+$anchor.Length,[StringComparison]::Ordinal);if($end -lt 0){$end=[Math]::Min($s.Length,$index+5000)};Write-Host "====HOST $anchor====";Write-Host $s.Substring($index,[Math]::Min(12000,$end-$index))}
  }
  foreach($term in @('==12','!=12','>=7','%7','360.0/7','"Tasks"','"Reminders"','Segments.Length')){
   $lines=$s -split '\r?\n';$matches=@()
   for($i=0;$i -lt $lines.Length;$i++){if($lines[$i].Contains($term)){$matches+=($i+1).ToString()+': '+$lines[$i]}}
   Write-Host "==== OCCURS $term ===="; $matches | Select-Object -First 60 | ForEach-Object {Write-Host $_}
  }
 }
 if($name -eq 'DeskSpaceModuleV10.cs'){
  $idx=$s.IndexOf('private void BuildReminders(');$end=$s.IndexOf('private void BuildCalendar(',$idx)
  Write-Host '==== REMINDER EDITOR ====';Write-Host $s.Substring($idx,$end-$idx)
 }
}
throw 'R75 info-only probe / no release'
