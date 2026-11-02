using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DeskSpace;

// R339-05: Read-only duplicate inspector. No deletion, move, rename,
// hardlinking or background scanning. All hashing starts on explicit consent.
internal static class DeskSpaceDuplicateInspectorV339
{
    internal sealed record DuplicateGroup(string Hash,long Size,IReadOnlyList<string> Paths);
    internal const int MaxFiles=20000;
    internal static async Task<IReadOnlyList<DuplicateGroup>> InspectAsync(
        string folder,CancellationToken ct=default,int maxFiles=MaxFiles)
    {
        folder=Path.GetFullPath(folder);
        if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);
        maxFiles=Math.Clamp(maxFiles,1,MaxFiles);
        var byLength=new Dictionary<long,List<string>>();
        var dirs=new Stack<string>();dirs.Push(folder);
        int seen=0;
        while(dirs.Count!=0)
        {
            ct.ThrowIfCancellationRequested();
            var dir=dirs.Pop();
            IEnumerable<string> paths;
            try{paths=Directory.EnumerateFileSystemEntries(dir).ToArray();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or System.Security.SecurityException){continue;}
            foreach(var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                FileAttributes attributes;
                try{attributes=File.GetAttributes(path);}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException){continue;}
                if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                if((attributes&FileAttributes.Directory)!=0){dirs.Push(path);continue;}
                if(++seen>maxFiles)
                    throw new InvalidOperationException(
                        "Scan stopped at 20,000 files. Choose a smaller folder to avoid excessive disk activity.");
                try
                {
                    long size=new FileInfo(path).Length;
                    if(!byLength.TryGetValue(size,out var list))
                        byLength[size]=list=new List<string>();
                    list.Add(path);
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException){}
            }
        }
        var results=new List<DuplicateGroup>();
        foreach(var pair in byLength.Where(x=>x.Value.Count>1))
        {
            ct.ThrowIfCancellationRequested();
            var hashes=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach(var path in pair.Value)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    string digest=await DeskSpaceFileVerifyV339.HashFileAsync(path,ct);
                    if(!hashes.TryGetValue(digest,out var list))
                        hashes[digest]=list=new List<string>();
                    list.Add(path);
                }
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException){
                    // Files may change during scan; never treat unreadable
                    // files as duplicates or delete user data.
                }
            }
            foreach(var group in hashes.Where(x=>x.Value.Count>1))
                results.Add(new DuplicateGroup(group.Key,pair.Key,
                    group.Value.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        return results.OrderByDescending(x=>x.Size*(x.Paths.Count-1L))
            .ThenBy(x=>x.Paths[0],StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static Window Create(Window? owner=null)
    {
        var window=new Window{Title="DeskSpace · Duplicate file inspector",
            Width=770,Height=520,MinHeight=390,MinWidth=580,
            Background=System.Windows.Media.Brushes.White,ShowInTaskbar=false,
            WindowStartupLocation=owner!=null && owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null && owner.IsVisible)window.Owner=owner;
        var root=new DockPanel{Margin=new Thickness(16)};window.Content=root;
        var header=new StackPanel();
        header.Children.Add(new TextBlock{Text="Inspect duplicate files",FontSize=21,
            FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,4)});
        header.Children.Add(new TextBlock{
            Text="Choose a folder. Files are compared by size and SHA-256. Read-only: no deletion, uploading or automatic changes.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,9)});
        var location=new TextBox{IsReadOnly=true,MinHeight=29,ToolTip="Folder to inspect"};
        header.Children.Add(location);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,8)};
        var pick=new Button{Content="Choose folder",Padding=new Thickness(12,6,12,6),
            Margin=new Thickness(0,0,9,0)};
        var scan=new Button{Content="Inspect",Padding=new Thickness(12,6,12,6),
            Margin=new Thickness(0,0,9,0)};
        var cancel=new Button{Content="Cancel",Padding=new Thickness(12,6,12,6),
            Margin=new Thickness(0,0,9,0),IsEnabled=false};
        var status=new TextBlock{Text="Ready",VerticalAlignment=VerticalAlignment.Center};
        foreach(var btn in new[]{pick,scan,cancel})actions.Children.Add(btn);
        actions.Children.Add(status);
        header.Children.Add(actions);
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var results=new TextBox{IsReadOnly=true,AcceptsReturn=true,
            TextWrapping=TextWrapping.NoWrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            FontFamily=new System.Windows.Media.FontFamily("Consolas"),
            FontSize=12,Padding=new Thickness(9)};
        root.Children.Add(results);
        CancellationTokenSource? pending=null;
        bool busy=false;
        pick.Click+=(_,_)=>{
            if(busy)return;
            var chooser=new OpenFolderDialog{Title="Inspect duplicate files in folder",
                Multiselect=false};
            if(chooser.ShowDialog(window)==true){location.Text=chooser.FolderName;results.Clear();}
        };
        scan.Click+=async (_,_)=>{
            if(busy||!Directory.Exists(location.Text)){
                status.Text="Select a valid folder.";return;
            }
            busy=true;scan.IsEnabled=false;pick.IsEnabled=false;cancel.IsEnabled=true;
            pending=new CancellationTokenSource();
            try
            {
                results.Clear();status.Text="Inspecting locally (limit: 20,000 files)…";
                var groups=await Task.Run(async ()=>await InspectAsync(
                    location.Text,pending.Token),pending.Token);
                if(pending.IsCancellationRequested)return;
                status.Text=$"{groups.Count} duplicate group(s) found";
                results.Text=groups.Count==0?"No identical files found in this folder.":
                    string.Join(Environment.NewLine+Environment.NewLine,
                    groups.Take(300).Select(g=>
                    $"SHA-256: {g.Hash}  |  {g.Size:N0} bytes each  |  {g.Paths.Count} files"+
                    Environment.NewLine+string.Join(Environment.NewLine,g.Paths.Select(p=>"  "+p))));
            }
            catch(OperationCanceledException){status.Text="Cancelled";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException
                or System.Security.SecurityException){status.Text=ex.Message;}
            finally
            {
                busy=false;scan.IsEnabled=true;pick.IsEnabled=true;
                cancel.IsEnabled=false;pending?.Dispose();pending=null;
            }
        };
        cancel.Click+=(_,_)=>pending?.Cancel();
        window.Closed+=(_,_)=>pending?.Cancel();
        return window;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
