$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r162/DeskSpaceFolderZipV162.cs' "$root/DeskSpaceFolderZipV162.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R162 $why requires 1 anchor, got $count"}
 $script:s=$script:s.Replace($before,$after)
}
Swap 'using System.Threading.Tasks;' @'
using System.Threading.Tasks;
using System.Threading;
'@ 'cancellation token import'
Swap '    private static bool busy;' @'
    private static bool busy;
    private static bool zipInProgress;
    private static int zipFilesWritten;
    private static CancellationTokenSource? zipCancellation;
    // A temporary archive has a different path from the ORIGINAL folder.
    // The origin must be shared across Copy and ZIP dedup paths.
    private static readonly Dictionary<string,string> pendingZipSources=
        new(StringComparer.OrdinalIgnoreCase);
'@ 'shared ZIP transaction flags and source mapping'

Swap @'
    static DeskSpaceDropShelfV15()
    {
'@ @'
    static DeskSpaceDropShelfV15()
    {
        // Only prune app-owned GUID temporary ZIP directories older than one
        // day, never active ZIPs, user files or the held-file staging path.
        _=Task.Run(()=>{
            try{DeskSpaceFolderZipV162.PruneAbandoned(
                Path.Combine(Path.GetTempPath(),"DeskSpaceFolderZip"),
                TimeSpan.FromHours(24));}
            catch(Exception error){System.Diagnostics.Debug.WriteLine(
                "ZIP temporary cleanup skipped: "+error.GetType().Name);}
        });
'@ 'non-invasive stale interrupted ZIP cleanup'

Swap '                if(AlreadyHeldSource(source))continue;' @'
                var originalSource=pendingZipSources.TryGetValue(source,
                    out var remembered)?remembered:source;
                if(AlreadyHeldSource(originalSource))continue;
'@ 'avoid ZIP and Copy duplicates using original source'
Swap '                originalByStaged[destination]=Path.GetFullPath(source);' @'
                originalByStaged[destination]=Path.GetFullPath(originalSource);
'@ 'track true originating folder for staged ZIP'

Swap @'
        list.Children.Clear();
'@ @'
        list.Children.Clear();
        if(zipInProgress)
        {
            // Display this transaction in the existing shared Deck, never
            // as a floating popup outside the circle.
            var progressRow=new DockPanel{
                Margin=new Thickness(5,2,5,4),LastChildFill=true};
            var cancel=new Button{Content="Cancel ZIP",FontSize=10,
                ToolTip="Cancel ZIP safely; the original folder is unchanged",
                Padding=new Thickness(6,2,6,2),MinWidth=75};
            cancel.Click+=(_,_)=>zipCancellation?.Cancel();
            DockPanel.SetDock(cancel,Dock.Right);progressRow.Children.Add(cancel);
            progressRow.Children.Add(new TextBlock{
                Text="ZIP: "+zipFilesWritten+" files",
                Foreground=Brushes.White,FontSize=11,
                VerticalAlignment=VerticalAlignment.Center
            });
            list.Children.Add(progressRow);
        }
'@ 'shared Deck ZIP progress and cancel button'

Swap @'
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
    {
        if(!zipFolders)
        {
            // Copy uses the existing folder holding path unchanged.
            await StageFilesAsync(paths);
            return;
        }
        foreach(var source in paths)
        {
            // An ordinary file in a mixed drop is still held as a file.
            if(!Directory.Exists(source))
            {
                await StageFilesAsync(new[]{source});
                continue;
            }
            var temporary=Path.Combine(Path.GetTempPath(),
                "DeskSpaceFolderZip",Guid.NewGuid().ToString("N"));
            var zipPath=Path.Combine(temporary,DeskSpaceFolderZipV159.ZipName(source));
            try
            {
                // Block other drop operations while ZIP creation is underway.
                // StageFilesAsync then performs its existing shared-Deck copy.
                busy=true;
                RefreshViews();
                await Task.Run(()=>DeskSpaceFolderZipV159.CreateFromFolder(source,zipPath));
                busy=false;
                RefreshViews();
                await StageFilesAsync(new[]{zipPath});
            }
            catch(Exception ex)
            {
                MessageBox.Show("Could not ZIP the folder: "+ex.Message,
                    "DeskSpace folder ZIP",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            finally
            {
                if(busy){busy=false;RefreshViews();}
                try{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
'@ @'
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
    {
        if(!zipFolders)
        {
            // Copy keeps the existing folder staging path unchanged.
            await StageFilesAsync(paths);
            return;
        }
        foreach(var source in paths)
        {
            if(!Directory.Exists(source))
            {
                await StageFilesAsync(new[]{source});
                continue;
            }
            // Both ZIP and Copy count as holding the same original source.
            if(AlreadyHeldSource(source))continue;
            if(busy)return;
            var temporary=Path.Combine(Path.GetTempPath(),
                "DeskSpaceFolderZip",Guid.NewGuid().ToString("N"));
            var zipPath=Path.Combine(temporary,DeskSpaceFolderZipV159.ZipName(source));
            using var cancellation=new CancellationTokenSource();
            try
            {
                zipCancellation=cancellation;
                zipFilesWritten=0;
                zipInProgress=true;
                busy=true;
                RefreshViews();
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
                cancellation.Token.ThrowIfCancellationRequested();
                zipInProgress=false;
                zipCancellation=null;
                busy=false;
                RefreshViews();
                pendingZipSources[zipPath]=source;
                try{await StageFilesAsync(new[]{zipPath});}
                finally{pendingZipSources.Remove(zipPath);}
            }
            catch(OperationCanceledException)
            {
                // A cancellation is normal; R162's writer removes .partial.
                System.Diagnostics.Debug.WriteLine("DeskSpace ZIP cancelled");
            }
            catch(Exception ex)
            {
                MessageBox.Show("Could not ZIP the folder: "+ex.Message,
                    "DeskSpace folder ZIP",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            finally
            {
                if(ReferenceEquals(zipCancellation,cancellation))
                    zipCancellation=null;
                if(zipInProgress){zipInProgress=false;busy=false;RefreshViews();}
                try{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
'@ 'cancellable ZIP streaming and guaranteed origin cleanup'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R162: cancellable ZIP progress in shared Deck, original folder dedup across ZIP/Copy, and cautious stale ZIP cleanup.'
