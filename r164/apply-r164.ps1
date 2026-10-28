$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r164/DeskSpaceFolderCopyV164.cs' "$root/DeskSpaceFolderCopyV164.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($count -ne 1){throw "R164 $why requires exactly 1 anchor, found $count"}
    $script:s=$script:s.Replace($before,$after)
}

Swap '    private static bool zipInProgress;' @'
    private static bool zipInProgress;
    // A single shared copy transaction is visible and cancellable from
    // either monitor's Deck. Closing a widget does not discard shared data.
    private static bool copyInProgress;
    private static CancellationTokenSource? copyCancellation;
    private static long copyBytesWritten,copyBytesTotal;
    private static int copyFilesWritten,copyFilesTotal;
'@ 'shared copy cancellation and progress'

Swap @'
        if(zipInProgress)
        {
'@ @'
        if(copyInProgress)
        {
            var copyRow=new DockPanel{
                Margin=new Thickness(5,2,5,4),LastChildFill=true};
            var cancelCopy=new Button{Content="Cancel Copy",FontSize=10,
                ToolTip="Cancel staging; source files and existing Deck items remain unchanged",
                Padding=new Thickness(6,2,6,2),MinWidth=80};
            cancelCopy.Click+=(_,_)=>copyCancellation?.Cancel();
            DockPanel.SetDock(cancelCopy,Dock.Right);
            copyRow.Children.Add(cancelCopy);
            var copiedMiB=copyBytesWritten/(1024.0*1024.0);
            var totalMiB=copyBytesTotal/(1024.0*1024.0);
            copyRow.Children.Add(new TextBlock{
                Text=$"Copy: {copyFilesWritten}/{copyFilesTotal} files · "+
                    $"{copiedMiB:F1}/{totalMiB:F1} MiB",
                Foreground=Brushes.White,FontSize=11,
                TextTrimming=TextTrimming.CharacterEllipsis,
                VerticalAlignment=VerticalAlignment.Center
            });
            list.Children.Add(copyRow);
        }
        if(zipInProgress)
        {
'@ 'show one shared Deck cancel and measured progress'

Swap @'
                await Task.Run(()=>{
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if(File.Exists(source))File.Copy(source,destination,false);
                    else CopyFolder(source,destination);
                });
'@ @'
                using var cancellation=new CancellationTokenSource();
                copyCancellation=cancellation;
                copyInProgress=true;
                copyBytesWritten=0;copyBytesTotal=0;
                copyFilesWritten=0;copyFilesTotal=0;
                RefreshViews();
                try
                {
                    // The copy worker writes only to a GUID-owned *.partial
                    // path and atomically renames it when completely copied.
                    await Task.Run(()=>DeskSpaceFolderCopyV164.Copy(
                        source,destination,cancellation.Token,
                        (written,total,filesCopied,filesTotal)=>{
                            host.Dispatcher.BeginInvoke(new Action(()=>{
                                if(copyInProgress&&ReferenceEquals(
                                    copyCancellation,cancellation))
                                {
                                    copyBytesWritten=written;copyBytesTotal=total;
                                    copyFilesWritten=filesCopied;
                                    copyFilesTotal=filesTotal;
                                    RefreshViews();
                                }
                            }));
                        }));
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                catch
                {
                    // A cancellation can arrive after atomic rename but
                    // before the Deck adopts the item. Remove only that copy.
                    DeletePath(destination);
                    throw;
                }
                finally
                {
                    if(ReferenceEquals(copyCancellation,cancellation))
                    {
                        copyCancellation=null;
                        copyInProgress=false;
                        RefreshViews();
                    }
                }
'@ 'replace blocking Copy with streamed partial-then-rename worker'

Swap @'
        catch(Exception ex)
        {
            MessageBox.Show("Could not hold the file: "+ex.Message,
'@ @'
        catch(OperationCanceledException)
        {
            // An explicit cancellation is not an error dialog.
            System.Diagnostics.Debug.WriteLine("DeskSpace Deck copy cancelled");
        }
        catch(Exception ex)
        {
            MessageBox.Show("Could not hold the file: "+ex.Message,
'@ 'silent intentional cancellation, retain real errors'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R164: all Deck copy operations stream to partial staging, show shared progress and allow safe cancellation.'
