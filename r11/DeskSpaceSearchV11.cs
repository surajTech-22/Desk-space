using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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

// The widget's circle morphs into this single search field.
// Action chips belong to hovered results only; no free-floating toolbar.
internal sealed class DeskSpaceSearchV11 : IDisposable
{
    private readonly WidgetSettingsV3 settings;
    private readonly Action exit;
    private readonly Action<double> resize;
    private readonly WorkspaceStore store = new();
    private CancellationTokenSource? pending;
    private DispatcherTimer? debounce;
    private List<SearchHit> hits = new();
    private bool disposed;

    public DeskSpaceSearchV11(WidgetSettingsV3 settings, Action exit, Action<double> resize)
    { this.settings=settings;this.exit=exit;this.resize=resize; }

    public FrameworkElement Create()
    {
        var root = new DockPanel {LastChildFill=true};
        var frame = new Border { Background=new SolidColorBrush(Color.FromRgb(250,248,255)),
            CornerRadius=new CornerRadius(19),
            BorderBrush=new SolidColorBrush(Color.FromRgb(164,146,212)),
            BorderThickness=new Thickness(1.5), Child=root };

        var top=new Grid { Height=68,Margin=new Thickness(12,1,8,1) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(38) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(47) });
        var icon=new TextBlock {Text="⌕",FontSize=32,Foreground=new SolidColorBrush(Color.FromRgb(104,80,185)),
            VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false};
        top.Children.Add(icon);
        var field=new TextBox { FontSize=19,BorderThickness=new Thickness(0),Background=Brushes.Transparent,
            VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Search files, apps, Windows Settings and notes" };
        Grid.SetColumn(field,1);top.Children.Add(field);
        var close=new Button { Content="×",FontSize=23,MinWidth=38,Padding=new Thickness(0),
            Margin=new Thickness(2),ToolTip="Return to previous circle" };
        close.Click+=(_,_)=>exit();
        Grid.SetColumn(close,2);top.Children.Add(close);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);

        var below=new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(7,0,7,8)};
        var status=new TextBlock {Foreground=new SolidColorBrush(Color.FromRgb(113,103,143)),
            FontSize=12,Margin=new Thickness(12,2,0,6)};
        below.Children.Add(status);
        var list =new StackPanel();
        var scroller=new ScrollViewer {MaxHeight=440,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=list};
        below.Children.Add(scroller);root.Children.Add(below);

        void SetSize()
        {
            var count=list.Children.Count;
            resize(below.Visibility==Visibility.Visible
                ? 92+Math.Min(440,Math.Max(1,count)*64):72);
        }
        void Do(SearchHit hit,string operation)
        {
            try {
                switch(operation)
                {
                    case "open":
                        if(hit.Category=="Notes")Clipboard.SetText(hit.NoteText??"");
                        else Process.Start(new ProcessStartInfo(hit.OpenTarget) {UseShellExecute=true});
                        exit();break;
                    case "path":
                        Clipboard.SetText(hit.OpenTarget);exit();break;
                    case "file":
                        if(File.Exists(hit.OpenTarget)){
                            var names=new StringCollection();names.Add(hit.OpenTarget);
                            Clipboard.SetFileDropList(names);exit();
                        }
                        break;
                    case "pin":
                        if(!settings.PinnedApps.Contains(hit.OpenTarget)){
                            settings.PinnedApps.Add(hit.OpenTarget);settings.Save();
                        }
                        status.Text="Pinned "+hit.Name;break;
                    case "folder":
                        if(File.Exists(hit.OpenTarget))
                            Process.Start(new ProcessStartInfo("explorer.exe",
                                "/select,\""+hit.OpenTarget+"\""){UseShellExecute=true});
                        else if(Directory.Exists(hit.OpenTarget))
                            Process.Start(new ProcessStartInfo(hit.OpenTarget){UseShellExecute=true});
                        exit();break;
                }
            }catch(Exception e){status.Text=e.Message;}
        }
        Button Chip(string text,string toolTip,SearchHit hit,string action)
        {
            var b=new Button {Content=text,ToolTip=toolTip,MinHeight=26,FontSize=12,
                Padding=new Thickness(7,2,7,2),Margin=new Thickness(1,0,1,0),
                BorderThickness=new Thickness(0),
                Background=new SolidColorBrush(Color.FromRgb(228,220,249))};
            b.Click+=(_,e)=>{e.Handled=true;Do(hit,action);};
            return b;
        }
        void Results(List<SearchHit> found)
        {
            hits=found;list.Children.Clear();
            foreach(var hit in found.Take(20))
            {
                var item=new Border {Background=Brushes.White,CornerRadius=new CornerRadius(9),
                    Margin=new Thickness(3,2,3,2),Padding=new Thickness(10,3,8,3),MinHeight=57};
                var layout=new DockPanel{LastChildFill=true};
                var chips=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed,
                    VerticalAlignment=VerticalAlignment.Center};
                chips.Children.Add(Chip("↗","Open",hit,"open"));
                if(hit.Category!="Notes")
                    chips.Children.Add(Chip("⧉","Copy path",hit,"path"));
                if(File.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("▤","Copy file",hit,"file"));
                if(hit.Category=="Files"||hit.Category=="Applications")
                    chips.Children.Add(Chip("☆","Pin",hit,"pin"));
                if(File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("⌂","Show in Explorer",hit,"folder"));
                DockPanel.SetDock(chips,Dock.Right);layout.Children.Add(chips);
                var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                text.Children.Add(new TextBlock{
                    Text=(hit.Category=="Applications"?"▣  ":hit.Category=="Settings"?"⚙  ":
                        hit.Category=="Notes"?"✎  ":"▤  ")+hit.Name,
                    FontSize=14,FontWeight=FontWeights.SemiBold,
                    Foreground=new SolidColorBrush(Color.FromRgb(45,39,82)),
                    TextTrimming=TextTrimming.CharacterEllipsis});
                text.Children.Add(new TextBlock {Text=hit.Location??"",FontSize=11,
                    Foreground=new SolidColorBrush(Color.FromRgb(119,110,144)),
                    TextTrimming=TextTrimming.CharacterEllipsis});
                layout.Children.Add(text);item.Child=layout;
                item.MouseEnter+=(_,_)=>{
                    chips.Visibility=Visibility.Visible;
                    item.Background=new SolidColorBrush(Color.FromRgb(237,232,252));};
                item.MouseLeave+=(_,_)=>{chips.Visibility=Visibility.Collapsed;item.Background=Brushes.White;};
                item.MouseLeftButtonUp+=(_,e)=>{
                    var source=e.OriginalSource as DependencyObject;
                    bool button=false;
                    while(source!=null) {
                        if(source is Button){button=true;break;}
                        source=VisualTreeHelper.GetParent(source);
                    }
                    if(!button){Do(hit,"open");e.Handled=true;}
                };
                list.Children.Add(item);
            }
            below.Visibility=Visibility.Visible;SetSize();
        }
        field.KeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){exit();e.Handled=true;}
            else if(e.Key==Key.Enter && hits.Count>0){Do(hits[0],"open");e.Handled=true;}
        };
        debounce=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(270)};
        debounce.Tick+=async (_,_)=>{
            debounce.Stop();pending?.Cancel();pending?.Dispose();
            pending=new CancellationTokenSource();var token=pending.Token;
            var term=field.Text.Trim();
            if(term.Length==0) {hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;SetSize();return;}
            status.Text="Searching…";below.Visibility=Visibility.Visible;SetSize();
            try {
                var found=await Task.Run(()=>DeskSpaceModuleV10.SearchNow(term,store.Load(),token),token);
                if(disposed || token.IsCancellationRequested || field.Text.Trim()!=term)return;
                status.Text=found.Count+" results · Hover a row for actions";
                Results(found);
                if(found.Count==0){
                    list.Children.Add(new TextBlock{Text="No results",Margin=new Thickness(15)});
                    SetSize();
                }
            } catch(OperationCanceledException){}
            catch(Exception ex){if(!token.IsCancellationRequested)status.Text=ex.Message;}
        };
        field.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        frame.Loaded+=(_,_)=>{resize(72);field.Focus();Keyboard.Focus(field);};
        return frame;
    }
    public void Dispose()
    {
        disposed=true;debounce?.Stop();pending?.Cancel();pending?.Dispose();
    }
}