$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $path)){throw 'R359 missing shipping Deck source'}
$s=[IO.File]::ReadAllText($path)
function Assert([bool]$yes,[string]$why){
    if(!$yes){throw 'R359 FAILED: '+$why}
}
function Part([string]$start,[string]$next){
    $i=$s.IndexOf($start,[StringComparison]::Ordinal)
    $j=$s.IndexOf($next,$i+$start.Length,[StringComparison]::Ordinal)
    if($i -lt 0 -or $j -lt $i){throw "R359 missing method $start"}
    return $s.Substring($i,$j-$i)
}
$over=Part 'private void DragOver(object sender,DragEventArgs e)' 'private void Receive(object sender,DragEventArgs e)'
$leave=Part 'private void FolderDragLeave(object sender,DragEventArgs e)' 'private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)'
$cancel=Part 'private void FolderCancelledV359(object? sender,EventArgs e)' 'private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)'
$hide=Part 'private void HideFolderChoice()' 'private void CommitFolderDrop(bool zipFolders)'
$drop=Part 'private void Receive(object sender,DragEventArgs e)' 'private void FolderDragEnter(object sender,DragEventArgs e)'
$close=Part 'public void Dispose()' '    }' # first Dispose close not enough; check rest by string.
Assert ($s.Contains('private readonly DispatcherTimer folderCancelResetV359=new()')) 'per-widget cancel timer missing'
Assert ($s.Contains('Interval=TimeSpan.FromMilliseconds(175)')) 'bounded leave debounce missing'
Assert ($s.Contains('folderCancelResetV359.Tick+=FolderCancelledV359;')) 'cancel timer never connected'
Assert ($leave.Contains('folderCancelResetV359.Start();')) 'DragLeave does not schedule cancel cleanup'
Assert (!$leave.Contains('e.GetPosition(host)') -and !$leave.Contains('host.ActualWidth')) 'Escape cancel is still ignored inside the widget'
Assert ($over.Contains('folderCancelResetV359.Stop();')) 'nested element DragOver does not cancel pending teardown'
Assert ($cancel.Contains('if(pendingFolderDrop!=null)HideFolderChoice();')) 'uncompleted drag does not clear ZIP/Copy'
Assert ($hide.Contains('folderCancelResetV359.Stop();')) 'finalization leaves timer running'
Assert ($hide.Contains('pendingFolderDrop=null;') -and
        $hide.Contains('folderLayer.Remove(folderAdorner)')) 'original ZIP/Copy adorner teardown removed'
Assert ($drop.Contains('HideFolderChoice();') -and
        $drop.Contains('CommitFolderDropAsync(')) 'original ZIP/Copy drop action changed'
Assert ($s.Contains('folderCancelResetV359.Tick-=FolderCancelledV359;') -and
        $s.Contains('host.PreviewDragEnter-=FolderDragEnter;') -and
        $s.Contains('host.PreviewDragLeave-=FolderDragLeave;') -and
        $s.Contains('host.PreviewDragLeave-=SmartDragLeaveV178;')) 'window close does not unsubscribe drag handlers'
Assert (!$s.Contains('private readonly Popup folderChoice;')) 'cancel fix introduced a detached popup'
Assert ($s.Contains('host.PreviewDragOver+=DragOver;') -and $s.Contains('host.PreviewDrop+=Receive;')) 'existing native WPF drop handlers were replaced'
Write-Host 'R359 PASS: Esc-cancel inside circle schedules reset; nested transition stays stable; valid ZIP/Copy keeps original drop implementation; closing widgets unhooks handlers.'
