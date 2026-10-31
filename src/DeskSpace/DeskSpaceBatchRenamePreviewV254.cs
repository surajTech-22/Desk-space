using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R254 provides deterministic, read-only preview and conflict checks.
// R275 adds non-overwriting Apply/Undo; R277 persists a recovery journal.
internal static class DeskSpaceBatchRenamePreviewV254
{
    internal const int MaxFiles=128;
    internal sealed record Proposed(string Before,string After,bool Conflict);
    internal sealed record Plan(bool Eligible,string Explanation,
        IReadOnlyList<Proposed> Files);

    private static bool SafeAffix(string? value)=>
        value!=null && value.Length<=64 &&
        !value.Any(c=>char.IsControl(c) || "<>:\"/\\|?*".Contains(c));

    internal static Plan Prepare(IEnumerable<string> names,string prefix,string suffix)
    {
        if(!SafeAffix(prefix) || !SafeAffix(suffix) ||
            (prefix.Length==0 && suffix.Length==0))
            return new(false,"Enter a valid nonempty prefix or suffix "+
                "(64 characters maximum each).",Array.Empty<Proposed>());
        var input=names?.ToArray()??Array.Empty<string>();
        if(input.Length==0 || input.Length>MaxFiles)
            return new(false,"Choose 1–128 direct files.",Array.Empty<Proposed>());
        if(input.Any(name=>string.IsNullOrWhiteSpace(name) ||
            Path.GetFileName(name)!=name ||
            name.EndsWith(' ') || name.EndsWith('.') ||
            name.Any(c=>char.IsControl(c) || "<>:\"/\\|?*".Contains(c))) ||
           input.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=input.Length)
            return new(false,"Invalid or duplicate Windows file names.",
                Array.Empty<Proposed>());
        var source=new HashSet<string>(input,StringComparer.OrdinalIgnoreCase);
        var output=new List<Proposed>();
        foreach(var name in input.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase))
        {
            var stem=Path.GetFileNameWithoutExtension(name);
            var extension=Path.GetExtension(name);
            var proposed=prefix+stem+suffix+extension;
            bool conflict=proposed.Length>255 ||
                proposed.EndsWith(' ') || proposed.EndsWith('.') ||
                string.Equals(proposed,name,StringComparison.OrdinalIgnoreCase) ||
                (source.Contains(proposed) &&
                 !string.Equals(proposed,name,StringComparison.OrdinalIgnoreCase));
            output.Add(new(name,proposed,conflict));
        }
        var collisions=output.GroupBy(x=>x.After,
            StringComparer.OrdinalIgnoreCase)
            .Where(g=>g.Count()>1)
            .SelectMany(g=>g).Select(x=>x.Before)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<output.Count;i++)
            if(collisions.Contains(output[i].Before))
                output[i]=output[i] with {Conflict=true};
        bool eligible=!output.Any(x=>x.Conflict);
        return new(eligible,eligible?
            "Preview only: "+output.Count+" proposed names. No files changed.":
            "Blocked: at least one result collides, is unchanged or has an invalid length.",
            output);
    }

    internal static Plan InspectFolder(string folder,string prefix,string suffix)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(folder) ||
               !Directory.Exists(folder))
                return new(false,"Select an existing folder.",Array.Empty<Proposed>());
            var root=new DirectoryInfo(folder);
            if((root.Attributes&FileAttributes.ReparsePoint)!=0)
                return new(false,"Linked folders are not supported.",Array.Empty<Proposed>());
            var files=root.EnumerateFiles("*",SearchOption.TopDirectoryOnly)
                .Where(f=>(f.Attributes&(FileAttributes.ReparsePoint|
                    FileAttributes.Hidden|FileAttributes.System))==0)
                .Select(f=>f.Name).Take(MaxFiles+1).ToArray();
            return Prepare(files,prefix,suffix);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return new(false,"Cannot preview folder ("+e.GetType().Name+").",
                Array.Empty<Proposed>());
        }
    }

    internal static void Show(Window owner)
    {
        var window=new Window{
            Title="DeskSpace — Batch rename and undo",
            Width=540,Height=575,MinWidth=420,MinHeight=345,
            Owner=owner.IsVisible?owner:null,ShowInTaskbar=false,
            WindowStartupLocation=owner.IsVisible?
                WindowStartupLocation.CenterOwner:
                WindowStartupLocation.CenterScreen};
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(new TextBlock{
            Text="Batch rename — preview, apply and undo",
            FontSize=18,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,6)});
        content.Children.Add(new TextBlock{
            Text="Select a folder and review every proposed filename. "+
                 "Only direct visible files are included, up to 128. "+
                 "Apply requires confirmation, never overwrites, and provides "+
                 "durable, explicitly reviewed recovery after a crash. "+
                 "No background watching or automatic file changes.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,12)});
        var prefix=new TextBox{MinHeight=28,MaxLength=64};
        var suffix=new TextBox{MinHeight=28,MaxLength=64};
        AutomationProperties.SetName(prefix,"Filename prefix");
        AutomationProperties.SetName(suffix,"Filename suffix");
        content.Children.Add(new TextBlock{Text="Prefix"});
        content.Children.Add(prefix);
        content.Children.Add(new TextBlock{
            Text="Suffix",Margin=new Thickness(0,8,0,0)});
        content.Children.Add(suffix);
        var inspect=new Button{
            Content="Choose folder and preview",
            Padding=new Thickness(9,6,9,6),
            HorizontalAlignment=HorizontalAlignment.Left,
            Margin=new Thickness(0,12,0,8)};
        var status=new TextBlock{
            Text="No preview calculated.",
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,8)};
        var apply=new Button{
            Content="Apply approved names",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var undo=new Button{
            Content="Undo this batch",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var cancel=new Button{
            Content="Cancel operation",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var recovery=new Button{
            Content="Review previous batch",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8)};
        var restore=new Button{
            Content="Recover reviewed batch",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        var keep=new Button{
            Content="Keep renamed files",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,8),IsEnabled=false};
        AutomationProperties.SetName(keep,
            "Accept completed rename and discard durable undo record");
        AutomationProperties.SetName(recovery,
            "Select folder to inspect interrupted rename");
        AutomationProperties.SetName(restore,
            "Restore safely reviewed renamed files");
        AutomationProperties.SetName(apply,"Apply previewed batch rename");
        AutomationProperties.SetName(undo,"Undo batch rename");
        var choices=new WrapPanel();
        choices.Children.Add(apply);
        choices.Children.Add(undo);
        choices.Children.Add(cancel);
        choices.Children.Add(recovery);
        choices.Children.Add(restore);
        choices.Children.Add(keep);
        var rows=new StackPanel();
        content.Children.Add(inspect);content.Children.Add(choices);
        content.Children.Add(status);content.Children.Add(rows);
        string selectedFolder="";
        string preparedPrefix="",preparedSuffix="";
        Plan? prepared=null;
        DeskSpaceBatchRenameTransactionV275.Receipt? undoReceipt=null;
        DeskSpaceBatchRenameRecoveryV277.Review? recoveryReview=null;
        string recoveryFolder="";
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
            recovery.IsEnabled=!value&&undoReceipt==null;
            restore.IsEnabled=!value&&undoReceipt==null&&
                recoveryReview?.CanRecover==true;
            keep.IsEnabled=!value&&(undoReceipt!=null ||
                (recoveryReview?.CanRecover==true &&
                 recoveryReview.Renamed>0 && recoveryReview.Untouched==0));
        }
        bool SameReview(DeskSpaceBatchRenameRecoveryV277.Review a,
            DeskSpaceBatchRenameRecoveryV277.Review b)
        {
            var first=a.Undo?.Moves;
            var second=b.Undo?.Moves;
            return a.CanRecover&&b.CanRecover &&
                a.Renamed==b.Renamed && a.Untouched==b.Untouched &&
                first!=null && second!=null &&
                first.Count==second.Count &&
                first.Zip(second,(x,y)=>
                    x.Before==y.Before && x.After==y.After &&
                    x.Length==y.Length &&
                    x.LastWriteUtc==y.LastWriteUtc).All(match=>match);
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
                    "The local recovery record will be retained after closing. "+
                    "To restore filenames later, reopen Batch Rename and "+
                    "choose Review previous batch. Close this window?",
                    "Close rename session",MessageBoxButton.YesNo,
                    MessageBoxImage.Information)!=MessageBoxResult.Yes)
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
                "window stays open, and a durable recovery record is kept "+
                "if the app closes unexpectedly.",
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
                    DeskSpaceBatchRenameRecoveryV277.Apply(
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
                    DeskSpaceBatchRenameRecoveryV277.Undo(
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
        recovery.Click+=async (_,_)=>{
            if(working||undoReceipt!=null)return;
            using var picker=new Forms.FolderBrowserDialog{
                Description="Choose folder with a previous batch rename",
                ShowNewFolderButton=false
            };
            if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
            recoveryFolder="";
            recoveryReview=null;
            rows.Children.Clear();
            Busy(true);
            status.Text="Checking local recovery record (no files changed)…";
            try
            {
                var selected=picker.SelectedPath;
                var review=await Task.Run(()=>
                    DeskSpaceBatchRenameRecoveryV277.Inspect(selected));
                if(!window.IsLoaded)return;
                recoveryReview=review;
                recoveryFolder=selected;
                status.Text=review.Message;
                if(review.CanRecover)
                {
                    rows.Children.Add(new TextBlock{
                        Text="Recovery review — renamed: "+review.Renamed+
                            "; original names intact: "+review.Untouched,
                        FontWeight=FontWeights.SemiBold,
                        TextWrapping=TextWrapping.Wrap});
                    foreach(var move in review.Undo!.Moves)
                        rows.Children.Add(new TextBlock{
                            Text=move.After+" → "+move.Before,
                            Margin=new Thickness(0,0,0,3),
                            TextWrapping=TextWrapping.Wrap});
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException)
            {status.Text="Recovery inspection unavailable ("+
                ex.GetType().Name+").";recoveryReview=null;}
            finally{if(window.IsLoaded)Busy(false);}
        };
        restore.Click+=async (_,_)=>{
            if(working||undoReceipt!=null||
               recoveryReview?.CanRecover!=true)return;
            var target=recoveryFolder;
            var latest=DeskSpaceBatchRenameRecoveryV277.Inspect(target);
            if(!SameReview(latest,recoveryReview))
            {
                status.Text="Recovery state changed. Review previous batch again.";
                recoveryReview=null;
                Busy(false);
                return;
            }
            if(MessageBox.Show(window,
                "Restore "+latest.Renamed+" renamed files in the selected "+
                "folder? Names that were not changed will be left alone. "+
                "Modified, missing or occupied names block recovery.",
                "Confirm interrupted batch recovery",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            using var cancellation=new CancellationTokenSource();
            running=cancellation;
            Busy(true);
            status.Text="Restoring approved filenames without overwrite…";
            try
            {
                var result=await Task.Run(()=>
                    DeskSpaceBatchRenameRecoveryV277.Recover(
                        target,cancellation.Token));
                status.Text=result.Message;
                recoveryReview=null;
                if(result.Success)rows.Children.Clear();
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or OperationCanceledException)
            {
                status.Text="Recovery stopped ("+ex.GetType().Name+
                    "). Recheck the folder before retrying.";
                recoveryReview=null;
            }
            finally{running=null;Busy(false);}
        };
        keep.Click+=async (_,_)=>{
            if(working)return;
            var folder=undoReceipt?.Folder??recoveryFolder;
            if(string.IsNullOrWhiteSpace(folder))return;
            var review=DeskSpaceBatchRenameRecoveryV277.Inspect(folder);
            if(!review.CanRecover || review.Renamed==0 ||
               review.Untouched!=0 ||
               (recoveryReview!=null &&
                !SameReview(review,recoveryReview)))
            {
                status.Text="Cannot keep this batch: files or recovery state "+
                    "changed. Review again before accepting.";
                recoveryReview=null;
                Busy(false);
                return;
            }
            if(MessageBox.Show(window,
                "Keep all "+review.Renamed+" renamed files permanently? "+
                "This removes the saved undo/recovery record but does not "+
                "move, delete or change any file. This cannot be undone "+
                "through DeskSpace after confirmation.",
                "Accept renamed filenames",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            using var cancellation=new CancellationTokenSource();
            running=cancellation;
            Busy(true);
            try
            {
                var result=await Task.Run(()=>
                    DeskSpaceBatchRenameRecoveryV277.Keep(
                        folder,cancellation.Token));
                status.Text=result.Message;
                if(result.Success)
                {
                    undoReceipt=null;
                    recoveryReview=null;
                    prepared=null;
                    rows.Children.Clear();
                }
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or OperationCanceledException)
            {
                status.Text="Could not accept batch ("+ex.GetType().Name+
                    "). Recovery record retained.";
            }
            finally{running=null;Busy(false);}
        };
        window.Content=new ScrollViewer{
            Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        inspect.Click+=async (_,_)=>{
            if(working||undoReceipt!=null)return;
            using var picker=new Forms.FolderBrowserDialog{
                Description="Choose folder for batch rename preview",
                ShowNewFolderButton=false};
            if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
            var selected=picker.SelectedPath;
            var beforePrefix=prefix.Text;
            var beforeSuffix=suffix.Text;
            rows.Children.Clear();
            prepared=null;
            selectedFolder="";
            apply.IsEnabled=false;
            inspect.IsEnabled=false;
            status.Text="Inspecting filenames; no changes made…";
            try
            {
                var plan=await Task.Run(()=>InspectFolder(
                    selected,beforePrefix,beforeSuffix));
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
                foreach(var entry in plan.Files)
                    rows.Children.Add(new TextBlock{
                        Text=entry.Before+" → "+entry.After+
                            (entry.Conflict?"  [BLOCKED]":""),
                        TextWrapping=TextWrapping.Wrap,
                        Foreground=entry.Conflict?Brushes.Firebrick:Brushes.Black,
                        Margin=new Thickness(0,0,0,5)});
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException)
            {
                if(window.IsLoaded)status.Text="Preview failed ("+
                    e.GetType().Name+"). No files changed.";
            }
            finally{if(window.IsLoaded)Busy(false);}
        };
        window.Show();
    }
}
