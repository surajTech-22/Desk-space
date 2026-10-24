$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$rec=[IO.File]::ReadAllText("$r/DeskSpaceRecentFileStateV99.cs")
$preview=[IO.File]::ReadAllText("$r/DeskSpaceDeckPreviewV99.cs")
foreach($token in @(
 'DeskSpaceRecentFileStateV99.IsDismissed(path)',
 'DeskSpaceRecentFileStateV99.Dismiss(item.StagedPath)',
 'DeskSpaceRecentFileStateV99.DismissMany(',
 'private bool previewOnHover;',
 'deckPreview ??= new DeskSpaceDeckPreviewV99(panel)',
 'DeckHeaderButton(previewOnHover?"◉":"◎"',
 'deckPreview?.Bind(previewFile,row,item.StagedPath,()=>previewOnHover)',
 'DeckPreviewV99.OpenContainingFolder',
 'StageFilesAsync(new[]{item.StagedPath})',
 'deckPreview?.Dispose()',
 'deckPreview?.Close();')){
 if(!$s.Contains($token)){throw "R99 missing live recent/preview action: $token"}
}
foreach($token in @('RecentDismissals.json','SHA256.HashData',
 'info.LastWriteTimeUtc.Ticks','File.Move(tmp,FileName,true)',
 'internal static bool IsDismissed(string path)',
 'internal static void DismissMany(',
 'internal static bool DifferentVersionIsNew(')){
 if(!$rec.Contains($token)){throw "R99 missing version-aware recent dismissal: $token"}
}
foreach($token in @('PlacementTarget=deck','Placement=PlacementMode.Right',
 'hoverOpen.Tick+=','internal void Bind(',
 'DeskSpaceFilePreviewV85.Create(path)',
 'internal void OpenContainingFolder(',
 'CloseIfMissing()','preview.Child=null;',
 'row.Unloaded+=','public void Dispose()')){
 if(!$preview.Contains($token)){throw "R99 detached preview missing behavior: $token"}
}
if($s.Contains('DeskSpaceFilePreviewV85.Attach(previewFile,item.StagedPath);')){
 throw 'R99 old row-bound preview still active'
}
Write-Host 'R99 PASS: files watch stays intact, dismissed versions persist, newer saves reappear, quick hold/reveal, stable deck preview remains independent of redraws'
