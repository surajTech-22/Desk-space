$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Read([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$a,[string]$b)
{
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R59 patch anchor $n : $a"}
 $script:s=$script:s.Replace($a,$b)
}
# Keep the existing R55/R58 source copies as the single authority.
Copy-Item -LiteralPath 'r55/DeskSpaceNewFileWatchV55.cs' -Destination (Join-Path $root 'DeskSpaceNewFileWatchV53.cs') -Force
Copy-Item -LiteralPath 'r55/DeskSpaceApplicationsV55.cs' -Destination (Join-Path $root 'DeskSpaceApplicationsV55.cs') -Force

# Add user-initiated monitoring of nonstandard save locations through a
# circle context-menu command. No registry hooks, scheduled scans of C:,
# or automatic reading/copying of detected document contents.
$p=Join-Path $root 'DeskSpaceDropShelfV15.cs';$s=Read $p
One '    public static void ClearActive()=>active?.ClearAll();' @'
    public static void ClearActive()=>active?.ClearAll();
    public static void SelectWatchedFolder()
    {
        var self=active;
        if(self==null||self.disposed)return;
        using var dialog=new System.Windows.Forms.FolderBrowserDialog{
            Description="Select an ordinary work folder for recent saved files",
            ShowNewFolderButton=false
        };
        if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
        try
        {
            string folder=Path.GetFullPath(dialog.SelectedPath);
            if(!DeskSpaceNewFileWatchV53.IsAllowedWatchFolder(folder))
            {
                MessageBox.Show("Only ordinary work folders can be monitored. " +
                    "Hidden, system, application and cache folders are excluded.",
                    "DeskSpace saved files",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }
            var store=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace","RecentFileFolders.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(store)!);
            var entries=File.Exists(store)?File.ReadAllLines(store):Array.Empty<string>();
            if(!entries.Contains(folder,StringComparer.OrdinalIgnoreCase))
                File.AppendAllLines(store,new[]{folder});
            self.newFiles?.Dispose();
            self.newFiles=new DeskSpaceNewFileWatchV53(self.host,
                self.OfferNewFile,onRemoved:self.RemoveRecentFile);
            self.newFiles.Start();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Cannot monitor that folder: "+ex.Message,
                "DeskSpace saved files",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
'@
Save $p $s

$p=Join-Path $root 'WidgetHost.cs';$s=Read $p
One @'
        menu.Items.Add("Excel Functions · Clipboard / Information",null,(_,_)=>Dispatcher.Invoke(
            OpenExcelArc));
'@ @'
        menu.Items.Add("Excel Functions · Clipboard / Information",null,(_,_)=>Dispatcher.Invoke(
            OpenExcelArc));
        menu.Items.Add("Watch folder for saved files...",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceDropShelfV15.SelectWatchedFolder));
'@
Save $p $s
Write-Host 'R59 user folder scope, deduplicated application saves and concentric Applications arcs integrated.'
