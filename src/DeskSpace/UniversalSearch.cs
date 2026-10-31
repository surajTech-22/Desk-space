using System.Diagnostics;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskSpace;

public sealed class SearchHit
{
    public string Category {get;set;}="";
    public string Name {get;set;}="";
    public string Location {get;set;}="";
    public string OpenTarget {get;set;}="";
    public string? NoteText {get;set;}
    public override string ToString()=> $"{Category}  •  {Name}\n{Location}";
}
public sealed class UniversalSearchWindow : Window
{
    private readonly Func<Workspace> workspace;
    private readonly WidgetSettingsV3 preferences;
    private readonly TextBox query=new(){FontSize=18,Margin=new Thickness(13,13,13,6)};
    private readonly ComboBox category=new(){Margin=new Thickness(13,0,13,7)};
    private readonly ListBox list=new(){Margin=new Thickness(13,3,13,10)};
    private readonly TextBlock status=new(){Margin=new Thickness(13,0,13,7),Text="Type at least two characters to search files and applications."};
    private CancellationTokenSource? searchCancel;
    private readonly DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(350)};
    private readonly string[] settingsUris={
        "Display|ms-settings:display", "Sound|ms-settings:sound", "Bluetooth|ms-settings:bluetooth",
        "Network and Internet|ms-settings:network", "Wi-Fi|ms-settings:network-wifi", "Apps|ms-settings:appsfeatures",
        "Default applications|ms-settings:defaultapps", "Storage|ms-settings:storagesense",
        "Windows Update|ms-settings:windowsupdate", "Accounts|ms-settings:yourinfo",
        "Mouse|ms-settings:mousetouchpad", "Keyboard|ms-settings:typing",
        "Notifications|ms-settings:notifications", "Privacy|ms-settings:privacy",
        "Personalization|ms-settings:personalization", "Taskbar|ms-settings:taskbar",
        "Power|ms-settings:powersleep", "Date and time|ms-settings:dateandtime"
    };
    public UniversalSearchWindow(Func<Workspace> workspace,WidgetSettingsV3 preferences)
    {
        this.workspace=workspace;this.preferences=preferences;
        Title="DeskSpace · Universal Search";Width=790;Height=570;Topmost=true;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        category.ItemsSource=new[]{"All","Files","Applications","Settings","Notes","Commands"};category.SelectedIndex=0;
        var root=new DockPanel();var top=new StackPanel();
        query.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        query.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Selected(Launch);e.Handled=true;}};
        category.SelectionChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        debounce.Tick+=(_,_)=>{debounce.Stop();_ = RunSearch();};
        top.Children.Add(new TextBlock{Text="Universal Search",FontWeight=FontWeights.Bold,FontSize=24,Margin=new Thickness(13,12,13,4)});
        top.Children.Add(query);top.Children.Add(category);top.Children.Add(status);
        var buttons=new WrapPanel{Margin=new Thickness(12,0,12,10)};
        Add("Open",Launch);Add("Open folder",OpenFolder);Add("Copy path",CopyPath);
        Add("Copy file",CopyFile);Add("Pin application",Pin);
        top.Children.Add(buttons);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);root.Children.Add(list);Content=root;
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
        Closed+=(_,_)=>{searchCancel?.Cancel();searchCancel?.Dispose();debounce.Stop();};
        void Add(string label,Action<SearchHit> cb){var b=new Button{Content=label,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,7,0)};b.Click+=(_,_)=>Selected(cb);buttons.Children.Add(b);}
    }
    private void Selected(Action<SearchHit> act){if(list.SelectedItem is SearchHit hit)try{act(hit);}catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Search");}}
    private void Launch(SearchHit hit)
    {
        if(hit.Category=="Notes") {Clipboard.SetText(hit.NoteText??"");MessageBox.Show("Note copied. Open DeskSpace to edit.");return;}
        if(!string.IsNullOrWhiteSpace(hit.OpenTarget))
            Process.Start(new ProcessStartInfo(hit.OpenTarget){UseShellExecute=true});
    }
    private void OpenFolder(SearchHit h)
    {
        if(!File.Exists(h.OpenTarget))return;
        Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{h.OpenTarget}\""){UseShellExecute=true});
    }
    private void CopyPath(SearchHit h){Clipboard.SetText(string.IsNullOrWhiteSpace(h.Location)?h.OpenTarget:h.Location);}
    private void CopyFile(SearchHit h)
    {
        if(!File.Exists(h.OpenTarget))return;
        Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection{h.OpenTarget});
    }
    private void Pin(SearchHit h)
    {
        if(h.Category!="Applications" || !File.Exists(h.OpenTarget))return;
        if(!preferences.PinnedApps.Contains(h.OpenTarget,StringComparer.OrdinalIgnoreCase))
        {preferences.PinnedApps.Add(h.OpenTarget);preferences.Save();}
        status.Text="Pinned: "+h.Name;
    }
    private async Task RunSearch()
    {
        searchCancel?.Cancel();searchCancel?.Dispose();searchCancel=new CancellationTokenSource();
        var token=searchCancel.Token;
        var term=query.Text.Trim();var selected=category.SelectedItem as string??"All";
        if(term.Length<2){list.ItemsSource=null;status.Text="Enter two or more characters to search.";return;}
        status.Text="Searching accessible system files, apps, Windows settings and DeskSpace notes…";
        var results=new List<SearchHit>();
        if(selected=="All"||selected=="Settings")
        {
            foreach(var entry in settingsUris){var parts=entry.Split('|');if(parts[0].Contains(term,StringComparison.OrdinalIgnoreCase))results.Add(new SearchHit{Category="Settings",Name=parts[0],Location="Windows Settings",OpenTarget=parts[1]});}
        }
        if(selected=="All"||selected=="Notes")
        {
            try{foreach(var note in workspace().Notes.Where(x=>x.Title.Contains(term,StringComparison.OrdinalIgnoreCase)||x.Content.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(30))
                results.Add(new SearchHit{Category="Notes",Name=note.Title,Location="DeskSpace note",NoteText=note.Content});}catch(IOException){}
        }
        var fastResults=results.ToList();list.ItemsSource=fastResults;
        try
        {
            var more=await Task.Run(()=>CollectSystemResults(term,selected,token),token);
            if(token.IsCancellationRequested)return;
            results.AddRange(more);
            list.ItemsSource=results.OrderBy(x=>x.Category=="Applications"?0:x.Category=="Settings"?1:2).ThenBy(x=>x.Name).Take(150).ToList();
            status.Text=$"{results.Count} matches (first 150 shown). File coverage depends on drive access and scan limits.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!token.IsCancellationRequested)status.Text="Search incomplete: "+ex.Message;}
    }
    private static List<SearchHit> CollectSystemResults(string term,string selected,CancellationToken token)
    {
        var results=new List<SearchHit>();
        if(selected is "All" or "Applications")
        {
            foreach(var start in new []{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)})
            {
                foreach(var file in SafeFiles(start,25000,token).Where(x=>x.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".url",StringComparison.OrdinalIgnoreCase)))
                {
                    token.ThrowIfCancellationRequested();
                    if(System.IO.Path.GetFileNameWithoutExtension(file).Contains(term,StringComparison.OrdinalIgnoreCase))
                        results.Add(new SearchHit{Category="Applications",Name=System.IO.Path.GetFileNameWithoutExtension(file),Location=file,OpenTarget=file});
                    if(results.Count>=100)break;
                }
            }
        }
        if(selected is "All" or "Files")
        {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var drive in DriveInfo.GetDrives())
            {
                token.ThrowIfCancellationRequested();
                if(!drive.IsReady||drive.DriveType!=DriveType.Fixed)continue;
                foreach(var file in SafeFiles(drive.RootDirectory.FullName,350000,token))
                {
                    token.ThrowIfCancellationRequested();
                    if(!System.IO.Path.GetFileName(file).Contains(term,StringComparison.OrdinalIgnoreCase))continue;
                    if(!seen.Add(file))continue;
                    results.Add(new SearchHit{Category="Files",Name=System.IO.Path.GetFileName(file),Location=file,OpenTarget=file});
                    if(results.Count>=150)return results;
                }
            }
        }
        return results;
    }
    private static IEnumerable<string> SafeFiles(string root,int maxCount,CancellationToken token)
    {
        var pending=new Stack<string>();pending.Push(root);int checkedCount=0;
        while(pending.Count>0&&checkedCount<maxCount)
        {
            token.ThrowIfCancellationRequested();var dir=pending.Pop();
            string[] dirs, files;
            try { dirs=Directory.GetDirectories(dir);files=Directory.GetFiles(dir); }
            catch(UnauthorizedAccessException){continue;}
            catch(IOException){continue;}
            catch(System.Security.SecurityException){continue;}
            foreach(var sub in dirs)
            {
                token.ThrowIfCancellationRequested();
                try { if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)pending.Push(sub); }
                catch(UnauthorizedAccessException){}catch(IOException){}
            }
            foreach(var file in files)
            {
                token.ThrowIfCancellationRequested();
                if(++checkedCount>maxCount)yield break;
                yield return file;
            }
        }
    }
}