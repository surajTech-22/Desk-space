using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DeskSpace;

// Per-drop operation window; closing or pressing Cancel only cancels THIS
// ZIP, never any other widget's held files or shared Deck state.
internal sealed class DeskSpaceZipProgressWindowV206
{
    private readonly Window window;
    private readonly ProgressBar bar;
    private readonly TextBlock description;
    private readonly CancellationTokenSource cancel;
    private bool finished;

    private DeskSpaceZipProgressWindowV206(Window owner,CancellationTokenSource source)
    {
        cancel=source;
        window=new Window
        {
            Title="DeskSpace · Creating ZIP",
            Width=430,Height=220,
            MinWidth=350,ResizeMode=ResizeMode.NoResize,
            ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Owner=owner.IsVisible?owner:null
        };
        if(window.Owner==null)
            window.WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var content=new StackPanel{Margin=new Thickness(18)};
        content.Children.Add(new TextBlock
        {
            Text="Creating a ZIP for the shared Deck",
            FontSize=16,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,9)
        });
        description=new TextBlock
        {
            Text="Inspecting folder safely…",TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,10)
        };
        content.Children.Add(description);
        bar=new ProgressBar
        {
            Minimum=0,Maximum=100,Value=0,Height=18,
            IsIndeterminate=true,Margin=new Thickness(0,0,0,12)
        };
        AutomationProperties.SetName(bar,"ZIP creation progress");
        content.Children.Add(bar);
        var button=new Button
        {
            Content="Cancel ZIP",Width=120,
            HorizontalAlignment=HorizontalAlignment.Right,
            MinHeight=32
        };
        AutomationProperties.SetName(button,"Cancel folder ZIP");
        button.Click+=(_,_)=>RequestCancellation();
        content.Children.Add(button);
        window.Content=content;
        window.Closing+=(_,_)=>{
            if(!finished)RequestCancellation();
        };
    }
    internal static DeskSpaceZipProgressWindowV206 Show(Window owner,
        CancellationTokenSource source)
    {
        var dialog=new DeskSpaceZipProgressWindowV206(owner,source);
        dialog.window.Show();
        return dialog;
    }
    internal void Report(DeskSpaceFolderZipV206.Progress progress)
    {
        if(finished || !window.IsVisible)return;
        bar.IsIndeterminate=false;
        bar.Value=progress.MaxBytes>0?Math.Clamp(
            100d*progress.CompletedBytes/progress.MaxBytes,0,100):100;
        description.Text=$"{progress.Files} file(s) completed · "+
            $"{progress.CompletedBytes/(1024d*1024d):N1} MiB processed · "+
            $"{progress.CurrentName}";
    }
    private void RequestCancellation()
    {
        if(cancel.IsCancellationRequested)return;
        cancel.Cancel();
        description.Text="Cancelling… incomplete ZIP will be deleted.";
    }
    internal void Close()
    {
        finished=true;
        if(window.IsVisible)window.Close();
    }
}
