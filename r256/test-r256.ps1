$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$deck=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$service=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFolderPreDropV256.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'host.PreviewDragEnter+=FolderDragEnter;',
 'host.PreviewDrop+=Receive;',
 'folderAdorner?.Hover(point);',
 'DeskSpaceFolderDragAdornerV160.ChoiceAt(',
 'hoverChoiceV256=hovered.HasValue?',
 'DeskSpaceFolderPreDropV256.Authorized(',
 'if(!busy && approved)',
 'hoverChoiceV256=DeskSpaceFolderPreDropV256.Choice.None;',
 'TestFolderPreDropV256();'
)){
 if(!$deck.Contains($needle) -and !$service.Contains($needle) -and !$smoke.Contains($needle)){
  throw ('R256 missing native adorner pre-drop authorization '+$needle)
 }
}
if($deck.Contains('card.PreviewDragOver+=PreOverV256;') -or
   $deck.Contains('folderChoice.Child is Border')){
 throw 'R256 must never replace the existing in-circle adorner with a Popup'
}
Write-Host 'R256 STATIC PASS: existing in-circle pre-drop ZIP/Copy adorner authorizes only matched prior hover and drop.'
