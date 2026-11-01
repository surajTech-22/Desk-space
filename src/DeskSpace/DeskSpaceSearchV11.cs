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
    private DispatcherTimer? indexRefresh;
    private List<SearchHit> hits = new();
    private bool disposed;
    private static int nextApplicationPinMode;
    private DispatcherTimer? appRefresh;
    // Applied to the next native Search module created from + Pin.
    internal static void RequestApplicationPinMode()=>
        Interlocked.Exchange(ref nextApplicationPinMode,1);

    public DeskSpaceSearchV11(WidgetSettingsV3 settings, Action exit, Action<double> resize)
    { this.settings=settings;this.exit=exit;this.resize=resize; }

    public FrameworkElement Create()
    {
        bool appPinMode=Interlocked.Exchange(ref nextApplicationPinMode,0)==1;
        if(appPinMode)DeskSpaceSearchIndexV13.WarmStart();
        var root = new DockPanel {LastChildFill=true};
                var frame = new Border { Background=new SolidColorBrush(Color.FromRgb(250,251,254)),
            Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=9,ShadowDepth=1,Opacity=.13,
                Color=Color.FromRgb(49,31,98)
            },
            CornerRadius=new CornerRadius(23),
            BorderBrush=new SolidColorBrush(Color.FromRgb(190,198,218)),
            BorderThickness=new Thickness(1.5), Child=root };

        var top=new Grid { Height=46,Margin=new Thickness(3,0,3,0) };
        // The action button itself is 34 DIPs wide: reserve actual space
        // instead of allowing the purple launcher to overlap Search text.
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(42) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(30) });
        var icon=new TextBlock {Text="⌕",FontSize=21,Foreground=new SolidColorBrush(Color.FromRgb(104,80,185)),
            VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false};
        top.Children.Add(icon);
                var field=new TextBox { FontSize=13,BorderThickness=new Thickness(0),Background=Brushes.Transparent,
            VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Case-insensitive search of apps, settings and accessible drives. This PC lists drives; D: report restricts search; content:word searches indexed file contents." };
                        field.ToolTip=appPinMode
            ?"Search installed applications to pin"
            :"Search files, apps, Windows Settings and notes";
        Grid.SetColumn(field,1);top.Children.Add(field);
        // Visible launcher on the existing donut Search bar.
        // Window.GetWindow selects THIS monitor's originating widget.
        var allActions=new Button{
            Content="✦",FontSize=20,Padding=new Thickness(0),
            ToolTip="Open DeskSpace Features and Action Palette",
            MinWidth=34,MinHeight=34,Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),
            Foreground=new SolidColorBrush(Color.FromRgb(104,80,185))
        };
        System.Windows.Automation.AutomationProperties.SetName(
            allActions,"Open DeskSpace Action Palette");
        allActions.Click+=(_,e)=>{
            var owner=Window.GetWindow(frame);
            if(owner!=null)DeskSpaceActionPaletteV197.Show(owner,false);
            e.Handled=true;
        };
        Grid.SetColumn(allActions,0);top.Children.Add(allActions);
        var placeholder=new TextBlock {
            Text="Search",FontSize=12,IsHitTestVisible=false,
            VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(1,0,0,0),
            Foreground=new SolidColorBrush(Color.FromRgb(153,146,172))
        };
        Grid.SetColumn(placeholder,1);top.Children.Add(placeholder);
        field.TextChanged+=(_,_)=>placeholder.Visibility=
            field.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
                var close=new Button { Content="×",FontSize=16,MinWidth=23,Padding=new Thickness(0),
            Background=Brushes.Transparent,BorderThickness=new Thickness(0),
            Foreground=new SolidColorBrush(Color.FromRgb(100,81,151)),
            Cursor=Cursors.Hand,
            Margin=new Thickness(2),ToolTip="Return to previous circle" };
        close.Click+=(_,_)=>exit();
        Grid.SetColumn(close,2);top.Children.Add(close);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);

        if(appPinMode)top.ToolTip="Applications · type to find and pin an installed app";
        var below=new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(7,0,7,8)};
        var status=new TextBlock {Foreground=new SolidColorBrush(Color.FromRgb(113,103,143)),
            FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(8,3,3,4)};
        below.Children.Add(status);
        var list =new StackPanel();
        int selectedIndex=0;
        void HighlightSearchResult()
        {
            int count=list.Children.Count;
            if(count<=0)return;
            selectedIndex=Math.Clamp(selectedIndex,0,count-1);
            for(int i=0;i<count;i++)
                if(list.Children[i] is Border row)
                    row.Background=i==selectedIndex?
                        new SolidColorBrush(Color.FromRgb(234,226,252)):Brushes.White;
            if(list.Children[selectedIndex] is FrameworkElement chosen)
                chosen.BringIntoView();
        }
        // Hook PreviewKeyDown at the Search input. Result-row templates
        // change across versions; this keeps keyboard actions independent.
        System.Windows.Automation.AutomationProperties.SetName(field,
            "DeskSpace Search. Up/Down choose a result, Enter opens, F8 ZIP.");
        field.TextChanged+=(_,_)=>selectedIndex=0;
        field.PreviewKeyDown+=(_,e)=>{
            if(hits.Count<=0)return;
            if(e.Key==Key.Down||e.Key==Key.Up||
               e.Key==Key.PageDown||e.Key==Key.PageUp)
            {
                int step=e.Key==Key.Down?1:e.Key==Key.Up?-1:
                    e.Key==Key.PageDown?7:-7;
                selectedIndex=DeskSpaceSearchActionsV193.NextIndex(
                    list.Children.Count,selectedIndex,step);
                HighlightSearchResult();
                e.Handled=true;
            }
            else if(e.Key==Key.Enter)
            {
                Do(hits[Math.Clamp(selectedIndex,0,hits.Count-1)],"open");
                e.Handled=true;
            }
            else if(e.Key==Key.F8 &&
                DeskSpaceSearchActionsV193.CanZip(
                    hits[Math.Clamp(selectedIndex,0,hits.Count-1)].OpenTarget))
            {
                Do(hits[Math.Clamp(selectedIndex,0,hits.Count-1)],"zip");
                e.Handled=true;
            }
        };
        var scroller=new ScrollViewer {MaxHeight=260,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=list};
        below.Children.Add(scroller);root.Children.Add(below);

        void SetSize()
        {
            var count=list.Children.Count;
            resize(below.Visibility==Visibility.Visible
                ? 80+Math.Min(260,Math.Max(1,count)*44):48);
        }
        async void Do(SearchHit hit,string operation)
        {
            try {
                // Application-only searches are PIN operations, never launch.
                if(appPinMode)
                {
                    if(hit.Category!="Applications")return;
                    if(!settings.PinnedApps.Contains(hit.OpenTarget))
                    {
                        settings.PinnedApps.Add(hit.OpenTarget);
                        settings.Save();
                    }
                    status.Text="Pinned application: "+hit.Name;
                    exit();
                    return;
                }
                switch(operation)
                {
                    case "open":
                                                if(hit.Category is "Notes" or "Reminders" or "Calendar" or "Information")
                            Clipboard.SetText(hit.NoteText??hit.Name);
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
                    case "zip":
                        if(!DeskSpaceSearchActionsV193.CanZip(hit.OpenTarget))break;
                        var downloads=Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads");
                        var picker=new Microsoft.Win32.SaveFileDialog{
                            Title="Create ZIP of selected file",
                            FileName=Path.GetFileName(hit.OpenTarget)+".zip",
                            Filter="ZIP archive (*.zip)|*.zip",DefaultExt=".zip",
                            AddExtension=true,OverwritePrompt=false,
                            InitialDirectory=Directory.Exists(downloads)?downloads:
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)};
                        if(picker.ShowDialog()==true)
                        {
                            status.Text="Creating ZIP of "+hit.Name+"…";
                            await Task.Run(()=>DeskSpaceSearchActionsV193.ZipSingleFile(
                                hit.OpenTarget,picker.FileName));
                            if(!disposed)status.Text="Created ZIP: "+Path.GetFileName(picker.FileName);
                        }
                        break;
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
            foreach(var hit in found.Take(45))
            {
                if(appPinMode && hit.Category!="Applications")continue;
                var item=new Border {Background=Brushes.White,CornerRadius=new CornerRadius(11),
                    Margin=new Thickness(3,1,3,1),Padding=new Thickness(7,2,6,2),MinHeight=40};
                var layout=new DockPanel{LastChildFill=true};
                var chips=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed,
                    VerticalAlignment=VerticalAlignment.Center};
                var more=new Button {
                    Content="⋯",ToolTip="File and app actions",
                    Width=28,Height=27,Padding=new Thickness(0),
                    FontSize=17,BorderThickness=new Thickness(0),
                    Background=new SolidColorBrush(Color.FromRgb(236,232,249)),
                    Foreground=new SolidColorBrush(Color.FromRgb(66,52,113))
                };
                var actions=new ContextMenu();
                void AddAction(string caption,string code,bool available=true)
                {
                    if(!available)return;
                    var action=new MenuItem{Header=caption};
                    action.Click+=(_,_)=>Do(hit,code);
                    actions.Items.Add(action);
                }
                if(appPinMode)
                    AddAction("Pin application","pin");
                else
                {
                    AddAction("Open","open");
                    AddAction("Copy path","path",hit.Category!="Notes");
                    AddAction("Copy file","file",File.Exists(hit.OpenTarget));
                    AddAction("Pin to DeskSpace","pin",
                        hit.Category=="Files"||hit.Category=="Applications");
                    AddAction("Show in Explorer","folder",
                        File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget));
                }
                more.Click+=(_,e)=>{
                    e.Handled=true;
                    more.ContextMenu=actions;
                    actions.PlacementTarget=more;
                    actions.IsOpen=true;
                };
                chips.Children.Add(more);
                if(hit.Category=="Files" &&
                    DeskSpaceSearchActionsV193.CanZip(hit.OpenTarget))
                    chips.Children.Add(Chip("ZIP","Save ZIP of selected file",hit,"zip"));
                DockPanel.SetDock(chips,Dock.Right);layout.Children.Add(chips);
                var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                text.Children.Add(new TextBlock{
                    Text=hit.Name,
                    FontSize=13,FontWeight=FontWeights.SemiBold,
                    Foreground=new SolidColorBrush(Color.FromRgb(45,39,82)),
                    TextTrimming=TextTrimming.CharacterEllipsis});
                text.Children.Add(new TextBlock {Text=hit.Location??"",FontSize=11,
                    Foreground=new SolidColorBrush(Color.FromRgb(119,110,144)),
                    TextTrimming=TextTrimming.CharacterEllipsis});
                var rowText=new StackPanel{Orientation=Orientation.Horizontal};
                if(hit.Category=="Applications" || hit.Category=="Files"){
                    var logo=DeskSpaceShellIconV14.Get(hit.OpenTarget);
                    if(logo!=null)rowText.Children.Add(new Image{
                        Source=logo,Width=20,Height=20,
                        Margin=new Thickness(0,1,7,0),
                        VerticalAlignment=VerticalAlignment.Center});
                }
                rowText.Children.Add(text);
                layout.Children.Add(rowText);item.Child=layout;
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
                    if(!button){Do(hit,appPinMode?"pin":"open");e.Handled=true;}
                };
                list.Children.Add(item);
            }
            below.Visibility=Visibility.Visible;SetSize();
        }
        field.KeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){exit();e.Handled=true;}
            else if(e.Key==Key.Enter && hits.Count>0){Do(hits[0],appPinMode?"pin":"open");e.Handled=true;}
        };
        debounce=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};
        debounce.Tick+=async (_,_)=>{
            debounce.Stop();pending?.Cancel();pending?.Dispose();
            pending=new CancellationTokenSource();var token=pending.Token;
            var term=field.Text.Trim();
            if(term.Length==0 && !appPinMode)
            {hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;SetSize();return;}
            status.Text="Searching…";below.Visibility=Visibility.Visible;SetSize();
            try {
                var found=appPinMode
                    ?DeskSpaceSearchIndexV13.SnapshotApplications()
                        .Where(h=>term.Length==0 ||
                            h.Name.Contains(term,StringComparison.OrdinalIgnoreCase))
                        .GroupBy(h=>h.OpenTarget,StringComparer.OrdinalIgnoreCase)
                        .Select(g=>g.First())
                        .OrderBy(h=>h.Name,StringComparer.OrdinalIgnoreCase)
                        .Take(40).ToList()
                    :await Task.Run(()=>DeskSpaceModuleV10.SearchNow(term,store.Load(),token),token);
                if(disposed || token.IsCancellationRequested || field.Text.Trim()!=term)return;
                                                                var coverage=DeskSpaceSearchIndexV13.Coverage;
                status.ToolTip=coverage.Length>0?coverage:"Waiting for accessible drives";
                                var groups=string.Join(" · ",found
                    .GroupBy(h=>h.Category,StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g=>g.Key)
                    .Select(g=>g.Key+": "+g.Count()));
                status.ToolTip=groups+" | "+DeskSpaceSearchIndexV13.Coverage;
                status.Text=appPinMode
                    ?found.Count+" installed applications · select one to pin"
                    :found.Count+" results · all categories"+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · indexing drives":" · ready")+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · indexing this PC":"");
                Results(found);
                if(found.Count==0){
                    list.Children.Add(new TextBlock{Text="No results",Margin=new Thickness(15)});
                    SetSize();
                }
            } catch(OperationCanceledException){}
            catch(Exception ex){if(!token.IsCancellationRequested)status.Text=ex.Message;}
        };
                indexRefresh=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1.7)};
        indexRefresh.Tick+=(_,_)=>{
            if(disposed || string.IsNullOrWhiteSpace(field.Text))
            {indexRefresh.Stop();return;}
            // One final refresh is required after the index completes.
            debounce.Stop();debounce.Start();
            if(!DeskSpaceSearchIndexV13.IsIndexing)indexRefresh.Stop();
        };
        field.TextChanged+=(_,_)=>{
            debounce.Stop();debounce.Start();
            if(!indexRefresh.IsEnabled)indexRefresh.Start();
        };
        frame.Loaded+=(_,_)=>{
            resize(48);field.Focus();Keyboard.Focus(field);
            if(appPinMode)
            {
                // Discovery of Start menu apps is asynchronous. Refresh on
                // the UI dispatcher while keeping the text caret focused.
                appRefresh=new DispatcherTimer{
                    Interval=TimeSpan.FromMilliseconds(700)};
                appRefresh.Tick+=(_,_)=>{
                    if(disposed){appRefresh?.Stop();return;}
                    debounce.Stop();debounce.Start();
                };
                appRefresh.Start();
                debounce.Start();
            }
        };
        return frame;
    }
    public void Dispose()
    {
        disposed=true;appRefresh?.Stop();
        debounce?.Stop();pending?.Cancel();pending?.Dispose();
    }
}