$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Get-ChildItem $r -Filter '*.cs'|ForEach-Object {
 $s=[IO.File]::ReadAllText($_.FullName)
 if($s.Contains('ApplyWindowsStartup') -or $s.Contains('ShowFloatingWidget') -or $s.Contains('WidgetHostLauncher')) {
  $ls=$s -split '\r?\n';Write-Host "==startup file $($_.Name) $($s.Length)=="
  for($i=0;$i -lt $ls.Length;$i++){if($ls[$i] -match 'ApplyWindowsStartup|ShowFloatingWidget|WidgetHostLauncher|OnStartup|ShutdownMode|MainWindow|Run\\|Registry.CurrentUser|StartupTask|\\-\\-startup|autostart') { Write-Host "$($i+1): $($ls[$i].Trim())"}}
 }
}
foreach($name in @('App.xaml','App.xaml.cs','WidgetSettingsV3.cs','DeskSpaceDropShelfV15.cs','DeskSpaceNewFileWatchV53.cs','WidgetHost.cs','Models.cs','WorkspaceStore.cs','DeskSpaceDueShelfV74.cs')){
 $path=Join-Path $r $name
 if(!(Test-Path $path)){continue}
 $s=[IO.File]::ReadAllText($path)
 Write-Host "=======FILE $name length=$($s.Length)======="
 if($name -in @('App.xaml','App.xaml.cs','WidgetSettingsV3.cs','DeskSpaceDropShelfV15.cs','Models.cs','WorkspaceStore.cs')){
   Write-Host $s.Substring(0,[Math]::Min($s.Length,29000))
 }elseif($name -eq 'WidgetHost.cs'){
  foreach($token in @('WidgetHostLauncher','private void Tick(','attachedShelf.Enable()','protected override void OnSourceInitialized','public WidgetHost(','private void UpdateCenter(','DeskSpaceDueShelfV74','void Show(')){
   $i=$s.IndexOf($token,[StringComparison]::Ordinal); if($i -ge 0){$j=[Math]::Max(0,$i-500);Write-Host "==== widget $token ====";Write-Host $s.Substring($j,[Math]::Min(6200,$s.Length-$j))}
  }
 }elseif($name -eq 'DeskSpaceNewFileWatchV53.cs'){
  foreach($token in @('void Start()','ReconcileAsync(','DefaultRoots()','Changed(','Created(','On','Offer','TryPost','IsOrdinaryUserFile')){
   $i=$s.IndexOf($token,[StringComparison]::Ordinal);if($i -ge 0){Write-Host "==== watcher $token ====";Write-Host $s.Substring($i,[Math]::Min(1800,$s.Length-$i))}
  }
 }else{
  Write-Host $s.Substring(0,[Math]::Min($s.Length,15000))
 }
}
throw 'R82 intentional pre-release inspection gate; do not publish'
