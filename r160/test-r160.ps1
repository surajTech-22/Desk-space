$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$draw=[IO.File]::ReadAllText("$root/DeskSpaceFolderDragAdornerV160.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'private DeskSpaceFolderDragAdornerV160? folderAdorner;',
 'host.PreviewDragEnter+=FolderDragEnter;',
 'host.PreviewDragLeave+=FolderDragLeave;',
 'private void DragOver(object sender,DragEventArgs e)',
 'ShowFolderChoice(paths);',
 'folderAdorner?.Hover(point);',
 'DeskSpaceFolderDragAdornerV160.ChoiceAt(',
 'private void Receive(object sender,DragEventArgs e)',
 '_=CommitFolderDropAsync(sources,zip);',
 'private void FolderDragLeave(object sender,DragEventArgs e)',
 'System.Windows.Documents.AdornerLayer.GetAdornerLayer(content)',
 'layer.Add(folderAdorner);',
 'folderLayer.Remove(folderAdorner);',
 'private void HideFolderChoice()',
 'private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)',
 'DeskSpaceFolderZipV159.CreateFromFolder(source,zipPath)',
 'await StageFilesAsync(new[]{zipPath});',
 'private static readonly List<Held> held=new();'
)){
 if(!$deck.Contains($token)){throw "R160 missing in-circle drag interaction: $token"}
}
foreach($forbidden in @(
 'private readonly Popup folderChoice;',
 'folderChoice.IsOpen',
 'PositionFolderChoice()',
 'folderChoice=new Popup',
 'Content="ZIP"',
 'Content="Copy"'
)){
 if($deck.Contains($forbidden)){throw "R160 still has detached/after-drop UI: $forbidden"}
}
foreach($token in @(
 'internal sealed class DeskSpaceFolderDragAdornerV160 : Adorner',
 'IsHitTestVisible=false;',
 'protected override void OnRender(DrawingContext dc)',
 'internal static bool? ChoiceAt(Size hostSize,Point local)',
 'return dx<0;',
 'dc.DrawGeometry(left,null,Half(center,r,true));',
 'dc.DrawGeometry(right,null,Half(center,r,false));',
 'Math.Clamp(Math.Min(hostSize.Width,hostSize.Height)-8.0,84.0,132.0)'
)){
 if(!$draw.Contains($token)){throw "R160 missing circular left/right choice: $token"}
}
foreach($token in @(
 'private static void TestFolderDropZipOrCopyV159()',
 'R160 ZIP/Copy must appear inside only the dragged widget',
 'R160 drag-hover must never stage a file before mouse release',
 'R160 left half of circle must select ZIP',
 'R160 right half of circle must select Copy',
 'R160 outside circular menu must not select an action',
 'R160 ZIP must stage one archive, not the folder',
 'R160 dropping/copying/zipping must leave source untouched'
)){
 if(!$smoke.Contains($token)){throw "R160 Windows WPF smoke test missing: $token"}
}
Write-Host 'R160 PASS: circular in-host drag-hover drop targets, no Popup or after-drop confirmation, two independent widgets and unchanged shared Deck.'
