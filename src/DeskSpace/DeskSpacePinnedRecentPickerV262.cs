using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// Only opened by an explicit RIGHT click on a pinned application sector.
// Lists up to 10 recent *document names* matched to that specific app's
// registered Windows handler. No global path history or watcher.
internal static class DeskSpacePinnedRecentPickerV262
{
    internal static Window Create(Window owner,string pinned)
    {
        var window=new Window{
            Title="DeskSpace — Recent documents",
            Width=570,Height=495,MinWidth=420,MinHeight=310,
            Owner=owner.IsVisible?owner:null,
            ShowInTaskbar=false,
            // CenterOwner and CenterScreen use unconstrained WPF placement;
            // the attached monitor fence positions in physical pixels.
            WindowStartupLocation=WindowStartupLocation.Manual};
        DeskSpacePinnedRecentMonitorV264.Attach(window,owner);
        var body=new StackPanel{Margin=new Thickness(16)};
        body.Children.Add(new TextBlock{
            Text="Pinned application · recent documents",
            FontSize=18,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,7)});
        body.Children.Add(new TextBlock{
            Text="Only this pinned app's matching Windows Recent document shortcuts "+
                 "are inspected when this view opens. No document contents are read. "+
                 "Select a document to open it explicitly.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,14)});
        var choices=DeskSpacePinnedRecentV122.CandidatesV262(pinned);
        if(choices.Count==0)
            body.Children.Add(new TextBlock{
                Text="No matching recent documents. You can still open the pinned app.",
                TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(0,0,0,14)});
        foreach(var choice in choices)
        {
            var document=choice.Path;
            var name=Path.GetFileName(document);
            var button=new Button{
                Content=name+"  ·  "+choice.LastUsed.ToLocalTime().ToString("g"),
                Padding=new Thickness(9,7,9,7),
                HorizontalContentAlignment=HorizontalAlignment.Left,
                Margin=new Thickness(0,0,0,6),
                ToolTip="Open selected matching recent document",
            };
            AutomationProperties.SetName(button,"Open recent document: "+name);
            button.Click+=(_,_)=>{
                // Re-resolve the OS handler at click time: a default app or
                // the document itself may have changed since the preview.
                if(!DeskSpacePinnedRecentV122.CanLaunchRecentV262(pinned,document))
                {
                    MessageBox.Show(window,
                        "Document is unavailable or no longer assigned to this pinned app.",
                        "DeskSpace",MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Process.Start(new ProcessStartInfo(document){
                        UseShellExecute=true});
                    window.Close();
                }
                catch(Exception ex)when(ex is InvalidOperationException or
                    System.ComponentModel.Win32Exception or
                    UnauthorizedAccessException)
                {
                    MessageBox.Show(window,
                        "Unable to open the selected document ("+
                        ex.GetType().Name+").",
                        "DeskSpace",MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };
            body.Children.Add(button);
        }
        var actions=new StackPanel{
            Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,8,0,0)};
        var openApp=new Button{
            Content="Open pinned app only",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var close=new Button{
            Content="Close",
            Padding=new Thickness(9,6,9,6)};
        openApp.Click+=(_,_)=>{
            if(!File.Exists(pinned))return;
            try
            {
                Process.Start(new ProcessStartInfo(pinned){
                    UseShellExecute=true});
                window.Close();
            }
            catch(Exception ex)when(ex is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                UnauthorizedAccessException)
            {
                MessageBox.Show(window,
                    "Unable to open pinned application ("+
                    ex.GetType().Name+").",
                    "DeskSpace",MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
        close.Click+=(_,_)=>window.Close();
        actions.Children.Add(openApp);actions.Children.Add(close);
        body.Children.Add(actions);
        window.Content=new ScrollViewer{
            Content=body,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        return window;
    }

    internal static void Show(Window owner,string pinned)
    {
        var window=Create(owner,pinned);
        window.Show();
        window.Activate();
    }
}
