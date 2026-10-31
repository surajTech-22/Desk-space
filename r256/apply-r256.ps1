$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r256/DeskSpaceFolderPreDropV256.cs' -Destination "$root/DeskSpaceFolderPreDropV256.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R256 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
    private string[]? pendingFolderDrop;
'@ @'
    private string[]? pendingFolderDrop;
    private DeskSpaceFolderPreDropV256.Choice choiceV256;
    private System.Windows.Threading.DispatcherTimer? expiryV256;
    private DateTime lastDragV256;
'@ 'choice and scoped idle cleanup'
'
'
Swap @'
        host.IsVisibleChanged+=FolderChoiceVisible;
'@ @'
        // Popup has its own HWND. Listen on the owning widget AND popup,
        // before WPF dispatches the original Drop event.
        card.AllowDrop=true;
        card.PreviewDragEnter+=PreEnterV256;
        card.PreviewDragOver+=PreOverV256;
        card.PreviewDrop+=PreDropV256;
        host.PreviewDragEnter+=PreEnterV256;
        host.PreviewDragOver+=PreOverV256;
        host.PreviewDrop+=PreDropV256;
        host.IsVisibleChanged+=FolderChoiceVisible;
'@ 'drag handlers before release'
Swap @'
    private void Receive(object sender,DragEventArgs e)
    {
'@ @'
    private void Receive(object sender,DragEventArgs e)
    {
        // Ignore unapproved native folder drops; only PreviewDrop can
        // commit a hover-selected action. No post-drop prompt is shown.
        if(FolderFilesV256(e)!=null)
        {
            HideFolderChoice();
            e.Effects=DragDropEffects.None;
            e.Handled=true;
            return;
        }
'@ 'remove post-drop chooser'
Swap @'
    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
'@ @'
    private static string[]? FolderFilesV256(DragEventArgs e)
    {
        try
        {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop))return null;
            var paths=e.Data.GetData(DataFormats.FileDrop) as string[];
            return paths?.Any(Directory.Exists)==true?paths:null;
        }
        catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException
            or IOException or UnauthorizedAccessException or ArgumentException)
        { return null; }
    }
    private Button? FindChoiceV256(string caption)
    {
        if(folderChoice.Child is Border border &&
           border.Child is StackPanel panel)
        {
            foreach(var group in panel.Children.OfType<StackPanel>())
                foreach(var button in group.Children.OfType<Button>())
                    if(string.Equals(button.Content?.ToString(),caption,
                        StringComparison.OrdinalIgnoreCase))
                        return button;
        }
        return null;
    }
    private static bool OverV256(Button? button,DragEventArgs e)
    {
        if(button==null||button.ActualWidth<=0||button.ActualHeight<=0)
            return false;
        var p=e.GetPosition(button);
        return p.X>=0&&p.Y>=0&&p.X<button.ActualWidth&&p.Y<button.ActualHeight;
    }
    private void MarkV256(DeskSpaceFolderPreDropV256.Choice choice)
    {
        choiceV256=choice;
        var zip=FindChoiceV256("ZIP");
        var copy=FindChoiceV256("Copy");
        if(zip!=null)
            zip.Background=choice==DeskSpaceFolderPreDropV256.Choice.Zip?
                Brushes.LightSkyBlue:SystemColors.ControlBrush;
        if(copy!=null)
            copy.Background=choice==DeskSpaceFolderPreDropV256.Choice.Copy?
                Brushes.LightSkyBlue:SystemColors.ControlBrush;
    }
    private void PreEnterV256(object sender,DragEventArgs e)
    {
        var paths=FolderFilesV256(e);
        if(paths==null||busy)return;
        if(pendingFolderDrop==null)ShowFolderChoice(paths);
        lastDragV256=DateTime.UtcNow;
        e.Effects=DragDropEffects.Copy;
        e.Handled=true;
    }
    private void PreOverV256(object sender,DragEventArgs e)
    {
        var paths=FolderFilesV256(e);
        if(paths==null||busy)return;
        if(pendingFolderDrop==null)ShowFolderChoice(paths);
        lastDragV256=DateTime.UtcNow;
        MarkV256(DeskSpaceFolderPreDropV256.Hover(
            OverV256(FindChoiceV256("ZIP"),e),OverV256(FindChoiceV256("Copy"),e)));
        e.Effects=DragDropEffects.Copy;
        e.Handled=true;
    }
    private void PreDropV256(object sender,DragEventArgs e)
    {
        var paths=FolderFilesV256(e);
        if(paths==null)return;
        bool approved=DeskSpaceFolderPreDropV256.Authorized(
            choiceV256,pendingFolderDrop,paths);
        bool zip=choiceV256==DeskSpaceFolderPreDropV256.Choice.Zip;
        if(approved)CommitFolderDrop(zip);
        else HideFolderChoice();
        e.Effects=approved?DragDropEffects.Copy:DragDropEffects.None;
        e.Handled=true;
    }
    private void WatchDragV256()
    {
        if(expiryV256==null)
        {
            expiryV256=new System.Windows.Threading.DispatcherTimer{
                Interval=TimeSpan.FromSeconds(1)};
            expiryV256.Tick+=(_,_)=>{
                if(pendingFolderDrop==null){expiryV256?.Stop();return;}
                // Explorer drag cancellation across popup HWNDs may omit
                // DragLeave. Remove only abandoned consent, not held files.
                if(DateTime.UtcNow-lastDragV256>TimeSpan.FromSeconds(12))
                    HideFolderChoice();
            };
        }
        lastDragV256=DateTime.UtcNow;
        expiryV256.Start();
    }

    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
'@ 'preview hover, consent and idle recovery'
Swap @'
        pendingFolderDrop=valid;
        PositionFolderChoice();
        folderChoice.IsOpen=host.IsVisible;
'@ @'
        pendingFolderDrop=valid;
        MarkV256(DeskSpaceFolderPreDropV256.Choice.None);
        PositionFolderChoice();
        folderChoice.IsOpen=host.IsVisible;
        WatchDragV256();
'@ 'only while active drag'
Swap @'
        pendingFolderDrop=null;
        folderChoice.IsOpen=false;
'@ @'
        pendingFolderDrop=null;
        MarkV256(DeskSpaceFolderPreDropV256.Choice.None);
        expiryV256?.Stop();
        folderChoice.IsOpen=false;
'@ 'clear consent after drag'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R256: folder ZIP/Copy option shown during native drag; only preselected choice commits on drop.'
