$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r206/DeskSpaceFolderZipV206.cs' -Destination "$root/DeskSpaceFolderZipV206.cs" -Force
Copy-Item -LiteralPath 'r206/DeskSpaceZipProgressWindowV206.cs' -Destination "$root/DeskSpaceZipProgressWindowV206.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$before='                await Task.Run(()=>DeskSpaceFolderZipV159.CreateFromFolder(source,zipPath));'
$after=@'
                // Each folder drop owns its own cancellation and progress;
                // only a fully verified ZIP is handed to the existing Deck.
                using(var cancelZip=new System.Threading.CancellationTokenSource())
                {
                    var progressWindow=DeskSpaceZipProgressWindowV206.Show(host,cancelZip);
                    try
                    {
                        var reporter=new Progress<DeskSpaceFolderZipV206.Progress>(
                            item=>progressWindow.Report(item));
                        await Task.Run(()=>DeskSpaceFolderZipV206.Create(
                            source,zipPath,reporter,cancelZip.Token));
                    }
                    finally{progressWindow.Close();}
                }
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R206 folder compression call expected once, found "+$n)}
$s=$s.Replace($before,$after)
$before=@'
            catch(Exception ex)
            {
                MessageBox.Show("Could not ZIP the folder: "+ex.Message,
'@.Replace($crlf,$lf)
$after=@'
            catch(System.OperationCanceledException)
            {
                // Cancellation leaves no completed ZIP, partial or Deck item.
            }
            catch(Exception ex)
            {
                MessageBox.Show("Could not ZIP the folder: "+ex.Message,
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R206 error handling expected once, found "+$n)}
$s=$s.Replace($before,$after)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R206: ZIP operation now cancellable with progress; final Deck staging remains unchanged.'
