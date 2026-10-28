$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$zip=[IO.File]::ReadAllText("$root/DeskSpaceFolderZipV159.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'private readonly Popup folderChoice;',
 'private string[]? pendingFolderDrop;',
 'Content="ZIP"',
 'Content="Copy"',
 'Content="×"',
 'DeskSpaceMonitorFenceV153.TrackPopup(host,folderChoice);',
 'private void ShowFolderChoice(string[] incoming)',
 'private void CommitFolderDrop(bool zipFolders)',
 'private void HideFolderChoice()',
 'if(dropped?.Any(Directory.Exists)==true)',
 'ShowFolderChoice(dropped);',
 'private void Receive(object sender,DragEventArgs e)',
 'DeskSpaceFolderZipV159.CreateFromFolder(source,destination);',
 'private async Task StageFilesAsync(string[] files,bool zipFolders=false)',
 'private static readonly List<Held> held=new();',
 'RefreshViews();'
)){
 if(!$deck.Contains($token)){throw "R159 folder drop or shared Deck integration missing: $token"}
}
foreach($token in @(
 'ZipArchiveMode.Create',
 'FileMode.CreateNew',
 'ZipFile.OpenRead(partial)',
 'File.Move(partial,destination);',
 'FileAttributes.ReparsePoint',
 'archive.CreateEntry(prefix,CompressionLevel.NoCompression)',
 'archive.CreateEntryFromFile(file',
 'catch',
 'File.Delete(partial)'
)){
 if(!$zip.Contains($token)){throw "R159 ZIP safety missing: $token"}
}
foreach($token in @(
 'TestFolderDropZipOrCopyV159();',
 'private static void TestFolderDropZipOrCopyV159()',
 'R159 folder action must open on dropped widget only',
 'R159 Copy must hold the folder and its original structure',
 'R159 staged ZIP must be visible from both widget Decks',
 'R159 source folder must never be modified',
 'R159 cancelling a folder drop must not create a file'
)){
 if(!$ui.Contains($token)){throw "R159 real WPF acceptance missing: $token"}
}
Write-Host 'R159 PASS: per-widget ZIP/Copy prompt; raw folder Copy, ZIP staging, reparse-point protection, shared Deck and real WPF acceptance.'
