$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $file)){throw 'R363 missing shipping Deck source'}
if(!(Test-Path -LiteralPath 'r363/DeskSpaceFolderAppRecognitionV363.cs')){
    throw 'R363 recognition source missing'
}
$s=[IO.File]::ReadAllText($file)
$lf=[string][char]10
$s=$s.Replace(([string][char]13+$lf),$lf)
function One([string]$old,[string]$replacement,[string]$what){
    $old=$old.Replace(([string][char]13+$script:lf),$script:lf)
    $replacement=$replacement.Replace(([string][char]13+$script:lf),$script:lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R363 $what expected exactly one shipping anchor, found $count"}
    $script:s=$script:s.Replace($old,$replacement)
}
One @'
    private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();
'@ @'
    private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();
    // Application matches are session-only shared Deck suggestions.
    // No folder contents are copied to this state, and no app is launched
    // during a drag, hover, ZIP, Copy or automatic update.
    private static IReadOnlyList<DeskSpaceFolderAppRecognitionV363.Suggestion>
        recognizedFolderV363=Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
    private static string? recognizedFolderErrorV363;
    private static int recognizedFolderEpochV363;
'@ 'session-only detected project suggestions'
One @'
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath)
            .Concat(excelMatchesV122.Select(x=>x.Id.ToString())));
'@ @'
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath)
            .Concat(excelMatchesV122.Select(x=>x.Id.ToString()))
            .Concat(recognizedFolderV363.Select(x=>
                x.Folder+"|"+x.AppId+"|"+recognizedFolderEpochV363)));
'@ 'folder open suggestion counts as a user-initiated non-app Deck action'
# Published R232/R359 shipping and newer checked-in development source
# have different, valid ZIP/Copy commits. Attach only at whichever is
# actually present and never replace their existing authorization rules.
if($s.Contains('if(!busy && approved)'))
{
    One @'
                if(!busy && approved)
                    _=CommitFolderDropAsync(dropped,zip);
'@ @'
                if(!busy && approved)
                {
                    SuggestFolderAppsV363(dropped);
                    _=CommitFolderDropAsync(dropped,zip);
                }
'@ 'newer authorized ZIP/Copy drop'
}
elseif($s.Contains('if(!busy&&selected is bool zip && sources.Length>0)'))
{
    One @'
                if(!busy&&selected is bool zip && sources.Length>0)
                    _=CommitFolderDropAsync(sources,zip);
'@ @'
                if(!busy&&selected is bool zip && sources.Length>0)
                {
                    SuggestFolderAppsV363(sources);
                    _=CommitFolderDropAsync(sources,zip);
                }
'@ 'published R232 ZIP/Copy drop'
}
else {throw 'R363 unrecognized shipping ZIP/Copy drop; no integration performed'}
One @'
    private void FolderDragEnter(object sender,DragEventArgs e)=>DragOver(sender,e);
'@ @'
    private void SuggestFolderAppsV363(string[] dropped)
    {
        int epoch=Interlocked.Increment(ref recognizedFolderEpochV363);
        recognizedFolderV363=
            Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
        recognizedFolderErrorV363=null;
        if(dropped.Length!=1 || !Directory.Exists(dropped[0]))
        {
            RefreshViews();
            return;
        }
        string folder=dropped[0];
        _=Task.Run(()=>DeskSpaceFolderAppRecognitionV363.Recognize(folder))
            .ContinueWith(task=>{
                if(task.IsCanceled || task.IsFaulted)return;
                try
                {
                    host.Dispatcher.BeginInvoke(new Action(()=>{
                        if(disposed || epoch!=Volatile.Read(ref recognizedFolderEpochV363))
                            return;
                        recognizedFolderV363=task.Result.ToArray();
                        RefreshViews();
                    }),DispatcherPriority.Background);
                }
                catch(InvalidOperationException){}
            },TaskScheduler.Default);
    }
    private void OpenRecognizedFolderV363(
        DeskSpaceFolderAppRecognitionV363.Suggestion choice)
    {
        if(DeskSpaceFolderAppRecognitionV363.TryOpen(choice,out var error))
        {
            // A choice remains visible for another app or explicit dismiss.
            recognizedFolderErrorV363=null;
        }
        else recognizedFolderErrorV363=
            error??"Application could not open this folder.";
        RefreshViews();
    }
    private void FolderDragEnter(object sender,DragEventArgs e)=>DragOver(sender,e);
'@ 'deferred no-content folder inspection and explicit launch command'
One @'
            if(held.Count+detected.Count>0 &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
'@ @'
            if((held.Count+detected.Count>0 ||
                recognizedFolderV363.Count>0) &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
'@ 'reopen Deck for a new recognized folder'
One @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 &&
'@ @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 && recognizedFolderV363.Count==0 &&
'@ 'recognized folder keeps the shared Deck visible'
One @'
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
'@ @'
        // The existing Deck displays a compact action after a successful
        // ZIP/Copy drop. The original two-part circle remains unchanged.
        if(recognizedFolderV363.Count>0)
        {
            var match=recognizedFolderV363[0];
            var folderActions=new StackPanel{
                Margin=new Thickness(5,4,5,5)
            };
            var top=new DockPanel{LastChildFill=true};
            var dismiss=new Button{
                Content="×",FontSize=12,MinWidth=22,
                ToolTip="Dismiss application suggestion; files are untouched",
                Padding=new Thickness(2,0,2,0)
            };
            dismiss.Click+=(_,_)=>{
                Interlocked.Increment(ref recognizedFolderEpochV363);
                recognizedFolderV363=
                    Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
                recognizedFolderErrorV363=null;
                RefreshViews();
            };
            DockPanel.SetDock(dismiss,Dock.Right);
            top.Children.Add(dismiss);
            var folderName=Path.GetFileName(match.Folder.TrimEnd(
                Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            top.Children.Add(Text("Folder · "+folderName,11,Brushes.White));
            folderActions.Children.Add(top);
            folderActions.Children.Add(Text(
                match.FolderKind+" · "+match.Evidence,10,Brushes.LightSteelBlue));
            foreach(var choice in recognizedFolderV363.Take(3))
            {
                bool available=DeskSpaceFolderAppRecognitionV363.IsAvailable(choice);
                var action=new Button{
                    Content=choice.AppId=="Explorer"
                        ?"Open folder in Explorer"
                        :choice.AppId=="Default"
                            ?"Open document with Windows default"
                            :"Open with "+choice.AppName,
                    IsEnabled=available,FontSize=11,
                    HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(7,4,7,4),
                    Margin=new Thickness(1,2,1,1),
                    ToolTip=available
                        ?"Open only when clicked; never executes folder contents automatically"
                        :choice.AppName+" was not found on this computer"
                };
                action.Click+=(_,_)=>OpenRecognizedFolderV363(choice);
                folderActions.Children.Add(action);
            }
            if(!string.IsNullOrWhiteSpace(recognizedFolderErrorV363))
                folderActions.Children.Add(Text(
                    recognizedFolderErrorV363,10,Brushes.OrangeRed));
            list.Children.Add(new Border{
                CornerRadius=new CornerRadius(8),
                Background=new SolidColorBrush(Color.FromArgb(150,39,53,85)),
                Padding=new Thickness(5),
                Margin=new Thickness(2,3,2,4),
                Child=folderActions
            });
        }
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
'@ 'render local app choice within current Deck, no new window'
One @'
        Views.Remove(this);
'@ @'
        Views.Remove(this);
        if(Views.Count==0)
        {
            Interlocked.Increment(ref recognizedFolderEpochV363);
            recognizedFolderV363=
                Array.Empty<DeskSpaceFolderAppRecognitionV363.Suggestion>();
            recognizedFolderErrorV363=null;
        }
'@ 'clear ephemeral suggestions after last widget exits'
if(!$s.Contains('folderLayer.Remove(folderAdorner)') -or
   !$s.Contains('SuggestFolderAppsV363(') -or
   (!$s.Contains('CommitFolderDropAsync(dropped,zip)') -and
    !$s.Contains('CommitFolderDropAsync(sources,zip)'))){
    throw 'R363 must preserve existing native ZIP/Copy drop and overlay lifecycle'
}
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Copy-Item 'r363/DeskSpaceFolderAppRecognitionV363.cs' (Join-Path $root 'DeskSpaceFolderAppRecognitionV363.cs') -Force
Write-Host 'R363: folder signatures offered in existing Deck after authorized drop; no auto-launch, unchanged ZIP/Copy arc.'
