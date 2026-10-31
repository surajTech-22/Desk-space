$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r206/DeskSpaceFolderZipV206.cs' -Destination "$root/DeskSpaceFolderZipV206.cs" -Force
Copy-Item -LiteralPath 'r206/DeskSpaceZipProgressWindowV206.cs' -Destination "$root/DeskSpaceZipProgressWindowV206.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$before=@'
                await Task.Run(()=>DeskSpaceFolderZipV162.Create(
                    source,zipPath,cancellation.Token,n=>{
                        if(n!=1 && n%5!=0)return; // avoid excessive WPF redraws
                        host.Dispatcher.BeginInvoke(new Action(()=>{
                            if(zipInProgress && ReferenceEquals(zipCancellation,cancellation))
                            {
                                zipFilesWritten=n;
                                RefreshViews();
                            }
                        }));
                    }));
'@.Replace($crlf,$lf)
$after=@'
                // Keep R162's existing donut cancellation and file count.
                // Add bounded ZIP creation, byte progress and explicit Cancel UI.
                var progressWindow=DeskSpaceZipProgressWindowV206.Show(host,cancellation);
                try
                {
                    var reporter=new Progress<DeskSpaceFolderZipV206.Progress>(item=>{
                        progressWindow.Report(item);
                        if(item.Files==1||item.Files%5==0)
                        {
                            if(zipInProgress&&ReferenceEquals(zipCancellation,cancellation))
                            {
                                zipFilesWritten=item.Files;
                                RefreshViews();
                            }
                        }
                    });
                    await Task.Run(()=>DeskSpaceFolderZipV206.Create(
                        source,zipPath,reporter,cancellation.Token));
                }
                finally{progressWindow.Close();}
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R206 current R162 ZIP call expected once, found "+$n)}
$s=$s.Replace($before,$after)
if(!$s.Contains('catch(OperationCanceledException)') -or
   !$s.Contains('zipCancellation=cancellation;') -or
   !$s.Contains('pendingZipSources[zipPath]=source;')){
 throw 'R206 must retain existing R162 user cancellation and shared Deck source tracking'
}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R206: ZIP operation now cancellable with progress; final Deck staging remains unchanged.'
