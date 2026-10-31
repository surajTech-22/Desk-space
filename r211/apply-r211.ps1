$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$p="$root/DeskSpaceDropShelfV15.cs"
if(!(Test-Path -LiteralPath $p)){throw 'R211 requires the verified DeskSpace Deck source'}
Copy-Item -LiteralPath 'r211/DeskSpaceContentDuplicateV211.cs' -Destination "$root/DeskSpaceContentDuplicateV211.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$name)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R211 "+$name+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
    private static bool busy;
'@ @'
    private static bool busy;
    // A content match is a question, never permission to replace files.
    // The pending decision is shared between Deck views on all monitors.
    private sealed class DuplicateChoiceV211
    {
        internal readonly string SourceName;
        internal readonly string ExistingName;
        internal readonly TaskCompletionSource<bool> Decision=
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal DuplicateChoiceV211(string source,string existing)
        { SourceName=source;ExistingName=existing; }
    }
    private static DuplicateChoiceV211? pendingDuplicateV211;
'@ 'shared pending decision'
Swap @'
        list.Children.Clear();
        if(lastCopyError!=null)
'@ @'
        list.Children.Clear();
        if(pendingDuplicateV211 is { } duplicateV211)
        {
            var row=new StackPanel{Margin=new Thickness(5,3,5,6)};
            row.Children.Add(new TextBlock{
                Text="Identical file already held: "+duplicateV211.SourceName+
                    " matches "+duplicateV211.ExistingName,
                FontSize=11,TextWrapping=TextWrapping.Wrap,
                Foreground=Brushes.White
            });
            var choices=new StackPanel{Orientation=Orientation.Horizontal};
            var keep=new Button{Content="Keep both",FontSize=10,
                ToolTip="Retain an additional copy; original and existing held file remain intact",
                Padding=new Thickness(7,2,7,2),Margin=new Thickness(0,3,5,0)};
            keep.Click+=(_,_)=>duplicateV211.Decision.TrySetResult(true);
            var skip=new Button{Content="Skip",FontSize=10,
                ToolTip="Do not stage another copy; leave both original and existing held item unchanged",
                Padding=new Thickness(7,2,7,2),Margin=new Thickness(0,3,0,0)};
            skip.Click+=(_,_)=>duplicateV211.Decision.TrySetResult(false);
            choices.Children.Add(keep);choices.Children.Add(skip);
            row.Children.Add(choices);
            list.Children.Add(row);
            // A prior dismissed Deck must not silently hide the user's
            // explicit keep/skip decision.
            deckDismissed=false;
        }
        if(lastCopyError!=null)
'@ 'inline nonmodal Keep both or Skip UI'
Swap @'
                if(AlreadyHeldSource(originalSource))continue;
                var id=Guid.NewGuid().ToString("N");
'@ @'
                if(AlreadyHeldSource(originalSource))continue;
                // File content checks run only after a user-initiated drop.
                // No filesystem work is performed on the WPF UI thread.
                if(File.Exists(source) && DeskSpaceDeckVisibilityV89.Visible &&
                    Views.Count>0)
                {
                    var currentlyHeld=held.Where(x=>File.Exists(x.StagedPath))
                        .Select(x=>x.StagedPath).ToArray();
                    var duplicate=await Task.Run(()=>
                        DeskSpaceContentDuplicateV211.FindMatching(
                            source,currentlyHeld,CancellationToken.None));
                    if(duplicate!=null)
                    {
                        var decision=new DuplicateChoiceV211(
                            Path.GetFileName(source),Path.GetFileName(duplicate));
                        pendingDuplicateV211=decision;
                        RefreshViews();
                        bool keepBoth=false;
                        try
                        {
                            // Never wait forever after an abandoned drop.
                            var completed=await Task.WhenAny(decision.Decision.Task,
                                Task.Delay(TimeSpan.FromMinutes(3)));
                            keepBoth=ReferenceEquals(completed,decision.Decision.Task)
                                && await decision.Decision.Task;
                        }
                        finally
                        {
                            if(ReferenceEquals(pendingDuplicateV211,decision))
                                pendingDuplicateV211=null;
                            RefreshViews();
                        }
                        if(!keepBoth)continue;
                    }
                }
                var id=Guid.NewGuid().ToString("N");
'@ 'hash-backed preflight before any copy'
Swap @'
        Views.Remove(this);
        if(ReferenceEquals(active,this))active=Views.FirstOrDefault();
'@ @'
        Views.Remove(this);
        if(Views.Count==0)pendingDuplicateV211?.Decision.TrySetResult(false);
        if(ReferenceEquals(active,this))active=Views.FirstOrDefault();
'@ 'safe cancel on final widget close'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R211: background SHA-256 duplicate preflight; shared inline Keep both/Skip, no replacement or source deletion.'
