$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$deck=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$service=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFolderPreDropV256.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'card.PreviewDragEnter+=PreEnterV256;',
 'card.PreviewDragOver+=PreOverV256;',
 'card.PreviewDrop+=PreDropV256;',
 'host.PreviewDragEnter+=PreEnterV256;',
 'host.PreviewDragOver+=PreOverV256;',
 'host.PreviewDrop+=PreDropV256;',
 'DeskSpaceFolderPreDropV256.Hover(',
 'DeskSpaceFolderPreDropV256.Authorized(',
 'if(approved)CommitFolderDrop(zip);',
 'TimeSpan.FromSeconds(12)',
 'TestFolderPreDropV256();'
)){
 if(!$deck.Contains($needle) -and !$service.Contains($needle) -and !$smoke.Contains($needle)){
  throw ('R256 missing native pre-drop safety '+$needle)
 }
}
if(!$deck.Contains('if(FolderFilesV256(e)!=null)') -or
   !$deck.Contains('e.Effects=DragDropEffects.None;')){
 throw 'R256 original Drop handler must block post-drop choices'
}
if(!$deck.Contains('expiryV256?.Stop();')){
 throw 'R256 must clear abandoned drag selection and timer'
}
Write-Host 'R256 STATIC PASS: ZIP/Copy pre-drop hover, popup and widget HWND handlers, exact-source authorization.'
