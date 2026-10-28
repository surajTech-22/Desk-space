$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r161/DeskSpaceDiagnosticsV161.cs' -Destination "$root/DeskSpaceDiagnosticsV161.cs" -Force
Copy-Item -LiteralPath 'r161/DeskSpaceArchivePreviewV161.cs' -Destination "$root/DeskSpaceArchivePreviewV161.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$what)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R161 $what expected 1 anchor, found $count"}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$widget="$root/WidgetHost.cs"
Swap $widget '        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{' @'
        // Diagnostics are opt-in and generated ONLY when explicitly clicked.
        DeskSpaceDiagnosticsV161.Install();
        menu.Items.Add("Export privacy-safe diagnostics...",null,(_,_)=>Dispatcher.Invoke(()=>{
            try
            {
                var archive=DeskSpaceDiagnosticsV161.ExportToDownloads();
                MessageBox.Show(this,"Diagnostics ZIP saved to Downloads:"+
                    Environment.NewLine+System.IO.Path.GetFileName(archive)+
                    Environment.NewLine+"No private notes, passwords, clipboard or Excel data included.",
                    "DeskSpace diagnostics",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception error)
            {
                MessageBox.Show(this,"Diagnostics could not be exported ("+
                    error.GetType().Name+").","DeskSpace",
                    MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }));
        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{
'@ 'add opt-in diagnostics in the existing tray menu'

$preview="$root/DeskSpaceFilePreviewV85.cs"
Swap $preview @'
            if(Images.Contains(extension) && info.Length<=40*1024*1024)
'@ @'
            if(extension==".zip")
            {
                // Inspect ZIP entry names only; never extract or execute files.
                panel.Children.Add(DeskSpaceArchivePreviewV161.Create(path));
            }
            else if(Images.Contains(extension) && info.Length<=40*1024*1024)
'@ 'inline ZIP contents in existing DeskSpace file preview'

$deck="$root/DeskSpaceDropShelfV15.cs"
Swap $deck '    private static readonly List<Held> held=new();' @'
    private static readonly List<Held> held=new();
    // Source-path deduplication applies only to staged copies, not external
    // recent-file discovery. Paths are never persisted or exported.
    private static readonly Dictionary<string,string> originalByStaged=
        new(StringComparer.OrdinalIgnoreCase);
    internal static bool AlreadyHeldSource(string source)
    {
        var canonical=Path.GetFullPath(source);
        return originalByStaged.Values.Any(original=>
            string.Equals(original,canonical,StringComparison.OrdinalIgnoreCase));
    }
'@ 'avoid staging same original source twice in either monitor'
Swap $deck @'
                var id=Guid.NewGuid().ToString("N");
'@ @'
                // The shared Deck is one queue across widgets: another monitor
                // must not duplicate files already held from the same source.
                if(AlreadyHeldSource(source))continue;
                var id=Guid.NewGuid().ToString("N");
'@ 'protect shared Deck against repeated folder/file drops'
Swap $deck @'
                held.Add(new Held(Path.GetFileName(destination),destination));
'@ @'
                held.Add(new Held(Path.GetFileName(destination),destination));
                originalByStaged[destination]=Path.GetFullPath(source);
'@ 'record original only after a completed staged copy'
Swap $deck @'
            if(!held.Remove(item))return;
            DeletePath(item.StagedPath);
'@ @'
            if(!held.Remove(item))return;
            originalByStaged.Remove(item.StagedPath);
            DeletePath(item.StagedPath);
'@ 'remove dedup entry on paste or removal, preserving future restaging'
Write-Host 'R161 installed: privacy-safe diagnostic export, ZIP Deck preview, source-path duplicate guard.'
