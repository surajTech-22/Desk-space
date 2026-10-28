$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R172 $($label) expected one unique anchor, found $n"}
 $script:s=$script:s.Replace($old,$new)
}
# WPF DragDrop currently executes an async method synchronously until the
# first await. A slow placement callback/NTFS exclusive lock could freeze the
# actual pointer drop before it has returned. Defer the transaction's UI
# start to the Dispatcher instead of starting it in PreviewDrop.
Swap @'
        _=CommitFolderDropAsync(paths,zipFolders);
    }
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
'@ @'
        host.Dispatcher.BeginInvoke(new Action(()=>
            _=CommitFolderDropAsync(paths,zipFolders)),
            System.Windows.Threading.DispatcherPriority.Background);
    }
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
'@ 'defer test/compat choice, not block UI drop'
# The real drag-drop path may call the same async method without going via
# CommitFolderDrop. Yield there too, preserving the original folder choice.
Swap @'
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
    {
        if(!zipFolders)
'@ @'
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
    {
        // PreviewDrop must finish before any disk activity or Deck rendering.
        await Task.Yield();
        if(!zipFolders)
'@ 'always yield real WPF drag-drop callback'

# R165 previously opened and Flush(true)'d an exclusive NTFS marker on the
# WPF dispatcher synchronously before Task.Run. This is a real UI freeze risk.
# Move the lease into the worker so copy+marker have exactly the same lifetime.
Swap @'
                    using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
                    // The copy worker writes only to a GUID-owned *.partial
'@ @'
                    // The entire exclusive-file marker and copy lifetime is
                    // background work, never synchronous disk IO on WPF UI.
                    // The copy worker writes only to a GUID-owned *.partial
'@ 'remove synchronous marker'
Swap '                    await Task.Run(()=>DeskSpaceFolderCopyV164.Copy(' @'
                    await Task.Run(()=>{
                        using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
                        DeskSpaceFolderCopyV164.Copy(
'@ 'create marker and stream inside background worker'
Swap @'
                        }));
                    cancellation.Token.ThrowIfCancellationRequested();
'@ @'
                        });
                    });
                    cancellation.Token.ThrowIfCancellationRequested();
'@ 'close Task.Run worker after streaming Copy'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R172: drag callback immediately returns; exclusive copy marker and Flush(true) run only in worker.'
