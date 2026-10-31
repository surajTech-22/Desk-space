using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// Explicit, local actions only. This is not an arbitrary command executor.
// The same window is shared by keyboard and the widget tray, with one palette
// instance per widget owner; user files, secrets and Excel content are never read.
internal static class DeskSpaceActionPaletteV197
{
    internal sealed record Command(string Id,string Name,string Description,
        string Group,bool Confirm=false);
    internal static readonly IReadOnlyList<Command> Commands=new Command[]{
        new("workflowreview","Preview workflows","Review saved reminder actions without running them","Automation"),
        new("vaultlookup","Vault website labels","Find exact HTTPS website labels after manually unlocking","Privacy"),
        new("features","DeskSpace Features","Open the visible Features tab in Workspace","Workspace"),
        new("excel","Excel mappings","Open saved Excel mapping and format tools","Excel"),
        new("reminders","Reminders","Open the existing reminders tab","Workspace"),
        new("notes","Notes","Open the existing notes tab","Workspace"),
        new("quicknote","Quick note","Write and save a new DeskSpace note with explicit consent","Workspace"),
        new("universalsearch","Universal search",
            "Find files across drives, installed apps, settings and DeskSpace content","Files"),
        new("searchhealth","Search index health",
            "Review indexed name counts and optionally start local indexing","System"),
        new("status","Component status","View local component health and indexing","System"),
        new("layouts","Window layout profiles","Review or restore monitor window layouts","Windows"),
        new("clipboard","Clipboard conversions","Convert selected text with a preview","Text"),
        new("diagnostics","Export diagnostics","Save privacy-safe troubleshooting ZIP to Downloads","System",true),
        new("previewfile","Preview file or folder",
            "Choose a local file or folder and inspect it without opening an external app","Files"),
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
    };
    private static readonly Dictionary<Window,Window> Open=new();

    internal static IReadOnlyList<Command> Filter(string? query)=>
        DeskSpacePaletteQueryV259.Filter(Commands,query);

    private static void Run(Window owner,bool trayVisible,Command command)
    {
        switch(command.Id)
        {
            case "universalsearch":
                var search=new UniversalSearchWindow(
                    ()=>new WorkspaceStore().Load(),WidgetSettingsV3.Load());
                if(owner.IsVisible)
                {
                    search.Owner=owner;
                    search.WindowStartupLocation=WindowStartupLocation.CenterOwner;
                }
                search.Show();
                break;
            case "searchhealth":
                DeskSpaceSearchIndexHealthV260.Show(owner);
                break;
            case "workflowreview":
                DeskSpaceWorkflowDryRunV203.Show(owner);
                break;
            case "vaultlookup":
                DeskSpaceVaultContextWindowV202.Show(owner);
                break;
            case "features":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"FEATURES");
                break;
            case "excel":
                _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner);
                break;
            case "reminders":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"REMINDERS");
                break;
            case "notes":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
            case "quicknote":
                var main=Application.Current.Windows.OfType<MainWindow>()
                    .FirstOrDefault(w=>w.IsVisible);
                if(main!=null)main.ShowQuickNoteV270();
                else _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
            case "status":
                DeskSpaceComponentHealthV175.Show(owner,trayVisible);
                break;
            case "layouts":
                DeskSpaceWindowLayoutsV179.Show(owner);
                break;
            case "clipboard":
                DeskSpaceClipboardTransformsV177.Show(owner);
                break;
            case "diagnostics":
                // Always request explicit approval. No background export and
                // never include clipboard contents or raw document paths.
                if(MessageBox.Show(owner,
                    "Create a privacy-safe diagnostics ZIP in Downloads? "+
                    "Review it before sharing. No files will be changed.",
                    "Export DeskSpace diagnostics",
                    MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)
                    return;
                try
                {
                    var archive=DeskSpaceDiagnosticsV161.ExportToDownloads();
                    MessageBox.Show(owner,"Diagnostics ZIP created in Downloads:\n"+
                        Path.GetFileName(archive),"DeskSpace diagnostics",
                        MessageBoxButton.OK,MessageBoxImage.Information);
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
                    InvalidOperationException or System.Security.SecurityException)
                {
                    MessageBox.Show(owner,"Unable to export diagnostics: "+ex.GetType().Name,
                        "DeskSpace diagnostics",MessageBoxButton.OK,MessageBoxImage.Warning);
                }
                break;
            case "downloads":
                string folder=Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),"Downloads");
                if(Directory.Exists(folder))
                {
                    try{Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});}
                    catch(Exception ex) when(ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception or System.Security.SecurityException)
                    {
                        MessageBox.Show(owner,"Unable to open Downloads: "+ex.GetType().Name,
                            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }
                break;
        }
    }
    internal static Window Create(Window owner,bool trayVisible)
    {
        var window=new Window
        {
            Title="DeskSpace actions",Width=530,Height=420,
            MinWidth=420,MinHeight=320,Owner=owner,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Background=Brushes.White,ShowInTaskbar=false,
            ResizeMode=ResizeMode.CanResize
        };
        var root=new DockPanel{Margin=new Thickness(16)};
        window.Content=root;
        var heading=new TextBlock{
            Text="DeskSpace actions",FontSize=20,FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(36,27,65)),
            Margin=new Thickness(0,0,0,8)
        };
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var hint=new TextBlock{
            Text="Type words or @Windows/@System/@Files · ↑/↓, PgUp/PgDn, Home/End · Enter open · Esc close",
            Foreground=Brushes.DimGray,FontSize=12,
            Margin=new Thickness(0,0,0,12)
        };
        DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        var input=new TextBox{FontSize=15,Padding=new Thickness(9),
            MinHeight=38,Margin=new Thickness(0,0,0,10)};
        AutomationProperties.SetName(input,"Find a DeskSpace action");
        DockPanel.SetDock(input,Dock.Top);root.Children.Add(input);
        var status=new TextBlock{Foreground=Brushes.DimGray,FontSize=12,
            Margin=new Thickness(2,8,0,0)};
        DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
        var list=new ListBox{BorderThickness=new Thickness(0),
            Background=Brushes.White};
        KeyboardNavigation.SetTabNavigation(list,KeyboardNavigationMode.Continue);
        AutomationProperties.SetName(list,"Available DeskSpace actions");
        root.Children.Add(list);

        void Refresh()
        {
            var matching=Filter(input.Text);
            list.Items.Clear();
            foreach(var command in matching)
            {
                var box=new StackPanel{Margin=new Thickness(4,5,4,5)};
                box.Children.Add(new TextBlock{
                    Text=command.Name,FontSize=14,FontWeight=FontWeights.SemiBold});
                box.Children.Add(new TextBlock{
                    Text=command.Group+" · "+(command.Confirm?"Confirmation required":"Manual action"),
                    FontSize=10,Foreground=Brushes.DimGray});
                box.Children.Add(new TextBlock{
                    Text=command.Description,FontSize=11,Foreground=Brushes.DimGray,
                    TextWrapping=TextWrapping.Wrap});
                var item=new ListBoxItem{Content=box,Tag=command,Padding=new Thickness(6)};
                AutomationProperties.SetName(item,command.Name+": "+command.Description);
                list.Items.Add(item);
            }
            list.SelectedIndex=list.Items.Count>0?0:-1;
            status.Text=list.Items.Count==0?
                "No matching actions. Try @Windows, @System or clear the filter.":
                list.Items.Count+" matching actions. Select an action to run.";
        }
        void Activate()
        {
            if(list.SelectedItem is not ListBoxItem item ||
               item.Tag is not Command command)return;
            // This is strictly an explicit key or mouse command, never hover.
            Run(owner,trayVisible,command);
        }
        input.TextChanged+=(_,_)=>Refresh();
        input.PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Down || e.Key==Key.Up ||
               e.Key==Key.PageDown || e.Key==Key.PageUp ||
               e.Key==Key.Home || e.Key==Key.End)
            {
                if(list.Items.Count>0)
                {
                    int next=e.Key switch {
                        Key.Home=>0, Key.End=>list.Items.Count-1,
                        _=>DeskSpacePaletteQueryV259.Navigate(
                            list.SelectedIndex,list.Items.Count,
                            e.Key==Key.Down?1:e.Key==Key.Up?-1:
                            e.Key==Key.PageDown?7:-7)
                    };
                    list.SelectedIndex=next;
                    list.ScrollIntoView(list.SelectedItem);
                }
                e.Handled=true;
            }
            else if(e.Key==Key.Enter){Activate();e.Handled=true;}
        };
        list.MouseDoubleClick+=(_,_)=>Activate();
        list.KeyDown+=(_,e)=>{
            if(e.Key==Key.Enter){Activate();e.Handled=true;}
        };
        window.PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){window.Close();e.Handled=true;}
        };
        Refresh();
        window.Loaded+=(_,_)=>input.Focus();
        return window;
    }
    internal static void Show(Window owner,bool trayVisible)
    {
        if(Open.TryGetValue(owner,out var existing))
        {
            if(existing.IsVisible){existing.Activate();return;}
            Open.Remove(owner);
        }
        var window=Create(owner,trayVisible);
        Open[owner]=window;
        window.Closed+=(_,_)=>Open.Remove(owner);
        window.Show();
        window.Activate();
    }
}
