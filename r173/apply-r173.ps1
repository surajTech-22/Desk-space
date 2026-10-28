$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R173 $($label) requires one anchor, got $n"}
 $script:s=$script:s.Replace($old,$new)
}
Swap '    private static bool copyInProgress;' @'
    private static bool copyInProgress;
    // Keep copy failures visible inside the shared Deck. Do not show a
    // modal MessageBox while a WPF drag-drop transaction is unwinding.
    private static string? lastCopyError;
'@ 'shared nonmodal error state'
Swap '        if(busy||disposed||files.Length==0)return;' @'
        if(busy||disposed||files.Length==0)return;
        lastCopyError=null;
        System.Diagnostics.Debug.WriteLine("R173 stage: StageFilesAsync entry");
        Console.WriteLine("R173 stage: StageFilesAsync entry");
'@ 'trace async staging entry'
Swap @'
        busy=true;
        RefreshViews();
        try
'@ @'
        busy=true;
        Console.WriteLine("R173 stage: before initial RefreshViews");
        RefreshViews();
        Console.WriteLine("R173 stage: after initial RefreshViews");
        try
'@ 'find refresh reentrancy'
Swap @'
                    await Task.Run(()=>{
                        using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
'@ @'
                    Console.WriteLine("R173 stage: before starting background Copy");
                    await Task.Run(()=>{
                        Console.WriteLine("R173 stage: worker before marker lease");
                        using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
                        Console.WriteLine("R173 stage: worker lease acquired");
'@ 'copy lease checkpoint'
Swap @'
                    });
                    cancellation.Token.ThrowIfCancellationRequested();
'@ @'
                    });
                    Console.WriteLine("R173 stage: worker Copy complete");
                    cancellation.Token.ThrowIfCancellationRequested();
'@ 'copy result checkpoint'
Swap @'
        catch(Exception ex)
        {
            MessageBox.Show("Could not hold the file: "+ex.Message,
'@ @'
        catch(Exception ex)
        {
            Console.Error.WriteLine("R173 COPY FAILED: "+ex.GetType().Name+
                ": "+ex.Message);
            System.Diagnostics.Debug.WriteLine("DeskSpace Deck Copy error: "+ex);
            lastCopyError=ex.GetType().Name;
            RefreshViews();
            // A modal warning in PreviewDrop freezes desktop automation and
            // can permanently trap a hidden widget after the Explorer switch.
            _=Task.Delay(TimeSpan.FromSeconds(12)).ContinueWith(_=>
            {
                try{host.Dispatcher.BeginInvoke(new Action(()=>{
                    lastCopyError=null;
                    RefreshViews();
                }));}
                catch(TaskCanceledException){}
                catch(InvalidOperationException){}
            });
        }
        /* previous modal error removed */
        private void UnusedCopyErrorSentinel()
        {
            MessageBox.Show("Could not hold the file: "+ex.Message,
'@ 'replace blocking error dialog with Deck error'
# The sentinel hack above is NOT acceptable; remove it and its legacy body
# before writing. The historical catch contains a MessageBox followed by
# finally; remove the old text using a narrow regex with exact context.
$marker='        /* previous modal error removed */'
$start=$s.IndexOf($marker,[StringComparison]::Ordinal)
$end=$s.IndexOf('        finally{busy=false;RefreshViews();}',$start,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -lt 0){throw 'R173 unexpected legacy catch/finally shape'}
$s=$s.Substring(0,$start)+$s.Substring($end)

Swap @'
        list.Children.Clear();
'@ @'
        list.Children.Clear();
        if(lastCopyError!=null)
        {
            list.Children.Add(new TextBlock{
                Text="Copy could not finish ("+lastCopyError+"). Original preserved.",
                Foreground=Brushes.Orange,FontSize=11,TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(5,2,5,6)});
        }
'@ 'nonmodal shared Deck error row'
Swap '        if(held.Count==0 && !busy){popup.IsOpen=false;return;}' @'
        if(held.Count==0 && !busy && lastCopyError==null){
            popup.IsOpen=false;return;}
'@ 'keep nonmodal error visible if no staged files'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R173: WPF Copy checkpoint trace, no modal popup on failure, shared Deck error, and original-file preservation.'
