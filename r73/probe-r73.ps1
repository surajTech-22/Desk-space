$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$all=Get-ChildItem $root -Filter '*.cs' -File
$matches=$all | Select-String -Pattern 'class ReminderItem|class WorkspaceStore|class WidgetHost|class WidgetShellWindow|class .*Reminder|DateTime.Now|Reminder|reminder|DispatcherTimer|UpdateCenter|BuildCompact|Task' | Group-Object Path
foreach($m in $matches){
  $file=[IO.Path]::GetFileName($m.Name)
  $lines=[IO.File]::ReadAllLines($m.Name)
  $selected=@()
  for($i=0;$i -lt $lines.Length;$i++){
    if($lines[$i] -match 'class ReminderItem|class WorkspaceStore|class WidgetHost|class WidgetShellWindow|DateTime.Now|Reminder|reminder|DispatcherTimer|UpdateCenter|BuildCompact|Task|Timer'){
      $selected+=($i+1).ToString()+':'+$lines[$i].Trim()
    }
  }
  if($selected.Count -gt 0){
    Write-Host "========== $file ($($lines.Length) lines) =========="
    $selected | Select-Object -First 100 | ForEach-Object {Write-Host $_}
  }
}
foreach($name in @('WidgetHost.cs','DeskSpaceModuleV10.cs','WorkspaceStore.cs')){
 $file=Join-Path $root $name
 if(!(Test-Path $file)){continue}
 $contents=[IO.File]::ReadAllText($file)
 $tokens=if($name -eq 'WidgetHost.cs'){@('public WidgetHost','WidgetHost(','UpdateCenter(','BuildCompact(','private void ClickSegment','private void OpenInWidget','private void ReturnFromModule')}elseif($name -eq 'DeskSpaceModuleV10.cs'){@('private void BuildReminders(')}else{@('class WorkspaceStore','class ReminderItem')}
 foreach($token in $tokens){
  $index=$contents.IndexOf($token,[StringComparison]::Ordinal)
  if($index -ge 0){
   Write-Host "========== $name : $token =========="
   Write-Host $contents.Substring([Math]::Max(0,$index-300),[Math]::Min(4500,$contents.Length-[Math]::Max(0,$index-300)))
  }
 }
}
throw 'R73 deliberate forensic-only stop before packaging; no release'
