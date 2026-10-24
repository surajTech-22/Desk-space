$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r99/DeskSpaceRecentFileStateV99.cs' "$root/DeskSpaceRecentFileStateV99.cs" -Force
Copy-Item 'r99/DeskSpaceDeckPreviewV99.cs' "$root/DeskSpaceDeckPreviewV99.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Swap([string]$before,[string]$after,[string]$label)
{
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($n -ne 1){throw "R99 $label integration anchor count $n"}
    $script:s=$script:s.Replace($before,$after)
}
Swap @'
    private bool deckMinimized,deckDismissed,appRenderPending;
'@ @'
    private bool deckMinimized,deckDismissed,appRenderPending;
    private bool previewOnHover;
    private DeskSpaceDeckPreviewV99? deckPreview;
'@ 'recent preview fields'
Swap @'
        if(disposed||!File.Exists(path))return;
        // Register the name/path with Search before any Search window opens.
'@ @'
        if(disposed||!File.Exists(path))return;
        // Previously dismissed version stays dismissed across app restarts.
        // A newly saved version with a different timestamp/size is eligible.
        if(DeskSpaceRecentFileStateV99.IsDismissed(path))return;
        // Register the name/path with Search before any Search window opens.
'@ 'version-aware recent watcher events'
Swap @'
        if(item.AutoDetected)
            detected.Remove(item); // Never delete the user's original file
'@ @'
        if(item.AutoDetected)
        {
            DeskSpaceRecentFileStateV99.Dismiss(item.StagedPath);
            detected.Remove(item); // Never delete the user's original file
            if(string.Equals(deckPreview?.CurrentPath,item.StagedPath,
                StringComparison.OrdinalIgnoreCase))deckPreview?.Close();
        }
'@ 'remember single recent dismissal'
Swap @'
        if(disposed)return;
        list.Children.Clear();
'@ @'
        if(disposed)return;
        deckPreview ??= new DeskSpaceDeckPreviewV99(panel);
        deckPreview.CloseIfMissing();
        list.Children.Clear();
'@ 'persistent deck preview outside row redraws'
Swap @'
            dismissedNonAppSignature=NonAppSignature();
            deckDismissed=true;
            popup.IsOpen=false;
'@ @'
            dismissedNonAppSignature=NonAppSignature();
            deckDismissed=true;
            deckPreview?.Close();
            popup.IsOpen=false;
'@ 'explicit deck close also closes detached preview'
Swap @'
        if(!visible)
        {
            popup.IsOpen=false;
            runningAppsTimer.Stop();
'@ @'
        if(!visible)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
            runningAppsTimer.Stop();
'@ 'manual deck visibility off closes preview'
Swap @'
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            popup.IsOpen=false;
            return;
        }
'@ @'
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
            return;
        }
'@ 'hidden deck never leaves floating preview'
Swap @'
        if(deckMinimized)
        {
            popup.IsOpen=host.IsVisible;
            return;
        }
'@ @'
        if(deckMinimized)
        {
            deckPreview?.Close();
            popup.IsOpen=host.IsVisible;
            return;
        }
'@ 'minimising deck closes only file preview'
Swap @'
        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",
'@ @'
        var previewMode=DeckHeaderButton(previewOnHover?"◉":"◎",
            previewOnHover?"Disable file preview on hover":
                "Preview files on hover (or use each row's eye button)",()=>{
                previewOnHover=!previewOnHover;
                if(!previewOnHover)deckPreview?.Close();
                Render();
            });
        DockPanel.SetDock(previewMode,Dock.Right);
        title.Children.Add(previewMode);
        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",
'@ 'compact deck-level preview mode'
Swap @'
            dismissRecent.Click+=(_,_)=>{detected.Clear();Render();};
'@ @'
            dismissRecent.Click+=(_,_)=>{
                DeskSpaceRecentFileStateV99.DismissMany(
                    detected.Select(x=>x.StagedPath).ToArray());
                deckPreview?.Close();
                detected.Clear();Render();
            };
'@ 'remember dismiss-all without deleting originals'
Swap @'
            DeskSpaceFilePreviewV85.Attach(previewFile,item.StagedPath);
'@ @'
            previewFile.Foreground=Brushes.LightSteelBlue;
            previewFile.Background=Brushes.Transparent;
            previewFile.BorderThickness=new Thickness(0);
            previewFile.ToolTip="Preview file in the compact deck";
            deckPreview?.Bind(previewFile,row,item.StagedPath,()=>previewOnHover);
'@ 'stable deck anchored file preview'
Swap @'
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
'@ @'
            // Open the original location, without importing or moving data.
            if(item.AutoDetected)
            {
                var holdCopy=new Button{Content="+",FontSize=13,MinWidth=21,
                    Foreground=Brushes.LightSteelBlue,
                    Background=Brushes.Transparent,BorderThickness=new Thickness(0),
                    Padding=new Thickness(1),
                    ToolTip="Hold a copy of this recently saved file"};
                holdCopy.Click+=(sender,args)=>{
                    _=StageFilesAsync(new[]{item.StagedPath});
                };
                DockPanel.SetDock(holdCopy,Dock.Right);
                content.Children.Add(holdCopy);
            }
            var locate=new Button{Content="⌕",FontSize=13,MinWidth=22,
                Foreground=Brushes.LightSteelBlue,
                Background=Brushes.Transparent,BorderThickness=new Thickness(0),
                Padding=new Thickness(1),
                ToolTip="Show this file in File Explorer"};
            locate.Click+=(_,_)=>DeskSpaceDeckPreviewV99.OpenContainingFolder(item.StagedPath);
            DockPanel.SetDock(locate,Dock.Right);
            content.Children.Add(locate);
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
'@ 'recent hold and reveal actions'
Swap @'
        runningAppsTimer.Stop();
        DeskSpaceDeckVisibilityV89.Changed-=DeckVisibilityChanged;
'@ @'
        runningAppsTimer.Stop();
        deckPreview?.Dispose();
        deckPreview=null;
        DeskSpaceDeckVisibilityV89.Changed-=DeckVisibilityChanged;
'@ 'clean detached preview on widget exit'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R99: version-aware recent dismissals, quick hold/reveal, stable deck file preview, optional hover preview mode. Original file bytes untouched.'
