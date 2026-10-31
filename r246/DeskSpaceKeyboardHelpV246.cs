using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// R246: accessible, local, user-invoked shortcuts reference.
// Only shortcuts that already exist in the DeskSpace source are listed.
// No global hotkey registration, keyboard hooks, telemetry or key injection.
internal static class DeskSpaceKeyboardHelpV246
{
    internal sealed record Shortcut(string Scope,string Keys,string Action);
    internal static IReadOnlyList<Shortcut> Supported()=>new[]
    {
        new Shortcut("Workspace","F1","Open this keyboard shortcuts guide"),
        new Shortcut("Workspace","Ctrl+K","Open searchable Action Palette"),
        new Shortcut("Search","Up / Down","Select previous or next result"),
        new Shortcut("Search","Page Up / Page Down","Jump seven search results"),
        new Shortcut("Search","Enter","Open the selected result"),
        new Shortcut("Search","F8","Create a ZIP for eligible selected file"),
        new Shortcut("Root donut","Left / Right","Move between donut sectors"),
        new Shortcut("Root donut","Up / Down","Move between the two donut rings"),
        new Shortcut("Root donut","Home / End","Go to first or last donut sector"),
        new Shortcut("This guide","Escape","Close the guide")
    };

    private static readonly Dictionary<Window,Window> Open=new();

    internal static Window Create(Window owner)
    {
        var content=new StackPanel{Margin=new Thickness(18),MaxWidth=680};
        content.Children.Add(new TextBlock{
            Text="DeskSpace keyboard shortcuts",
            FontSize=20,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.Black,
            Margin=new Thickness(0,0,0,7)});
        content.Children.Add(new TextBlock{
            Text="Only currently implemented DeskSpace shortcuts are listed. "+
                 "Search keys work while Search is focused. Donut keys work "+
                 "during root-donut keyboard navigation. No system-wide "+
                 "shortcuts are installed by this guide.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,14)});
        foreach(var item in Supported())
        {
            var row=new StackPanel{Margin=new Thickness(0,0,0,10)};
            var heading=new TextBlock{
                Text=item.Keys+"   ·   "+item.Scope,
                FontWeight=FontWeights.SemiBold,
                Foreground=Brushes.Black,
                FontSize=13};
            AutomationProperties.SetName(heading,
                item.Scope+" "+item.Keys+": "+item.Action);
            row.Children.Add(heading);
            row.Children.Add(new TextBlock{
                Text=item.Action,Foreground=Brushes.DimGray,
                FontSize=12,TextWrapping=TextWrapping.Wrap});
            content.Children.Add(row);
        }
        var close=new Button{
            Content="Close guide",
            MinWidth=120,Padding=new Thickness(10,5,10,5),
            HorizontalAlignment=HorizontalAlignment.Left,
            Margin=new Thickness(0,6,0,0)};
        AutomationProperties.SetName(close,"Close keyboard shortcuts guide");
        content.Children.Add(close);
        var window=new Window{
            Title="DeskSpace — Keyboard shortcuts",
            Width=525,Height=580,MinWidth=395,MinHeight=320,
            ShowInTaskbar=false,Owner=owner,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Background=Brushes.White,
            Content=new ScrollViewer{
                Content=content,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto}
        };
        close.Click+=(_,_)=>window.Close();
        window.PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape)
            {
                window.Close();
                e.Handled=true;
            }
        };
        return window;
    }

    internal static void Show(Window owner)
    {
        if(Open.TryGetValue(owner,out var previous))
        {
            if(previous.IsLoaded){previous.Activate();return;}
            Open.Remove(owner);
        }
        var dialog=Create(owner);
        Open[owner]=dialog;
        dialog.Closed+=(_,_)=>Open.Remove(owner);
        dialog.Show();
    }
}
