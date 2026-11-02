using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// Manual, local-only text comparison. Does not read clipboard automatically,
// persist input, watch foreground windows or access the network or disk.
internal static class DeskSpaceTextCompareV328
{
    internal sealed record Change(char Kind,string Text);
    internal sealed record Result(IReadOnlyList<Change> Lines,int Added,int Removed,int Same);
    internal const int MaximumCharacters=60000;
    internal const int MaximumLines=500;
    private static readonly Dictionary<Window,Window> Open=new();

    internal static Result Compare(string? oldText,string? newText)
    {
        string left=(oldText??"").Replace("\r\n","\n").Replace('\r','\n');
        string right=(newText??"").Replace("\r\n","\n").Replace('\r','\n');
        if(left.Length>MaximumCharacters||right.Length>MaximumCharacters)
            throw new ArgumentException("Text comparison supports at most 60,000 characters per side.");
        string[] a=left.Length==0?Array.Empty<string>():left.Split('\n');
        string[] b=right.Length==0?Array.Empty<string>():right.Split('\n');
        if(a.Length>MaximumLines||b.Length>MaximumLines)
            throw new ArgumentException("Text comparison supports at most 500 lines per side.");
        int n=a.Length,m=b.Length;
        // Bounded dynamic LCS ensures inserting a line does not make every
        // subsequent line appear modified. No external diff service or AI.
        var dp=new int[n+1,m+1];
        for(int i=n-1;i>=0;i--)
            for(int j=m-1;j>=0;j--)
                dp[i,j]=string.Equals(a[i],b[j],StringComparison.Ordinal)
                    ?1+dp[i+1,j+1]:Math.Max(dp[i+1,j],dp[i,j+1]);

        var changes=new List<Change>(n+m);
        int x=0,y=0,added=0,removed=0,same=0;
        while(x<n||y<m)
        {
            if(x<n && y<m && string.Equals(a[x],b[y],StringComparison.Ordinal))
            {
                changes.Add(new Change(' ',a[x++]));y++;same++;
            }
            else if(y<m && (x==n||dp[x,y+1]>=dp[x+1,y]))
            {
                changes.Add(new Change('+',b[y++]));added++;
            }
            else
            {
                changes.Add(new Change('-',a[x++]));removed++;
            }
        }
        return new Result(changes,added,removed,same);
    }

    internal static string Render(Result result)
    {
        var lines=new List<string>(result.Lines.Count+2){
            $"Added: {result.Added}   Removed: {result.Removed}   Unchanged: {result.Same}",
            "  (comparison is case-sensitive and preserves original line order)"
        };
        lines.AddRange(result.Lines.Select(line=>line.Kind+" "+line.Text));
        return string.Join(Environment.NewLine,lines);
    }

    internal static Window Create(Window? owner=null)
    {
        var window=new Window{
            Title="DeskSpace — Compare text",Width=800,Height=640,
            MinWidth=560,MinHeight=430,ShowInTaskbar=false,
            WindowStartupLocation=owner!=null && owner.IsVisible
                ?WindowStartupLocation.CenterOwner
                :WindowStartupLocation.CenterScreen,
            Background=Brushes.White
        };
        if(owner!=null && owner.IsVisible)window.Owner=owner;
        var root=new DockPanel{Margin=new Thickness(15)};
        window.Content=root;
        var top=new StackPanel();
        top.Children.Add(new TextBlock{
            Text="Compare two text blocks",FontSize=22,
            FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,4)
        });
        top.Children.Add(new TextBlock{
            Text="Paste text manually. Nothing is saved, uploaded or monitored. "+
                "Comparison is limited to 500 lines per side.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,10)
        });
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var footer=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,9,0,0)};
        var compare=new Button{Content="Compare",Padding=new Thickness(13,7),
            MinWidth=92,Margin=new Thickness(0,0,8,0)};
        var copy=new Button{Content="Copy comparison",Padding=new Thickness(13,7),
            Margin=new Thickness(0,0,8,0),IsEnabled=false};
        var clear=new Button{Content="Clear all",Padding=new Thickness(13,7)};
        var status=new TextBlock{Margin=new Thickness(12,7,0,0),
            Foreground=Brushes.DimGray,Text="Ready"};
        footer.Children.Add(compare);footer.Children.Add(copy);
        footer.Children.Add(clear);footer.Children.Add(status);
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var columns=new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        columns.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        columns.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        columns.RowDefinitions.Add(new RowDefinition{Height=new GridLength(0.75,GridUnitType.Star)});
        columns.Children.Add(new TextBlock{
            Text="Original",FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,2,8,5)
        });
        var labelNew=new TextBlock{Text="Updated",FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(7,2,0,5)};
        Grid.SetColumn(labelNew,1);columns.Children.Add(labelNew);
        TextBox Input(string description)=>new(){
            AcceptsReturn=true,AcceptsTab=true,TextWrapping=TextWrapping.NoWrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            FontFamily=new FontFamily("Consolas"),FontSize=12,
            Padding=new Thickness(7),MaxLength=MaximumCharacters,
            ToolTip=description
        };
        var original=Input("Original text, manually pasted");
        var updated=Input("Updated text, manually pasted");
        Grid.SetRow(original,1);columns.Children.Add(original);
        Grid.SetRow(updated,1);Grid.SetColumn(updated,1);
        updated.Margin=new Thickness(5,0,0,0);columns.Children.Add(updated);
        var heading=new TextBlock{Text="Line differences (+ added, - removed)",
            FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,4)};
        Grid.SetRow(heading,2);Grid.SetColumnSpan(heading,2);
        columns.Children.Add(heading);
        var output=Input("Read-only, locally calculated line comparison");
        output.IsReadOnly=true;output.IsTabStop=false;
        Grid.SetRow(output,3);Grid.SetColumnSpan(output,2);columns.Children.Add(output);
        root.Children.Add(columns);
        AutomationProperties.SetName(original,"Original text");
        AutomationProperties.SetName(updated,"Updated text");
        AutomationProperties.SetName(output,"Comparison results");
        void ResetResult(){output.Clear();copy.IsEnabled=false;status.Text="Changed — compare again";}
        original.TextChanged+=(_,_)=>ResetResult();
        updated.TextChanged+=(_,_)=>ResetResult();
        compare.Click+=(_,_)=>{
            try
            {
                var result=Compare(original.Text,updated.Text);
                output.Text=Render(result);
                copy.IsEnabled=true;
                status.Text=$"{result.Added} added · {result.Removed} removed";
            }
            catch(ArgumentException ex){ResetResult();status.Text=ex.Message;}
        };
        copy.Click+=(_,_)=>{
            if(copy.IsEnabled && output.Text.Length>0)
                Clipboard.SetText(output.Text);
        };
        clear.Click+=(_,_)=>{
            original.Clear();updated.Clear();output.Clear();
            copy.IsEnabled=false;status.Text="Cleared from this window";
        };
        window.Closed+=(_,_)=>{
            original.Clear();updated.Clear();output.Clear();
        };
        return window;
    }

    internal static void Show(Window owner)
    {
        if(Open.TryGetValue(owner,out var existing))
        {
            if(existing.IsVisible){existing.Activate();return;}
            Open.Remove(owner);
        }
        var window=Create(owner);
        Open[owner]=window;
        window.Closed+=(_,_)=>Open.Remove(owner);
        window.Show();
    }
}
