$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r186/DeskSpaceContentDuplicatesV186.cs' -Destination "$root/DeskSpaceContentDuplicatesV186.cs" -Force
$p="$root/DeskSpaceComponentHealthV175.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$from,[string]$to,[string]$label)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($hits -ne 1){throw ("R186 "+$label+" expected one anchor; found "+$hits)}
 $script:s=$script:s.Replace($from,$to)
}
One @'
            Text="On-demand local checks only. No clipboard, Excel, search, "+
                "account or saved-content data is read or uploaded.",
'@ @'
            Text="Status checks do not inspect clipboard, Excel or passwords. "+
                "Optional duplicate scan reads only staged Deck copies when clicked; "+
                "fingerprints remain local and no files are changed.",
'@ 'explain explicitly requested staged-content read'
One @'
        controls.Children.Add(export);
'@ @'
        controls.Children.Add(export);
        // This action is explicit, read-only, and never auto-removes or
        // merges held files. It reads ONLY app-owned staged copies, not source
        // paths, user documents, clipboard contents, or password vault data.
        var findings=new TextBlock{
            Text="Content duplicates have not been checked.",
            Foreground=Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,10,0,0),FontSize=11};
        CancellationTokenSource? scan=null;
        var findDuplicates=new Button{
            Content="Find duplicate held files (SHA-256)",
            Margin=new Thickness(8,0,0,0),Padding=new Thickness(8,6,8,6)};
        var cancelScan=new Button{
            Content="Cancel scan",IsEnabled=false,
            Margin=new Thickness(8,0,0,0),Padding=new Thickness(8,6,8,6)};
        cancelScan.Click+=(_,_)=>scan?.Cancel();
        findDuplicates.Click+=async (_,_)=>{
            if(scan!=null)return;
            var tokenSource=new CancellationTokenSource();
            scan=tokenSource;
            findDuplicates.IsEnabled=false;
            cancelScan.IsEnabled=true;
            findings.Text="Scanning staged file contents locally. Sources untouched.";
            try
            {
                var paths=DeskSpaceDropShelfV15.ActivePaths;
                var answer=await System.Threading.Tasks.Task.Run(()=>
                    DeskSpaceContentDuplicatesV186.Check(paths,tokenSource.Token));
                var lines=new List<string>{
                    answer.Checked+" staged item(s) scanned; "+
                    answer.Skipped+" skipped (unavailable, unsafe or oversized)."};
                if(answer.Groups.Length==0)
                    lines.Add("No identical held-file contents found.");
                foreach(var group in answer.Groups)
                    lines.Add(group.Kind+" duplicates: "+string.Join(", ",group.Names));
                lines.Add("No items were removed or modified.");
                findings.Text=string.Join(Environment.NewLine,lines);
            }
            catch(OperationCanceledException)
            {
                findings.Text="Duplicate scan cancelled. Held files unchanged.";
            }
            catch(Exception error)
            {
                findings.Text="Duplicate scan unavailable ("+
                    error.GetType().Name+"). Held files unchanged.";
            }
            finally
            {
                scan=null;
                tokenSource.Dispose();
                findDuplicates.IsEnabled=true;
                cancelScan.IsEnabled=false;
            }
        };
        controls.Children.Add(findDuplicates);
        controls.Children.Add(cancelScan);
'@ 'explicit cancellable duplicate scan'
One @'
        body.Children.Add(list);body.Children.Add(controls);
'@ @'
        body.Children.Add(list);body.Children.Add(controls);
        body.Children.Add(findings);
'@ 'render local duplicate findings'
One @'
        Refresh();
        return window;
'@ @'
        window.Closed+=(_,_)=>scan?.Cancel();
        Refresh();
        return window;
'@ 'cancel slow read-only scan when status panel closes'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R186: opt-in cancellable SHA256 content-duplicate check in existing status dashboard; no changes to Deck staging or paste semantics.'
