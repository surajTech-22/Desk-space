$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $path)){throw 'R359 missing real shipping Deck / drag source'}
$s=[IO.File]::ReadAllText($path)
$lf=[string][char]10
$s=$s.Replace(([string][char]13+$lf),$lf)
# Log only source CODE landmarks (no local paths or user data), because the
# canonical shipping source is reconstructed from a verified Windows artifact.
$s.Split([char]10) | Where-Object {$_ -match 'pendingFolderDrop|hoverChoice|FolderDragLeave|HideFolderChoice|PreviewDragEnter|private void DragOver|private void Dispose'} |
    Select-Object -First 35 | ForEach-Object {Write-Host ('R359 SHIPPING LANDMARK: '+$_.Trim())}
function One([string]$from,[string]$to,[string]$why) {
    $from=$from.Replace(([string][char]13+$script:lf),$script:lf)
    $to=$to.Replace(([string][char]13+$script:lf),$script:lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($from))).Count
    if($count -ne 1){throw "R359 $why expected exactly one anchor, got $count"}
    $script:s=$script:s.Replace($from,$to)
}
One @'
    private string[]? pendingFolderDrop;
'@ @'
    private string[]? pendingFolderDrop;
    // WPF OLE DragLeave is also raised when Esc cancels an external drag
    // while the pointer is STILL within the circle. A short deferred reset
    // allows nested-element transitions to receive a new DragOver first.
    private readonly DispatcherTimer folderCancelResetV359=new()
        {Interval=TimeSpan.FromMilliseconds(175)};
'@ 'existing per-widget drag choice state'
One @'
        host.PreviewDragEnter+=FolderDragEnter;
        host.PreviewDragLeave+=SmartDragLeaveV178;
'@ @'
        folderCancelResetV359.Tick+=FolderCancelledV359;
        host.PreviewDragEnter+=FolderDragEnter;
        host.PreviewDragLeave+=SmartDragLeaveV178;
'@ 'one timer per widget, wired before drag enter'
One @'
    private void DragOver(object sender,DragEventArgs e)
    {
        // A user-initiated text or URL drag has its own choices.
'@ @'
    private void DragOver(object sender,DragEventArgs e)
    {
        // A new DragOver means the drag is still live. In particular, moving
        // between child shapes in the same widget may raise PreviewDragLeave
        // followed immediately by DragOver; avoid flickering ZIP / Copy.
        folderCancelResetV359.Stop();
        // A user-initiated text or URL drag has its own choices.
'@ 'cancel pending leave if drag is still active'
One @'
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        e.Effects=(busy||disposed||held.Count>=25)?DragDropEffects.None:DragDropEffects.Copy;
'@ @'
        // A different drag type or a non-folder file cannot retain a stale
        // ZIP / Copy overlay from an earlier cancelled folder drag.
        if(pendingFolderDrop!=null)HideFolderChoice();
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        e.Effects=(busy||disposed||held.Count>=25)?DragDropEffects.None:DragDropEffects.Copy;
'@ 'clear stale folder overlay when hovered object is not a folder'
One @'
    private void FolderDragLeave(object sender,DragEventArgs e)
    {
        if(pendingFolderDrop==null)return;
        // Nested WPF elements can generate a DragLeave without leaving
        // the host; do not flicker when crossing internal donut graphics.
        var p=e.GetPosition(host);
        if(p.X>=0&&p.Y>=0&&p.X<=host.ActualWidth&&p.Y<=host.ActualHeight)
            return;
        HideFolderChoice();
    }
'@ @'
    private void FolderDragLeave(object sender,DragEventArgs e)
    {
        if(pendingFolderDrop==null)return;
        // Do not use pointer-in-window coordinates to decide that a drag
        // survived. Esc cancels Explorer's OLE drag while the pointer is
        // still inside this WPF window; there may be no Drop event.
        // Delay only long enough to absorb nested child DragLeave/DragOver.
        // If a real DragOver follows, DragOver() stops this timer.
        folderCancelResetV359.Stop();
        folderCancelResetV359.Start();
    }
    private void FolderCancelledV359(object? sender,EventArgs e)
    {
        folderCancelResetV359.Stop();
        if(pendingFolderDrop!=null)HideFolderChoice();
    }
'@ 'Esc/abandoned folder drag restores circle even inside window'
One @'
    private void HideFolderChoice()
    {
        pendingFolderDrop=null;
'@ @'
    private void HideFolderChoice()
    {
        folderCancelResetV359.Stop();
        pendingFolderDrop=null;
'@ 'drop and cancellation share exactly one teardown'
One @'
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
'@ @'
    public void Dispose()
    {
        if(disposed)return;
        // Host close and multi-monitor removal must not strand visual
        // adorners or timers on an old WPF widget.
        folderCancelResetV359.Stop();
        HideFolderChoice();
        HideSmartDropV178();
        disposed=true;
'@ 'dispose overlay on monitor close'
One @'
        host.PreviewDragOver-=DragOver;
        host.PreviewDrop-=Receive;
'@ @'
        folderCancelResetV359.Tick-=FolderCancelledV359;
        host.PreviewDragEnter-=FolderDragEnter;
        host.PreviewDragLeave-=FolderDragLeave;
        host.PreviewDragLeave-=SmartDragLeaveV178;
        host.PreviewDragOver-=DragOver;
        host.PreviewDrop-=Receive;
'@ 'unregister all drag handlers to avoid stale widget state'
# The shipped R232/356 application retains the original R160 ZIP/Copy
# authorization and completion path. R359 must not introduce a new drop
# engine or mistake later development-only R256 code for published code.
if(!$s.Contains('CommitFolderDropAsync(') -or
   !$s.Contains('private void Receive(object sender,DragEventArgs e)') -or
   !$s.Contains('folderLayer.Remove(folderAdorner)')){
    throw 'R359 must retain original folder ZIP / Copy handling'
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R359: Esc / abandoned folder drag returns ZIP/Copy overlay to normal circle after deferred DragLeave; valid drops unchanged.'
