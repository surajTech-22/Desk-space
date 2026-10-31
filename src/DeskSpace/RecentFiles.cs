using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DeskSpace;

public sealed class RecentFileEntry
{
    public string Path {get;set;}="";
    public DateTime FirstSeen {get;set;}=DateTime.Now;
    public bool Pinned {get;set;}
    public string Name=>System.IO.Path.GetFileName(Path);
    public override string ToString()=> $"{(Pinned?"📌 ":"")}{Name}    —   {FirstSeen:g}\n{Path}";
}
public sealed class RecentFilesService : IDisposable
{
    private readonly ConcurrentDictionary<string,RecentFileEntry> items=new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> watchers=new();
    private readonly string filename=System.IO.Path.Combine(WorkspaceStore.Root,"recent-files-v3.json");
    private readonly object saveLock=new();
    public IReadOnlyList<RecentFileEntry> Entries=>items.Values.OrderByDescending(x=>x.Pinned).ThenByDescending(x=>x.FirstSeen).Take(300).ToList();
    public RecentFilesService()
    {
        try{foreach(var entry in JsonSerializer.Deserialize<List<RecentFileEntry>>(File.ReadAllText(filename))??new())if(File.Exists(entry.Path))items.TryAdd(entry.Path,entry);}catch{}
    }
    public void Start()
    {
        foreach(var path in WatchPaths())
        {
            try
            {
                if(!Directory.Exists(path))continue;
                foreach(var file in Directory.EnumerateFiles(path,"*",SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).Take(30))Add(file,false);
                var watcher=new FileSystemWatcher(path){NotifyFilter=NotifyFilters.FileName|NotifyFilters.CreationTime,IncludeSubdirectories=true};
                watcher.Created+=(_,e)=>Add(e.FullPath,true);
                watcher.Renamed+=(_,e)=>Add(e.FullPath,true);
                watcher.EnableRaisingEvents=true;watchers.Add(watcher);
            }
            catch(UnauthorizedAccessException){} catch(IOException){}
        }
        Save();
    }
    private static IEnumerable<string> WatchPaths()
    {
        yield return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        yield return WorkspaceStore.Attachments;
    }
    private void Add(string path,bool flush)
    {
        if(Directory.Exists(path)||!File.Exists(path))return;
        if(!items.ContainsKey(path))items.TryAdd(path,new RecentFileEntry{Path=path,FirstSeen=DateTime.Now});
        if(items.Count>400)
        {
            foreach(var s in items.Values.Where(x=>!x.Pinned).OrderBy(x=>x.FirstSeen).Take(items.Count-300))items.TryRemove(s.Path,out _);
        }
        if(flush)Save();
    }
    public void Pin(string path,bool value){if(items.TryGetValue(path,out var item)){item.Pinned=value;Save();}}
    private void Save()
    {
        lock(saveLock)
        {
            try{Directory.CreateDirectory(WorkspaceStore.Root);string tmp=filename+".tmp";
                File.WriteAllText(tmp,JsonSerializer.Serialize(Entries));File.Move(tmp,filename,true);}catch(IOException){}
        }
    }
    public void Dispose(){foreach(var watcher in watchers)watcher.Dispose();Save();}
}
public sealed class RecentFilesWindow : Window
{
    private readonly RecentFilesService files;
    private readonly ListBox list=new(){Margin=new Thickness(10)};
    private readonly TextBox filter=new(){Margin=new Thickness(10),MinWidth=200};
    private readonly DispatcherTimer refresh=new(){Interval=TimeSpan.FromSeconds(2)};
    public RecentFilesWindow(RecentFilesService files)
    {
        this.files=files;Title="DeskSpace · Recent files";Width=740;Height=525;
        var root=new DockPanel();var top=new StackPanel{Orientation=Orientation.Vertical};
        var buttons=new WrapPanel{Margin=new Thickness(10,0,0,5)};
        Add("Open",()=>Selected(Launch));Add("Open folder",()=>Selected(OpenFolder));
        Add("Copy path",()=>Selected(p=>Clipboard.SetText(p)));
        Add("Copy file",()=>Selected(p=>{if(File.Exists(p))Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection{p});}));
        Add("Pin / Unpin",()=>{if(list.SelectedItem is RecentFileEntry f){files.Pin(f.Path,!f.Pinned);Reload();}});
        Add("Refresh",Reload);
        filter.TextChanged+=(_,_)=>Reload();top.Children.Add(filter);top.Children.Add(buttons);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);root.Children.Add(list);Content=root;
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);
        refresh.Tick+=(_,_)=>Reload();refresh.Start();Closed+=(_,_)=>refresh.Stop();Reload();
        void Add(string label,Action callback){var b=new Button{Content=label,Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,0,8,0)};b.Click+=(_,_)=>callback();buttons.Children.Add(b);}
    }
    private void Reload()
    {
        var previous=(list.SelectedItem as RecentFileEntry)?.Path;
        list.ItemsSource=files.Entries.Where(x=>x.Name.Contains(filter.Text,StringComparison.OrdinalIgnoreCase)||x.Path.Contains(filter.Text,StringComparison.OrdinalIgnoreCase)).ToList();
        if(previous!=null)list.SelectedItem=((IEnumerable<RecentFileEntry>)list.ItemsSource).FirstOrDefault(x=>x.Path==previous);
    }
    private void Selected(Action<string> action){if(list.SelectedItem is RecentFileEntry e&&File.Exists(e.Path))try{action(e.Path);}catch(Exception ex){MessageBox.Show(ex.Message,"File action failed");}}
    private static void Launch(string p)=>Process.Start(new ProcessStartInfo(p){UseShellExecute=true});
    private static void OpenFolder(string p)=>Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{p}\""){UseShellExecute=true});
}
