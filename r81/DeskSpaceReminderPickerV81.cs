using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// The search textbox is always visible when a file/app action is selected.
// Uses DeskSpaceModuleV10.SearchNow (same universal index); result rows
// appear in a separate WPF popup outside the reminder editor boundaries.
internal static class DeskSpaceReminderPickerV81
{
    internal static bool CanChoose(SearchHit hit,string mode)
        =>DeskSpaceReminderPickerV80.CanChoose(hit,mode);

    internal static string DisplayName(SearchHit hit,string mode)
    {
        string name=string.IsNullOrWhiteSpace(hit.Name)?
            Path.GetFileName(hit.OpenTarget):hit.Name.Trim();
        if(name.Contains('\\')||name.Contains('/'))
            name=Path.GetFileName(name);
        if(mode=="Open application" &&
           (name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
            name=Path.GetFileNameWithoutExtension(name);
        return name;
    }

    internal static FrameworkElement Create(string mode,Action<string,string> chosen)
    {
        var root=new StackPanel{
            Tag="DeskSpaceReminderAutomaticSearch",
            Margin=new Thickness(2,3,2,4)};
        var search=new TextBox{
            Height=32,FontSize=13,Padding=new Thickness(8,4,8,4),
            ToolTip=mode=="Open application"?"Type an application name":
                "Type a file name"};
        root.Children.Add(search);
        var hint=new TextBlock{
            Text=mode=="Open application"?"Search applications by name":
                "Search files by name",
            Margin=new Thickness(3,3,2,0),
            FontSize=11,Foreground=new SolidColorBrush(
                Color.FromRgb(105,98,136))};
        root.Children.Add(hint);

        var rows=new StackPanel();
        var popup=new Popup{
            PlacementTarget=search,Placement=PlacementMode.Bottom,
            VerticalOffset=3,AllowsTransparency=true,
            StaysOpen=false,PopupAnimation=PopupAnimation.Fade};
        var dropdown=new Border{
            Background=new SolidColorBrush(Color.FromRgb(249,247,255)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(175,165,220)),
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(10),
            Width=350,Padding=new Thickness(4),
            Child=new ScrollViewer{
                Content=rows,MaxHeight=270,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto
            }
        };
        popup.Child=dropdown;
        root.Children.Add(popup);

        var throttle=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(230)};
        CancellationTokenSource? ongoing=null;
        int sequence=0;
        throttle.Tick+=async (_,_)=>{
            throttle.Stop();
            ongoing?.Cancel();
            ongoing?.Dispose();
            ongoing=new CancellationTokenSource();
            var token=ongoing.Token;
            int current=++sequence;
            string query=search.Text.Trim();
            rows.Children.Clear();
            if(query.Length<2){
                hint.Text="Enter at least two characters";
                popup.IsOpen=false;
                return;
            }
            hint.Text="Searching…";
            try
            {
                var matches=await Task.Run(()=>
                    DeskSpaceModuleV10.SearchNow(query,new WorkspaceStore().Load(),token),token);
                if(token.IsCancellationRequested||current!=sequence)return;
                var filtered=matches.Where(hit=>CanChoose(hit,mode))
                    .GroupBy(hit=>hit.OpenTarget,StringComparer.OrdinalIgnoreCase)
                    .Select(group=>group.First())
                    .Take(12).ToArray();
                foreach(var hit in filtered)
                {
                    string path=hit.OpenTarget;
                    string label=DisplayName(hit,mode);
                    var row=new StackPanel{
                        Orientation=Orientation.Horizontal,
                        Margin=new Thickness(3,1,3,1)};
                    var icon=DeskSpaceApplicationIconsV65.Get(path);
                    if(icon!=null)
                        row.Children.Add(new Image{
                            Source=icon,Width=23,Height=23,
                            Stretch=Stretch.Uniform,
                            Margin=new Thickness(3,0,9,0)});
                    else
                        row.Children.Add(new TextBlock{
                            Text=mode=="Open application"?"▣":"▤",
                            FontSize=18,Width=32,
                            TextAlignment=TextAlignment.Center});
                    row.Children.Add(new TextBlock{
                        Text=label,FontSize=13,MaxWidth=277,
                        TextTrimming=TextTrimming.CharacterEllipsis,
                        VerticalAlignment=VerticalAlignment.Center});
                    var button=new Button{
                        Content=row,Padding=new Thickness(5,5,5,5),
                        HorizontalContentAlignment=HorizontalAlignment.Left,
                        Background=Brushes.Transparent,
                        BorderThickness=new Thickness(0),
                        Cursor=Cursors.Hand,
                        ToolTip=label};
                    button.Click+=(_,_)=>{
                        popup.IsOpen=false;
                        chosen(path,label);
                        hint.Text="Selected: "+label;
                    };
                    rows.Children.Add(button);
                }
                hint.Text=filtered.Length==0?"No matching results":filtered.Length+
                    " matches · select by name";
                popup.IsOpen=filtered.Length>0 && search.IsVisible;
            }
            catch(OperationCanceledException){}
            catch(Exception e)
            {
                if(!token.IsCancellationRequested)
                    hint.Text="Search unavailable: "+e.Message;
                popup.IsOpen=false;
            }
        };
        search.TextChanged+=(_,_)=>{
            popup.IsOpen=false;throttle.Stop();throttle.Start();};
        search.GotKeyboardFocus+=(_,_)=>{
            if(rows.Children.Count>0)popup.IsOpen=true;};
        root.Unloaded+=(_,_)=>{
            throttle.Stop();
            ongoing?.Cancel();ongoing?.Dispose();
            popup.IsOpen=false;
        };
        return root;
    }
}
