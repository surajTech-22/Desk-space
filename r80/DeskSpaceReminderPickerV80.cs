using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// The reminder picker is a SMALL embedded view of the EXISTING global
// DeskSpace search engine. This does not create an independent index.
internal static class DeskSpaceReminderPickerV80
{
    internal static bool CanChoose(SearchHit hit,string mode)
    {
        var path=hit.OpenTarget;
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||
           !File.Exists(path))return false;
        if(mode=="Open application")
        {
            var extension=Path.GetExtension(path);
            return (hit.Category=="Applications"||hit.Category=="Files") &&
                (extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||
                 extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase));
        }
        return mode=="Open file" &&
            (hit.Category=="Files"||hit.Category=="Applications");
    }

    internal static FrameworkElement Create(string mode,Action<string> chosen)
    {
        var frame=new StackPanel{Margin=new Thickness(2,4,2,6)};
        frame.Children.Add(new TextBlock{
            Text=mode=="Open application"?"Search installed applications":
                "Search all accessible files",
            FontSize=12,FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(55,48,89)),
            Margin=new Thickness(3,2,3,2)});
        var query=new TextBox{Height=31,FontSize=13,
            ToolTip="DeskSpace universal search (all indexed drives and apps)",
            Margin=new Thickness(2,2,2,2),
            Padding=new Thickness(7,4,7,4)};
        frame.Children.Add(query);
        var status=new TextBlock{
            Text="Type to search using DeskSpace's existing index",
            Foreground=new SolidColorBrush(Color.FromRgb(105,100,140)),
            FontSize=11,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(3,2,2,3)};
        frame.Children.Add(status);
        var found=new StackPanel();
        frame.Children.Add(new ScrollViewer{
            MaxHeight=147,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=found});
        var clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(260)};
        CancellationTokenSource? cancel=null;
        int generation=0;
        clock.Tick+=async (_,_)=>{
            clock.Stop();
            cancel?.Cancel();cancel?.Dispose();
            cancel=new CancellationTokenSource();
            var ct=cancel.Token;
            int serial=++generation;
            var value=query.Text.Trim();
            if(value.Length<2){found.Children.Clear();
                status.Text="Enter at least two characters";return;}
            status.Text="Searching DeskSpace…";
            try
            {
                var results=await Task.Run(()=>
                    DeskSpaceModuleV10.SearchNow(value,new WorkspaceStore().Load(),ct),ct);
                if(ct.IsCancellationRequested||serial!=generation)return;
                var selectable=results.Where(x=>CanChoose(x,mode))
                    .GroupBy(x=>x.OpenTarget,StringComparer.OrdinalIgnoreCase)
                    .Select(g=>g.First()).Take(12).ToArray();
                found.Children.Clear();
                foreach(var hit in selectable)
                {
                    string selectedPath=hit.OpenTarget;
                    var select=new Button{
                        Content=(mode=="Open application"?"▣ ":"▤ ")+hit.Name,
                        HorizontalContentAlignment=HorizontalAlignment.Left,
                        Padding=new Thickness(8,6,8,6),
                        ToolTip=selectedPath,
                        Margin=new Thickness(2,1,2,1),
                        MaxWidth=326,
                        Background=new SolidColorBrush(Color.FromRgb(246,242,255))
                    };
                    select.Click+=(_,_)=>{chosen(selectedPath);
                        status.Text="Selected: "+hit.Name;};
                    found.Children.Add(select);
                }
                status.Text=selectable.Length==0?
                    "No matching selectable items; use Browse file if needed":
                    selectable.Length+" results · select one";
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(!ct.IsCancellationRequested)
                    status.Text="Search unavailable: "+error.Message;
            }
        };
        query.TextChanged+=(_,_)=>{clock.Stop();clock.Start();};
        frame.Unloaded+=(_,_)=>{clock.Stop();cancel?.Cancel();cancel?.Dispose();};
        frame.Tag="DeskSpaceReminderEmbeddedSearch";
        return frame;
    }
}
