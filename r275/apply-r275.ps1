$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r275/DeskSpaceBatchRenameTransactionV275.cs' "$root/DeskSpaceBatchRenameTransactionV275.cs" -Force
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$p="$root/DeskSpaceBatchRenamePreviewV254.cs"
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R275 '+$why+' expected one anchor, got '+$count)}
 $script:s=$script:s.Replace($old,$next)
}
Swap @'
using System.Linq;
using System.Threading.Tasks;
'@ @'
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
'@ 'Batch rename workflow'
Swap @'
// R254: deterministic read-only batch rename PREVIEW. There is deliberately
// NO rename/commit button until undo/rollback and approval are implemented.
'@ @'
// R254 provides deterministic, read-only preview and conflict checks.
// R275 adds a separately verified non-overwriting Apply and same-session Undo.
'@ 'Batch rename workflow'
Swap @'
            Title="DeskSpace — Batch rename preview (no changes)",
'@ @'
            Title="DeskSpace — Batch rename and undo",
'@ 'Batch rename workflow'
Swap @'
            Text="Batch rename — preview only",
'@ @'
            Text="Batch rename — preview, apply and undo",
'@ 'Batch rename workflow'
Swap @'
            Text="Select a folder and preview prefix/suffix changes. "+
                 "Only direct visible files are considered, up to 128. "+
                 "No renaming, deletion, move or background watching.",
'@ @'
            Text="Select a folder and review every proposed filename. "+
                 "Only direct visible files are included, up to 128. "+
                 "Apply requires confirmation, never overwrites, and provides "+
                 "undo until this window closes. No background watching.",
'@ 'Batch rename workflow'
Swap @'
        var rows=new StackPanel();
        content.Children.Add(inspect);content.Children.Add(status);
        content.Children.Add(rows);
'@ @'
        var apply=new Button{
            Content="Apply approved names",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var undo=new Button{
            Content="Undo this batch",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var cancel=new Button{
            Content="Cancel operation",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        AutomationProperties.SetName(apply,"Apply previewed batch rename");
        AutomationProperties.SetName(undo,"Undo batch rename");
        var choices=new WrapPanel();
        choices.Children.Add(apply);
        choices.Children.Add(undo);
        choices.Children.Add(cancel);
        var rows=new StackPanel();
        content.Children.Add(inspect);content.Children.Add(choices);
        content.Children.Add(status);content.Children.Add(rows);
        string selectedFolder="";
        string preparedPrefix="",preparedSuffix="";
        Plan? prepared=null;
        DeskSpaceBatchRenameTransactionV275.Receipt? undoReceipt=null;
        CancellationTokenSource? running=null;
        bool working=false;
        void ResetPreview()
        {
            if(working)return;
            prepared=null;apply.IsEnabled=false;
            if(undoReceipt==null)
                status.Text="Names changed. Review the folder again before Apply.";
        }
        prefix.TextChanged+=(_,_)=>ResetPreview();
        suffix.TextChanged+=(_,_)=>ResetPreview();
        void Busy(bool value)
        {
            working=value;
            inspect.IsEnabled=!value&&undoReceipt==null;
            prefix.IsEnabled=!value&&undoReceipt==null;
            suffix.IsEnabled=!value&&undoReceipt==null;
            apply.IsEnabled=!value&&undoReceipt==null&&
                prepared?.Eligible==true;
            undo.IsEnabled=!value&&undoReceipt!=null;
            cancel.IsEnabled=value;
        }
        cancel.Click+=(_,_)=>{
            running?.Cancel();
            status.Text="Cancelling and reverting completed changes…";
        };
        window.Closing+=(_,e)=>{
            if(working)
            {
                running?.Cancel();
                e.Cancel=true;
                status.Text="Operation is stopping; the window stays open "+
                    "until rollback is finished.";
            }
            else if(undoReceipt!=null &&
                MessageBox.Show(window,
                    "Undo will no longer be available after closing this window. "+
                    "Keep the renamed files?",
                    "Close rename session",MessageBoxButton.YesNo,
                    MessageBoxImage.Warning)!=MessageBoxResult.Yes)
                e.Cancel=true;
        };
        apply.Click+=async (_,_)=>{
            if(prepared?.Eligible!=true || undoReceipt!=null ||
               preparedPrefix!=prefix.Text || preparedSuffix!=suffix.Text)
            {
                status.Text="Preview again before applying.";
                return;
            }
            if(MessageBox.Show(window,
                "Rename "+prepared.Files.Count+" files in the selected folder? "+
                "Every destination must be empty. This changes filenames "+
                "but never file contents. Undo is available while this "+
                "window stays open.",
                "Confirm batch rename",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var selected=selectedFolder;
            var proposal=prepared;
            var before=preparedPrefix;
            var after=preparedSuffix;
            using var cancellation=new CancellationTokenSource();
            running=cancellation;
            Busy(true);
            status.Text="Rechecking folder and applying approved names…";
            try
            {
                var outcome=await Task.Run(()=>
                    DeskSpaceBatchRenameTransactionV275.Apply(
                        selected,proposal,before,after,cancellation.Token));
                undoReceipt=outcome.Undo;
                if(outcome.Success)prepared=null;
                status.Text=outcome.Message;
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or OperationCanceledException)
            {
                status.Text="Could not apply ("+ex.GetType().Name+"). "+
                    "Review the folder before retrying.";
                prepared=null;
            }
            finally
            {
                running=null;
                Busy(false);
            }
        };
        undo.Click+=async (_,_)=>{
            var receipt=undoReceipt;
            if(receipt==null)return;
            if(MessageBox.Show(window,
                "Restore the original names for this batch? "+
                "Undo refuses to overwrite or replace files modified "+
                "since Apply.",
                "Confirm undo",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            using var cancellation=new CancellationTokenSource();
            running=cancellation;
            Busy(true);
            status.Text="Verifying renamed files and restoring names…";
            try
            {
                var outcome=await Task.Run(()=>
                    DeskSpaceBatchRenameTransactionV275.Undo(
                        receipt,cancellation.Token));
                undoReceipt=outcome.Undo;
                prepared=null;
                status.Text=outcome.Message;
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or OperationCanceledException)
            {
                status.Text="Undo unavailable ("+ex.GetType().Name+"). "+
                    "Originals were not overwritten.";
            }
            finally
            {
                running=null;
                Busy(false);
            }
        };
'@ 'Batch rename workflow'
Swap @'
        inspect.Click+=async (_,_)=>{
            using var picker=new Forms.FolderBrowserDialog{
                Description="Choose folder for read-only batch rename preview",
'@ @'
        inspect.Click+=async (_,_)=>{
            if(working||undoReceipt!=null)return;
            using var picker=new Forms.FolderBrowserDialog{
                Description="Choose folder for batch rename preview",
'@ 'Batch rename workflow'
Swap @'
            rows.Children.Clear();
            inspect.IsEnabled=false;
'@ @'
            rows.Children.Clear();
            prepared=null;
            selectedFolder="";
            apply.IsEnabled=false;
            inspect.IsEnabled=false;
'@ 'Batch rename workflow'
Swap @'
                if(!window.IsLoaded)return;
                status.Text=plan.Explanation;
'@ @'
                if(!window.IsLoaded)return;
                prepared=plan.Eligible?plan:null;
                selectedFolder=selected;
                preparedPrefix=beforePrefix;
                preparedSuffix=beforeSuffix;
                apply.IsEnabled=plan.Eligible;
                status.Text=plan.Eligible?
                    "Ready to apply "+plan.Files.Count+
                    " names. Review all rows, then choose Apply approved names.":
                    plan.Explanation;
'@ 'Batch rename workflow'
Swap @'
            finally{if(window.IsLoaded)inspect.IsEnabled=true;}
'@ @'
            finally{if(window.IsLoaded)Busy(false);}
'@ 'Batch rename workflow'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R275: consented batch rename and rollback-safe Undo wired to original preview.'