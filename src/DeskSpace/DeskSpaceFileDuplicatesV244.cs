using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R244: explicitly selected one-folder duplicate inspection.
// No recursion, deletion, copy/move, hard links, watch service, or telemetry.
// Hashing runs off the WPF thread with cancellation and strict byte budgets.
internal static class DeskSpaceFileDuplicatesV244
{
    internal const int MaxFiles=128;
    internal const long MaxPerFileBytes=64L*1024*1024;
    internal const long MaxTotalBytes=256L*1024*1024;
    internal sealed record MatchGroup(string Hash,long Bytes,
        IReadOnlyList<string> Names);
    internal sealed record Result(bool Accepted,string Status,
        int Examined,IReadOnlyList<MatchGroup> Groups);

    private static string HashFile(FileInfo file,CancellationToken cancel)
    {
        using var input=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,
            FileShare.Read,64*1024,FileOptions.SequentialScan);
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer=new byte[64*1024];
        long seen=0;
        int n;
        while((n=input.Read(buffer,0,buffer.Length))>0)
        {
            cancel.ThrowIfCancellationRequested();
            seen+=n;
            if(seen>MaxPerFileBytes)
                throw new InvalidDataException("A file exceeded the inspection size limit.");
            digest.AppendData(buffer,0,n);
        }
        cancel.ThrowIfCancellationRequested();
        file.Refresh();
        if(!file.Exists || file.Length!=seen)
            throw new IOException("A file changed during inspection.");
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    internal static Result Scan(string folder,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new(false,"Select an existing folder.",0,Array.Empty<MatchGroup>());
        try
        {
            var root=new DirectoryInfo(folder);
            if((root.Attributes & FileAttributes.ReparsePoint)!=0)
                return new(false,"Linked folders are not inspected.",0,
                    Array.Empty<MatchGroup>());
            // No recursion. Only direct, ordinary visible files; never
            // follow symlink/reparse-point entries or enumerate subfolders.
            var files=root.EnumerateFiles("*",SearchOption.TopDirectoryOnly)
                .Where(x=>(x.Attributes &
                    (FileAttributes.ReparsePoint|FileAttributes.Hidden|
                     FileAttributes.System))==0)
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
                .Take(MaxFiles+1).ToArray();
            if(files.Length>MaxFiles)
                return new(false,"Folder has more than 128 eligible files. "+
                    "No files were hashed.",0,Array.Empty<MatchGroup>());
            if(files.Any(x=>x.Length>MaxPerFileBytes || x.Length<0))
                return new(false,"File exceeds the 64 MiB individual limit. "+
                    "No files were hashed.",0,Array.Empty<MatchGroup>());
            long total=0;
            foreach(var file in files)
            {
                cancel.ThrowIfCancellationRequested();
                total=checked(total+file.Length);
                if(total>MaxTotalBytes)
                    return new(false,"Folder exceeds the 256 MiB total limit. "+
                        "No files were hashed.",0,Array.Empty<MatchGroup>());
            }
            var fingerprints=new List<(string Name,long Bytes,string Hash)>();
            foreach(var file in files)
            {
                cancel.ThrowIfCancellationRequested();
                fingerprints.Add((file.Name,file.Length,HashFile(file,cancel)));
            }
            var groups=fingerprints.GroupBy(x=>(x.Bytes,x.Hash))
                .Where(g=>g.Count()>1)
                .OrderByDescending(g=>g.Count())
                .ThenBy(g=>g.Key.Hash,StringComparer.Ordinal)
                .Select(g=>new MatchGroup(g.Key.Hash,g.Key.Bytes,
                    g.Select(x=>x.Name).ToArray()))
                .ToArray();
            return new(true,groups.Length==0?
                "No matching SHA-256 file groups in this folder.":
                groups.Length+" matching SHA-256 group(s) found. No changes made.",
                files.Length,groups);
        }
        catch(OperationCanceledException){throw;}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidDataException or OverflowException)
        {
            return new(false,"Inspection unavailable ("+ex.GetType().Name+
                "). No changes made.",0,Array.Empty<MatchGroup>());
        }
    }

    internal static void Show(Window owner)
    {
        var window=new Window{
            Title="DeskSpace — Duplicate-file inspector (read-only)",
            Width=560,Height=590,MinWidth=430,MinHeight=360,
            Owner=owner,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ResizeMode=ResizeMode.CanResize};
        var panel=new StackPanel{Margin=new Thickness(16)};
        var intro=new TextBlock{
            Text="Inspect duplicate files in one selected folder",
            FontSize=17,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,8)};
        var privacy=new TextBlock{
            Text="Manual scan only. Direct visible files, up to 128 files / "+
                 "256 MiB total. SHA-256 is calculated locally. No recursion, "+
                 "upload, copy, move, rename or deletion.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,14)};
        panel.Children.Add(intro);panel.Children.Add(privacy);
        var label=new TextBlock{
            Text="No folder selected.",TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,10)};
        var status=new TextBlock{
            Text="Ready. Choose a folder to start.",
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,12)};
        var details=new StackPanel();
        var actions=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,0,0,12)};
        var choose=new Button{
            Content="Choose folder and inspect",Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(0,0,8,0)};
        var cancel=new Button{
            Content="Cancel scan",Padding=new Thickness(10,6,10,6),
            IsEnabled=false};
        AutomationProperties.SetName(choose,"Choose folder for read-only duplicate scan");
        AutomationProperties.SetName(cancel,"Cancel duplicate scan");
        actions.Children.Add(choose);actions.Children.Add(cancel);
        panel.Children.Add(actions);panel.Children.Add(label);
        panel.Children.Add(status);panel.Children.Add(details);
        window.Content=new ScrollViewer{
            Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        CancellationTokenSource? current=null;
        cancel.Click+=(_,_)=>current?.Cancel();
        choose.Click+=async (_,_)=>{
            if(current!=null)return;
            using var picker=new Forms.FolderBrowserDialog{
                Description="Select one folder for read-only duplicate checking",
                ShowNewFolderButton=false,
                UseDescriptionForTitle=true};
            if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
            var directory=picker.SelectedPath;
            // Do not persist the chosen full path or show it in diagnostics.
            label.Text="Selected folder: "+Path.GetFileName(
                directory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            details.Children.Clear();
            choose.IsEnabled=false;cancel.IsEnabled=true;
            status.Text="Reading file hashes locally…";
            var scope=new CancellationTokenSource();
            current=scope;
            try
            {
                var result=await Task.Run(()=>Scan(directory,scope.Token),scope.Token);
                if(!window.IsLoaded || scope.IsCancellationRequested)return;
                status.Text=result.Status+" Inspected "+result.Examined+" file(s).";
                foreach(var match in result.Groups)
                {
                    var heading=new TextBlock{
                        Text=match.Names.Count+" files, "+match.Bytes+
                            " bytes each, SHA-256 "+match.Hash[..12]+"…",
                        FontWeight=FontWeights.SemiBold,
                        TextWrapping=TextWrapping.Wrap,
                        Margin=new Thickness(0,8,0,4)};
                    details.Children.Add(heading);
                    foreach(var name in match.Names)
                        details.Children.Add(new TextBlock{
                            Text="• "+name,TextWrapping=TextWrapping.Wrap});
                }
            }
            catch(OperationCanceledException)
            {
                if(window.IsLoaded)status.Text="Inspection cancelled; no files changed.";
            }
            finally
            {
                if(ReferenceEquals(current,scope))current=null;
                scope.Dispose();
                if(window.IsLoaded){choose.IsEnabled=true;cancel.IsEnabled=false;}
            }
        };
        window.Closed+=(_,_)=>{current?.Cancel();};
        window.Show();
    }
}
