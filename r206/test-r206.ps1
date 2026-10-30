$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$zip=[IO.File]::ReadAllText("$root/DeskSpaceFolderZipV206.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceZipProgressWindowV206.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($required in @(
 'internal const int MaxEntries=4096',
 'internal const long MaxTotalBytes=1024L*1024*1024',
 'MaxFileBytes=512L*1024*1024',
 'cancel.ThrowIfCancellationRequested();',
 'FileAttributes.ReparsePoint',
 'new FileStream(partial,FileMode.CreateNew',
 'ZipFile.OpenRead(partial)',
 'File.Move(partial,output,false)',
 'File.Delete(partial)'
)){
 if(!$zip.Contains($required)){throw ("R206 ZIP safety check missing "+$required)}
}
foreach($required in @(
 'Cancel ZIP',
 'window.Closing',
 'cancel.Cancel()',
 'bar.Value=progress.MaxBytes',
 'DeskSpaceFolderZipV206.Progress'
)){
 if(!$ui.Contains($required)){throw ("R206 progress UI check missing "+$required)}
}
foreach($required in @(
 'DeskSpaceZipProgressWindowV206.Show(host,cancelZip)',
 'DeskSpaceFolderZipV206.Create(',
 'catch(System.OperationCanceledException)',
 'await StageFilesAsync(new[]{zipPath});'
)){
 if(!$deck.Contains($required)){throw ("R206 live Deck path missing "+$required)}
}
if(!$tests.Contains('TestCancellableFolderZipV206();')){
 throw 'R206 isolated native ZIP acceptance absent'
}
if($zip.Contains('Directory.Delete(') -or $zip.Contains('File.Move(source') -or
   $zip.Contains('File.Delete(source') -or $zip.Contains('Process.Start(') -or
   $ui.Contains('Clipboard.SetText(')){
 throw 'R206 must not modify original folders or run external processes'
}
Write-Host 'R206 STATIC PASS: cancel ZIP, progress, 1GiB/4096 budget, anti-link, atomic staging and source protection.'
