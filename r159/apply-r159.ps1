$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=[string][char]13+$lf
Copy-Item 'r159/DeskSpaceFolderZipV159.cs' "$r/DeskSpaceFolderZipV159.cs" -Force
$p="$r/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf)
 $after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R159 $why expected one anchor, found $count"}
 $script:s=$script:s.Replace($before,$after)
}

Swap '    private readonly Popup popup;' @'
    private readonly Popup popup;
    // A choice is owned by the widget that received the drag. Only the
    // completed staged result is shared with other monitor Deck views.
    private readonly Popup folderChoice;
    private string[]? pendingFolderDrop;
'@ 'choice is a view-local visual surface'

Swap @'
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
    }
'@ @'
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
        var choicePanel=new StackPanel{Margin=new Thickness(10,8,10,9)};
        var heading=new TextBlock{
            Text="Dropped folder",FontSize=12,
            FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,
            HorizontalAlignment=HorizontalAlignment.Center};
        var hint=new TextBlock{
            Text="ZIP or copy the folder?",FontSize=11,
            Foreground=new SolidColorBrush(Color.FromRgb(213,221,245)),
            Margin=new Thickness(0,3,0,8),TextAlignment=TextAlignment.Center};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,
            HorizontalAlignment=HorizontalAlignment.Center};
        var zip=new Button{Content="ZIP",MinWidth=65,MinHeight=27,
            FontWeight=FontWeights.SemiBold,ToolTip="Compress folder and hold ZIP file",
            Margin=new Thickness(0,0,7,0),Padding=new Thickness(6,3,6,3)};
        var copyFolder=new Button{Content="Copy",MinWidth=65,MinHeight=27,
            ToolTip="Hold original folder structure without compression",
            Padding=new Thickness(6,3,6,3)};
        zip.Click+=(_,_)=>CommitFolderDrop(true);
        copyFolder.Click+=(_,_)=>CommitFolderDrop(false);
        actions.Children.Add(zip);actions.Children.Add(copyFolder);
        var dismiss=new Button{Content="×",FontSize=12,
            MinWidth=22,Height=22,HorizontalAlignment=HorizontalAlignment.Right,
            Margin=new Thickness(0,-5,-6,-5),Background=Brushes.Transparent,
            Foreground=Brushes.White,BorderThickness=new Thickness(0),
            ToolTip="Cancel folder drop"};
        dismiss.Click+=(_,_)=>HideFolderChoice();
        choicePanel.Children.Add(dismiss);
        choicePanel.Children.Add(heading);
        choicePanel.Children.Add(hint);
        choicePanel.Children.Add(actions);
        var card=new Border{Width=190,
            CornerRadius=new CornerRadius(13),
            Padding=new Thickness(2),
            BorderThickness=new Thickness(1),
            BorderBrush=new SolidColorBrush(Color.FromRgb(156,176,238)),
            Background=new SolidColorBrush(Color.FromArgb(248,24,34,66)),
            Child=choicePanel};
        folderChoice=new Popup{
            PlacementTarget=host,Placement=PlacementMode.Relative,
            StaysOpen=true,AllowsTransparency=true,
            Child=card
        };
        DeskSpaceMonitorFenceV153.TrackPopup(host,folderChoice);
        host.IsVisibleChanged+=FolderChoiceVisible;
    }
'@ 'add centered ZIP and Copy choice on the owning circle'

Swap @'
        if(!popup.IsOpen)return;
'@ @'
        if(folderChoice.IsOpen)PositionFolderChoice();
        if(!popup.IsOpen)return;
'@ 'keep choice centered when widget morphs/moves'

Swap @'
        var files=e.Data.GetData(DataFormats.FileDrop) as string[];
        if(files?.Length>0)_=StageFilesAsync(files);
'@ @'
        var files=e.Data.GetData(DataFormats.FileDrop) as string[];
        if(files?.Length>0 && !busy && pendingFolderDrop==null)
        {
            // Only FOLDERS dropped into the donut/Deck need an action.
            // Normal dropped files keep the existing direct-hold behavior.
            if(files.Any(Directory.Exists))ShowFolderChoice(files);
            else _=StageFilesAsync(files);
        }
'@ 'route folder drops to choice and regular files to prior path'

Swap @'
    private void ImportFromClipboard()
'@ @'
    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!(bool)e.NewValue)HideFolderChoice();
    }
    private void PositionFolderChoice()
    {
        // The prompt appears over the current circle, not on another screen.
        var width=host.ActualWidth>0?host.ActualWidth:host.Width;
        var height=host.ActualHeight>0?host.ActualHeight:host.Height;
        folderChoice.HorizontalOffset=Math.Max(0,(width-190)/2);
        folderChoice.VerticalOffset=Math.Max(0,(height-112)/2);
    }
    private void ShowFolderChoice(string[] incoming)
    {
        if(disposed||busy||pendingFolderDrop!=null)return;
        var valid=incoming.Where(path=>
            (File.Exists(path)||Directory.Exists(path))&&
            !IsOwnPath(path,staging)).ToArray();
        if(valid.Length==0)return;
        pendingFolderDrop=valid;
        PositionFolderChoice();
        folderChoice.IsOpen=host.IsVisible;
    }
    private void HideFolderChoice()
    {
        pendingFolderDrop=null;
        folderChoice.IsOpen=false;
    }
    private void CommitFolderDrop(bool zipFolders)
    {
        var paths=pendingFolderDrop;
        HideFolderChoice();
        if(disposed||paths==null||paths.Length==0)return;
        _=StageFilesAsync(paths,zipFolders);
    }
    private void ImportFromClipboard()
'@ 'choice cancel, presentation and option handler'

Swap @'
    private async Task StageFilesAsync(string[] files)
'@ @'
    private async Task StageFilesAsync(string[] files,bool zipFolders=false)
'@ 'folder ZIP option is passed into existing shared stage routine'

Swap @'
                var destination=Path.Combine(staging,id,Path.GetFileName(source.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)));
                await Task.Run(()=>{
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if(File.Exists(source))File.Copy(source,destination,false);
                    else CopyFolder(source,destination);
                });
'@ @'
                var folder=Directory.Exists(source);
                var targetName=folder&&zipFolders?
                    DeskSpaceFolderZipV159.ZipName(source):
                    Path.GetFileName(source.TrimEnd(
                        Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
                var destination=Path.Combine(staging,id,targetName);
                try
                {
                    await Task.Run(()=>{
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        if(folder&&zipFolders)
                            DeskSpaceFolderZipV159.CreateFromFolder(source,destination);
                        else if(File.Exists(source))
                            File.Copy(source,destination,false);
                        else CopyFolder(source,destination);
                    });
                }
                catch
                {
                    DeletePath(destination); // never retain partial archives
                    throw;
                }
'@ 'ZIP creates one file; Copy stages the folder directly'

Swap @'
        popup.IsOpen=false;
        host.PreviewDragOver-=DragOver;
'@ @'
        HideFolderChoice();
        host.IsVisibleChanged-=FolderChoiceVisible;
        popup.IsOpen=false;
        host.PreviewDragOver-=DragOver;
'@ 'per-widget prompt closes without touching the shared held files'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R159: folder drops show ZIP or Copy over the owning circle; regular files unchanged, shared Deck unchanged.'
