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
    private int requestSequence;
    public UniversalSearchWindow(Func<Workspace> workspace,WidgetSettingsV3 preferences)
    {
        this.workspace=workspace;this.preferences=preferences;
        Title="DeskSpace · Universal Search";Width=790;Height=570;Topmost=true;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        category.ItemsSource=DeskSpaceUniversalSearchPolicyV269.Categories;category.SelectedIndex=0;
        var root=new DockPanel();var top=new StackPanel();
        query.TextChanged+=(_,_)=>{requestSequence++;searchCancel?.Cancel();debounce.Stop();debounce.Start();};
        query.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Selected(Launch);e.Handled=true;}};
        category.SelectionChanged+=(_,_)=>{requestSequence++;searchCancel?.Cancel();debounce.Stop();debounce.Start();};
        debounce.Tick+=(_,_)=>{debounce.Stop();_ = RunSearch();};
        top.Children.Add(new TextBlock{Text="Universal Search",FontWeight=FontWeights.Bold,FontSize=24,Margin=new Thickness(13,12,13,4)});
        top.Children.Add(query);top.Children.Add(category);top.Children.Add(status);
        var buttons=new WrapPanel{Margin=new Thickness(12,0,12,10)};
        Add("Open",Launch);Add("Open folder",OpenFolder);Add("Copy path",CopyPath);
        Add("Copy file",CopyFile);Add("Pin application",Pin);
        var actions=new Button{Content="Commands",Padding=new Thickness(10,5,10,5),
            ToolTip="Open the complete DeskSpace Action Palette",Margin=new Thickness(0,0,7,0)};
        actions.Click+=(_,_)=>DeskSpaceActionPaletteV197.Show(this,false);
        buttons.Children.Add(actions);
        top.Children.Add(buttons);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);root.Children.Add(list);Content=root;
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
        PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){Close();e.Handled=true;}
        };
        Closed+=(_,_)=>{requestSequence++;searchCancel?.Cancel();searchCancel?.Dispose();debounce.Stop();};
        void Add(string label,Action<SearchHit> cb){var b=new Button{Content=label,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,7,0)};b.Click+=(_,_)=>Selected(cb);buttons.Children.Add(b);}
    }
    private void Selected(Action<SearchHit> act){if(list.SelectedItem is SearchHit hit)try{act(hit);}catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Search");}}
    private void Launch(SearchHit hit)
    {
        if(hit.Category is "Notes" or "Reminders" or "Calendar" or "Information")
        {
            if(hit.Category=="Information" &&
               MessageBox.Show(this,"Copy this Information entry to the clipboard? "+
                   "Its contents may be private.","DeskSpace Search",
                   MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)
                return;
            Clipboard.SetText(hit.NoteText??hit.Name);
            MessageBox.Show(this,"Selected DeskSpace entry copied by request. "+
                "No file or app was launched.","DeskSpace Search");
            return;
        }
        if(!string.IsNullOrWhiteSpace(hit.OpenTarget))
            Process.Start(new ProcessStartInfo(hit.OpenTarget){UseShellExecute=true});
    }
    private void OpenFolder(SearchHit h)
    {
        if(h.Category!="Files")return;
        if(Directory.Exists(h.OpenTarget))
            Process.Start(new ProcessStartInfo(h.OpenTarget){UseShellExecute=true});
        else if(File.Exists(h.OpenTarget))
            Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{h.OpenTarget}\""){UseShellExecute=true});
    }
    private void CopyPath(SearchHit h)
    {
        if(h.Category is "Files" or "Applications" && !string.IsNullOrWhiteSpace(h.OpenTarget))
            Clipboard.SetText(h.OpenTarget);
    }
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
        searchCancel?.Cancel();
        searchCancel?.Dispose();
        searchCancel=new CancellationTokenSource();
        var token=searchCancel.Token;
        int sequence=requestSequence;
        var term=query.Text.Trim();
        var selected=category.SelectedItem as string??"All";
        if(term.Length<2)
        {
            list.ItemsSource=null;
            status.Text="Enter two or more characters to search.";
            return;
        }
        status.Text="Searching apps, Windows settings and indexed names across ready drives…";
        // Capture Workspace only on the WPF thread; the single shared search
        // index performs file/application discovery off the UI thread.
        Workspace snapshot;
        try{snapshot=workspace();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            status.Text="Workspace unavailable ("+ex.GetType().Name+").";
            return;
        }
        try
        {
            var indexed=await Task.Run(()=>
                DeskSpaceSearchIndexV13.Search(term,snapshot,token),token);
            if(!DeskSpaceUniversalSearchPolicyV269.Accept(
                sequence,requestSequence,token.IsCancellationRequested,
                term,query.Text.Trim(),selected,category.SelectedItem as string))
                return;
            var filtered=DeskSpaceUniversalSearchPolicyV269.Filter(indexed,selected);
            list.ItemsSource=filtered;
            status.Text=filtered.Count+" matching result(s)"+
                (DeskSpaceSearchIndexV13.IsIndexing
                    ?" · discovering additional drive names":"")+
                ". Select a result to act.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            if(!token.IsCancellationRequested &&
               sequence==requestSequence)
                status.Text="Search unavailable ("+ex.GetType().Name+").";
        }
    }
}
