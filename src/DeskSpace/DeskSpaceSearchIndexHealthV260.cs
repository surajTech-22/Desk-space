using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R260: on-demand, aggregate search index status. No paths, filenames,
// indexed search hits or document contents are exposed to this window.
internal static class DeskSpaceSearchIndexHealthV260
{
    internal sealed record Stats(bool Scanning,int Volumes,int Finished,long Names);

    internal static string Describe(Stats stats)
    {
        if(stats.Volumes<0 || stats.Finished<0 || stats.Names<0 ||
           stats.Finished>stats.Volumes)
            return "Index status unavailable. No file names inspected here.";
        if(stats.Volumes==0)
            return stats.Scanning?
                "Discovering ready drives. File names only; no file contents read.":
                "No indexed volumes reported. Search may not have started.";
        return (stats.Scanning?"Indexing":"Not currently scanning")+
            ": "+stats.Finished+" / "+stats.Volumes+
            " volume(s) completed, "+stats.Names.ToString("N0")+
            " indexed names. No paths or contents shown.";
    }

    private static readonly Dictionary<Window,Window> Open=new();

    internal static Window Create(Window owner)
    {
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(new TextBlock{
            Text="Search index health",
            FontSize=20,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,8)});
        content.Children.Add(new TextBlock{
            Text="Local, on-demand status of the existing multi-drive name index. "+
                 "No file paths, document contents or search terms are displayed. "+
                 "Starting indexing may scan names across all ready drives.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,14)});
        var status=new TextBlock{
            Text="Index status unavailable.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        AutomationProperties.SetName(status,"Aggregate search index status");
        content.Children.Add(status);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        var refresh=new Button{
            Content="Refresh status",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var start=new Button{
            Content="Start indexing names",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var close=new Button{
            Content="Close",Padding=new Thickness(9,6,9,6)};
        AutomationProperties.SetName(refresh,"Refresh index status, no scan");
        AutomationProperties.SetName(start,"Start indexing names on ready drives");
        buttons.Children.Add(refresh);buttons.Children.Add(start);
        buttons.Children.Add(close);content.Children.Add(buttons);
        var window=new Window{
            Title="DeskSpace — Search index status",
            Width=550,Height=310,MinWidth=410,MinHeight=265,
            ShowInTaskbar=false,
            Owner=owner.IsVisible?owner:null,
            WindowStartupLocation=owner.IsVisible?
                WindowStartupLocation.CenterOwner:
                WindowStartupLocation.CenterScreen,
            Content=new ScrollViewer{Content=content,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto}
        };
        void Update()
        {
            try{status.Text=Describe(DeskSpaceSearchIndexV13.SafeCountsV260());}
            catch(Exception ex)when(ex is InvalidOperationException or
                System.ComponentModel.Win32Exception)
            {
                status.Text="Index status unavailable ("+
                    ex.GetType().Name+").";
            }
        }
        refresh.Click+=(_,_)=>Update();
        start.Click+=(_,_)=>{
            if(MessageBox.Show(window,
                "Index names and folder paths on all ready Windows drives? "+
                "The existing index runs in the application process. "+
                "No file contents or credentials are read.",
                "Start local search index",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            DeskSpaceSearchIndexV13.WarmStart();
            Update();
        };
        close.Click+=(_,_)=>window.Close();
        Update();
        return window;
    }
    internal static void Show(Window owner)
    {
        if(Open.TryGetValue(owner,out var existing))
        {
            if(existing.IsLoaded){existing.Activate();return;}
            Open.Remove(owner);
        }
        var dialog=Create(owner);
        Open[owner]=dialog;
        dialog.Closed+=(_,_)=>Open.Remove(owner);
        dialog.Show();
    }
}
