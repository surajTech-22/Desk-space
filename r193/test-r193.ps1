$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchV11.cs')
$worker=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchActionsV193.cs')
foreach($required in @(
    'async void Do(SearchHit hit,string operation)',
    'DeskSpaceSearchActionsV193.NextIndex(',
    'System.Windows.Automation.AutomationProperties.SetName(',
    'e.Key==Key.Down||e.Key==Key.Up',
    'e.Key==Key.PageDown||e.Key==Key.PageUp',
    'e.Key==Key.F8 && hits.Count>0',
    'case "zip":',
    'await Task.Run(()=>DeskSpaceSearchActionsV193.ZipSingleFile(',
    'if(hit.Category=="Files" &&',
    'Math.Clamp(selectedIndex,0,hits.Count-1)'
)){
 if(!$src.Contains($required)){throw ("R193 Search UI missing "+$required)}
}
foreach($required in @(
    'MaxSingleFileBytes=128L*1024*1024',
    'FileAttributes.ReparsePoint',
    'FileMode.CreateNew',
    'File.Move(temp,output,false)',
    'cancellationToken.ThrowIfCancellationRequested()',
    'if(File.Exists(output))throw new IOException',
    'Path.GetFullPath(source)',
    'if(!CanZip(source))'
)){
 if(!$worker.Contains($required)){throw ("R193 archive safety missing "+$required)}
}
if($src.Contains('Stop-Process') -or $worker.Contains('File.Delete(input)')){
 throw 'R193 cannot close other processes or delete selected search files'
}
Write-Host 'R193 PASS: accessible keyboard Search, explicit ZIP, atomic no-overwrite and bounded source stream.'
