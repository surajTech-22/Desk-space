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
        new("status","Component status","View local component health and indexing","System"),
        new("layouts","Window layout profiles","Review or restore monitor window layouts","Windows"),
        new("clipboard","Clipboard conversions","Convert selected text with a preview","Text"),
        new("diagnostics","Export diagnostics","Save privacy-safe troubleshooting ZIP to Downloads","System",true),
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
    };
    private static readonly Dictionary<Window,Window> Open=new();

    internal static IReadOnlyList<Command> Filter(string? query)
    {
        string[] tokens=(query??"").Trim()
            .Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(tokens.Length==0)return Commands;
        if(tokens.Length>10||tokens.Any(x=>x.Length>64))return Array.Empty<Command>();
        return Commands.Where(command=>tokens.All(token=>
            (command.Name+" "+command.Description+" "+command.Group)
                .Contains(token,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(command=>command.Name.StartsWith(
                tokens[0],StringComparison.OrdinalIgnoreCase)?0:1)
            .ThenBy(command=>command.Name,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    private static void Run(Window owner,bool trayVisible,Command command)
    {
        switch(command.Id)
        {
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
            Text="Type to filter · ↑/↓ select · Enter open · Esc close",
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
            Background=Brushes.White,KeyboardNavigation.TabNavigation=KeyboardNavigationMode.Continue};
        AutomationProperties.SetName(list,"Available DeskSpace actions");
        root.Children.Add(list);

        void Refresh()
        {
            var matching=Filter(input.Text);
            list.Items.Clear();
            foreach(var command in matching)
            {
                var box=new StackPanel{Margin=new Thickness(4,5)};
                box.Children.Add(new TextBlock{
                    Text=command.Name,FontSize=14,FontWeight=FontWeights.SemiBold});
                box.Children.Add(new TextBlock{
                    Text=command.Description,FontSize=11,Foreground=Brushes.DimGray,
                    TextWrapping=TextWrapping.Wrap});
                var item=new ListBoxItem{Content=box,Tag=command,Padding=new Thickness(6)};
                AutomationProperties.SetName(item,command.Name+": "+command.Description);
                list.Items.Add(item);
            }
            list.SelectedIndex=list.Items.Count>0?0:-1;
            status.Text=list.Items.Count+" available actions. Execution requires selection.";
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
            if(e.Key==Key.Down || e.Key==Key.Up)
            {
                if(list.Items.Count>0)
                    list.SelectedIndex=Math.Clamp(list.SelectedIndex+
                        (e.Key==Key.Down?1:-1),0,list.Items.Count-1);
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
