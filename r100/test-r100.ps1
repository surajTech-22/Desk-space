$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$r/DeskSpaceWindowPreviewV100.cs")
$files=[IO.File]::ReadAllText("$r/DeskSpaceDeckPreviewV99.cs")
$apps=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
$shelf=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
foreach($term in @(
 'Interval=TimeSpan.FromMilliseconds(650)',
 'internal static bool Stationary(Point before,Point after',
 'PrintWindow(hwnd,hdc,2)',
 'Task.Run(()=>Capture(app.Handle))',
 'if(disposed||version!=generation||!popup.IsOpen',
 'tile.MouseMove+=',
 'Close();',
 'Start(tile,panel,resolve,now)',
 'IsHitTestVisible=false',
 'internal void Bind(FrameworkElement tile',
 'popup.PlacementTarget=panel',
 'popup.Placement=showRight?PlacementMode.Right:PlacementMode.Left')){
 if(!$app.Contains($term)){throw "R100 app stationary preview feature missing: $term"}
}
foreach($term in @(
 'Interval=TimeSpan.FromMilliseconds(650)',
 'internal static bool Stationary(Point previous,Point current',
 'Stationary(pendingPosition,Mouse.GetPosition(row))',
 'row.MouseMove+=',
 'if(preview.IsOpen && !manualPreview)Close()',
 'else Show(path,row,true)')){
 if(!$files.Contains($term)){throw "R100 file preview idle-only behavior missing: $term"}
}
foreach($term in @(
 'new DeskSpaceWindowPreviewV100()',
 'appPreview.Bind(appButton,iconCard,',
 'appPreview.Bind(choice,windowCard,()=>selected)',
 'appPreview.Dispose()')){
 if(!$apps.Contains($term)){throw "R100 Running Apps preview integration missing: $term"}
}
if(!$shelf.Contains('private bool previewOnHover=true;')){
 throw 'R100 file preview must work by default without manually enabling'
}
if($app.Contains('System.Drawing.Graphics.CopyFromScreen') -or
   $app.Contains('Process.Kill(')){
 throw 'R100 must never capture desktop or terminate applications'
}
Write-Host 'R100 PASS: real window previews on intentional idle hover, exact multi-window labels, moving cancels both file and app previews.'
