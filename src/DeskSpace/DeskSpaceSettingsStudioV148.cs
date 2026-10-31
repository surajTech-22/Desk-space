using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace DeskSpace;

// Independent visual preferences: never alter passwords, workspace data,
// captured Excel mappings, app launch paths, or existing widget-v3.json.
// All WidgetShellWindow brushes reference Current to support live refresh.
internal sealed class DeskSpaceAppearanceV148
{
    public string Style {get;set;}="Soft";
    public string Mode {get;set;}="System";
    public string Accent {get;set;}="#7364D3";
    public double Scale {get;set;}=1;
    public double Opacity {get;set;}=.96;
    public int Softness {get;set;}=16;
    private static DeskSpaceAppearanceV148? cached;
    internal static string PathName=>System.IO.Path.Combine(WorkspaceStore.Root,"appearance-v148.json");
    internal static DeskSpaceAppearanceV148 Current=>cached??=Load();
    internal static DeskSpaceAppearanceV148 Load()
    {
        try{
            if(!File.Exists(PathName))return new();
            var loaded=JsonSerializer.Deserialize<DeskSpaceAppearanceV148>(
                File.ReadAllText(PathName))??new();
            return Sanitize(loaded);
        }catch(Exception ex) when(ex is IOException or JsonException or
            UnauthorizedAccessException){System.Diagnostics.Debug.WriteLine(ex);return new();}
    }
    internal static DeskSpaceAppearanceV148 Sanitize(DeskSpaceAppearanceV148 item)
    {
        item.Style=item.Style=="Material"?"Material":"Soft";
        item.Mode=new[]{"Light","Dark","System"}.Contains(item.Mode)?item.Mode:"System";
        item.Accent=TryColor(item.Accent,out _)?item.Accent.ToUpperInvariant():"#7364D3";
        item.Scale=double.IsFinite(item.Scale)?Math.Clamp(item.Scale,.75,1.25):1;
        item.Opacity=double.IsFinite(item.Opacity)?Math.Clamp(item.Opacity,.65,1):.96;
        item.Softness=Math.Clamp(item.Softness,0,32);
        return item;
    }
    internal static bool TryColor(string? value,out Color color)
    {
        color=Colors.MediumPurple;
        if(value==null||value.Length!=7||value[0]!='#')return false;
        if(!int.TryParse(value.Substring(1),System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,out int x))return false;
        color=Color.FromRgb((byte)((x>>16)&255),(byte)((x>>8)&255),(byte)(x&255));
        return true;
    }
    internal static void Save(DeskSpaceAppearanceV148 data)
    {
        // Never advertise uncommitted preferences as active if the disk write fails.
        var next=Sanitize(data);
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            File.WriteAllText(tmp,JsonSerializer.Serialize(next));
            File.Move(tmp,PathName,true);
            cached=next;
        }finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    internal static Color AccentColor
    {
        get{TryColor(Current.Accent,out var c);return c;}
    }
    // Use the draft mode in the settings preview rather than the last saved mode.
    internal static bool IsDarkFor(DeskSpaceAppearanceV148 appearance)
    {
        if(appearance.Mode=="Dark")return true;
        if(appearance.Mode=="Light")return false;
        try
        {
            var value=Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",1);
            return Convert.ToInt32(value)==0;
        }
        catch{return false;}
    }
    private static bool Dark=>IsDarkFor(Current);
    internal static Brush AccentBrush=>new SolidColorBrush(AccentColor);
    internal static Brush LightBrush
    {
        get
        {
            var accent=AccentColor;
            double blend=Current.Style=="Material"?.78:.88;
            // Softness is a tonal, not transparency, adjustment.
            blend=Math.Clamp(blend+(Current.Softness-16)*.003,.67,.94);
            Color baseColor=Dark?Color.FromRgb(32,34,49):Colors.White;
            return new SolidColorBrush(Mix(accent,baseColor,blend));
        }
    }
    internal static Brush SecondaryBrush=>
        new SolidColorBrush(Mix(AccentColor,Dark?Color.FromRgb(41,43,56):
            Colors.White,Current.Style=="Material"?.68:.80));
    internal static Brush InkBrush=>new SolidColorBrush(
        Dark?Color.FromRgb(244,242,255):Color.FromRgb(48,43,73));
    internal static Color Mix(Color a,Color b,double frac)=>
        Color.FromRgb((byte)Math.Clamp((int)(a.R*(1-frac)+b.R*frac),0,255),
                      (byte)Math.Clamp((int)(a.G*(1-frac)+b.G*frac),0,255),
                      (byte)Math.Clamp((int)(a.B*(1-frac)+b.B*frac),0,255));
    internal static void Apply(Window widget)
    {
        widget.Opacity=Current.Opacity;
    }
}

internal sealed class DeskSpaceVisualPresetV148
{
    public string Name {get;set;}="";
    public string Style {get;set;}="Soft";
    public string Mode {get;set;}="System";
    public string Accent {get;set;}="#7364D3";
    public double Scale {get;set;}=1;
    public double Opacity {get;set;}=.96;
    public int Softness {get;set;}=16;
}
internal static class DeskSpaceAppearancePresetsV148
{
    internal static string Filename=>System.IO.Path.Combine(WorkspaceStore.Root,
        "appearance-presets-v148.json");
    internal static List<DeskSpaceVisualPresetV148> Load()
    {
        try{return File.Exists(Filename)?
            (JsonSerializer.Deserialize<List<DeskSpaceVisualPresetV148>>(
                File.ReadAllText(Filename))??new())
                .Where(x=>x!=null && !string.IsNullOrWhiteSpace(x.Name) &&
                    x.Name.Length<=48)
                .Take(20).ToList():new();}
        catch(Exception e) when(e is JsonException or IOException or UnauthorizedAccessException)
        {System.Diagnostics.Debug.WriteLine(e);return new();}
    }
    internal static void Save(DeskSpaceVisualPresetV148 preset)
    {
        if(string.IsNullOrWhiteSpace(preset.Name)||preset.Name.Length>48)
            throw new ArgumentException("Preset name must be 1–48 characters.");
        var entries=Load();
        entries.RemoveAll(x=>x.Name.Equals(preset.Name,StringComparison.OrdinalIgnoreCase));
        entries.Insert(0,preset);
        if(entries.Count>20)entries.RemoveRange(20,entries.Count-20);
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(tmp,JsonSerializer.Serialize(entries));File.Move(tmp,Filename,true);}
        finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
}

// Settings is a STUDIO for the existing 12-sector DeskSpace widget, not a
// second donut widget or a Filee conversion engine. The 6+6 module topology
// remains fixed so module hit targets, sub-donuts, and preferences do not regress.
internal sealed class DeskSpaceSettingsStudioV148:Window
{
    private readonly WidgetSettingsV3 settings;
    private readonly Action refresh;
    private readonly Action general;
    private readonly DeskSpaceAppearanceV148 draft;
    private readonly StackPanel body=new(){Margin=new Thickness(18)};
    private readonly TextBlock subtitle=new(){FontSize=13,Margin=new Thickness(0,4,0,8)};
    private readonly TextBlock status=new(){FontSize=12,TextWrapping=TextWrapping.Wrap};
    // Edits stay local to this settings window until Apply is clicked.
    private readonly List<string> pinnedDraft=new();
    private readonly ListBox pinned=new(){Height=230,MinWidth=270};
    private readonly Canvas donut=new(){Width=290,Height=290};
    private string current="Donut toolbar";
    private bool showEveryMonitorDraft;
    private static DeskSpaceSettingsStudioV148? open;
    private static readonly string[] Modules={
        "Search","Calendar","Calculator","Clock","Weather","Settings",
        "Information","Notes","Reminders","Clipboard","Applications","Excel Functions"
    };
    private static readonly string[] Swatches={
        "#7364D3","#199978","#DA633E","#3589DE","#D64D80",
        "#B77615","#629924","#5F5F63"
    };
    private DeskSpaceSettingsStudioV148(Window owner,WidgetSettingsV3 prefs,
        Action redraw,Action showGeneral)
    {
        settings=prefs;refresh=redraw;general=showGeneral;
        pinnedDraft.AddRange(settings.PinnedApps??new List<string>());
        showEveryMonitorDraft=DeskSpaceMultiMonitorV151.Enabled;
        var source=DeskSpaceAppearanceV148.Current;
        draft=new(){Style=source.Style,Mode=source.Mode,Accent=source.Accent,
            Scale=source.Scale,Opacity=source.Opacity,Softness=source.Softness};
        Title="DeskSpace · Donut & Appearance";Width=1000;Height=750;
        MinWidth=800;MinHeight=570;ShowInTaskbar=true;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(247,248,253));
        var root=new Grid();root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});
        root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var nav=new StackPanel{Background=Brushes.White,Margin=new Thickness(0)};
        nav.Children.Add(new TextBlock{Text="DeskSpace",FontSize=23,FontWeight=FontWeights.Bold,
            Foreground=new SolidColorBrush(Color.FromRgb(23,45,78)),Margin=new Thickness(18,18,8,25)});
        foreach(var name in new[]{"Donut toolbar","Theme","Displays","Presets","General"})
        {
            var selection=name;
            var b=Button(selection,()=>Navigate(selection));
            b.Margin=new Thickness(10,4,10,4);
            nav.Children.Add(b);
        }
        nav.Children.Add(new TextBlock{Text="DeskSpace settings\nYour files and Excel mappings stay local.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(14,27,9,0),
            Foreground=Brushes.Gray});
        Grid.SetColumn(nav,0);root.Children.Add(nav);
        var host=new DockPanel{LastChildFill=true};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(18,9,16,15)};
        footer.Children.Add(Button("Apply changes",Apply));
        footer.Children.Add(Button("Reset appearance",()=>{
            var d=new DeskSpaceAppearanceV148();
            draft.Style=d.Style;draft.Mode=d.Mode;draft.Accent=d.Accent;
            draft.Scale=d.Scale;draft.Opacity=d.Opacity;draft.Softness=d.Softness;
            Navigate(current);
        }));
        footer.Children.Add(status);
        DockPanel.SetDock(footer,Dock.Bottom);host.Children.Add(footer);
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=body};
        host.Children.Add(scroll);Grid.SetColumn(host,1);root.Children.Add(host);
        Content=root;Navigate(current);
        Closed+=(_,_)=>{if(ReferenceEquals(open,this))open=null;};
    }
    internal static void Show(Window owner,WidgetSettingsV3 prefs,
        Action refresh,Action general)
    {
        if(open!=null)
        {
            try{open.Activate();return;}
            catch(InvalidOperationException){open=null;}
        }
        try
        {
            open=new DeskSpaceSettingsStudioV148(owner,prefs,refresh,general);
            open.Show();
            open.Activate();
        }
        catch(Exception ex)
        {
            open=null;
            DeskSpaceSettingsDiagnosticsV151.Report("Open settings",ex);
        }
    }
    private static Button Button(string name,Action click)
    {
        var b=new Button{Content=name,Padding=new Thickness(12,8,12,8),
            Margin=new Thickness(0,0,9,6),Background=Brushes.White,
            HorizontalContentAlignment=HorizontalAlignment.Left,
            BorderBrush=new SolidColorBrush(Color.FromRgb(216,218,235))};
        // WPF treats uncaught Click handlers as fatal dispatcher exceptions.
        // An invalid/stale preset or a failing page must never exit DeskSpace.
        b.Click+=(_,_)=>{
            try{click();}
            catch(Exception ex){DeskSpaceSettingsDiagnosticsV151.Report(
                "Settings action: "+name,ex);}
        };
        return b;
    }
    private static TextBlock Caption(string title)=>new(){
        Text=title,Foreground=new SolidColorBrush(Color.FromRgb(59,69,98)),
        FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,5)};
    private void Navigate(string page)
    {
        current=page;
        try
        {
            body.Children.Clear();
            body.Children.Add(new TextBlock{Text=page,FontSize=25,
                FontWeight=FontWeights.SemiBold,
                Foreground=new SolidColorBrush(Color.FromRgb(30,42,73))});
            if(page=="Donut toolbar")DonutPage();
            else if(page=="Theme")ThemePage();
            else if(page=="Displays")DisplaysPage();
            else if(page=="Presets")PresetsPage();
            else GeneralPage();
        }
        catch(Exception ex)
        {
            body.Children.Clear();
            body.Children.Add(new TextBlock{
                Text="This settings page could not be opened. Other modules remain available.",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)});
            body.Children.Add(Button("Try again",()=>Navigate(page)));
            DeskSpaceSettingsDiagnosticsV151.Report("Settings page: "+page,ex);
        }
    }
    private void DonutPage()
    {
        body.Children.Add(new TextBlock{Text=
            "DeskSpace has six inner tools and six outer modules. Their positions "+
            "are linked to hover gestures and sub-donuts, so moving those fixed "+
            "sectors is intentionally disabled. Pinned shortcuts can be reordered.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray});
        var layout=new StackPanel{Orientation=Orientation.Horizontal};
        var left=new StackPanel{Width=310};
        left.Children.Add(Caption("Live two-ring widget preview"));
        if(donut.Parent is Panel previous)previous.Children.Remove(donut);
        left.Children.Add(donut);
        var right=new StackPanel{Margin=new Thickness(14,2,0,0),Width=340};
        right.Children.Add(Caption("Pinned applications"));
        right.Children.Add(new TextBlock{Text="Order changes apply to the existing Applications module.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray});
        pinned.ItemsSource=pinnedDraft;
        pinned.DisplayMemberPath="";
        // This ListBox is reused when revisiting the Donut page. WPF will
        // throw if it is still parented to the old (detached) page container.
        if(pinned.Parent is Panel oldPinnedHost)oldPinnedHost.Children.Remove(pinned);
        right.Children.Add(pinned);
        var buttons=new WrapPanel{Margin=new Thickness(0,9,0,0)};
        buttons.Children.Add(Button("Move up",()=>MovePin(-1)));
        buttons.Children.Add(Button("Move down",()=>MovePin(1)));
        buttons.Children.Add(Button("Pin application",AddPin));
        buttons.Children.Add(Button("Unpin",RemovePin));
        right.Children.Add(buttons);
        layout.Children.Add(left);layout.Children.Add(right);
        body.Children.Add(layout);
        body.Children.Add(Caption("Donut appearance"));
        body.Children.Add(Slider("Widget size",.75,1.25,draft.Scale,
            v=>{draft.Scale=v;Draw();},v=>$"{v:P0}"));
        body.Children.Add(new TextBlock{
            Text="Scaling the existing whole widget preserves the original "+
            "hit areas and nesting geometry. The centre hole stays aligned "+
            "with the current interaction layout.",
            Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap});
        Draw();
    }
    private void ThemePage()
    {
        body.Children.Add(new TextBlock{Text=
            "Colours and settings apply to DeskSpace's existing widget. "+
            "The preview shows the compact two-ring appearance.",
            Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap});
        body.Children.Add(Caption("Theme style"));
        var types=new WrapPanel();
        types.Children.Add(Button("Soft pastel",()=>{draft.Style="Soft";ThemePageRefresh();}));
        types.Children.Add(Button("Material tonal",()=>{draft.Style="Material";ThemePageRefresh();}));
        body.Children.Add(types);
        body.Children.Add(Caption("Appearance"));
        var modes=new WrapPanel();
        foreach(var type in new[]{"System","Light","Dark"}){
            var local=type;
            modes.Children.Add(Button(type,()=>{draft.Mode=local;ThemePageRefresh();}));
        }
        body.Children.Add(modes);
        body.Children.Add(Caption("Accent colour"));
        var swatches=new WrapPanel();
        foreach(var hex in Swatches){
            var colour=hex;
            var b=Button(hex==draft.Accent?"✓":"●",()=>{
                draft.Accent=colour;ThemePageRefresh();});
            b.Width=52;b.Height=43;b.Foreground=Brushes.White;
            b.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            swatches.Children.Add(b);
        }
        body.Children.Add(swatches);
        var custom=new TextBox{Text=draft.Accent,Width=130,
            HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,5,0,7)};
        custom.LostFocus+=(_,_)=>{
            try{
                if(DeskSpaceAppearanceV148.TryColor(custom.Text,out _)){
                    draft.Accent=custom.Text;Draw();
                }else{custom.Text=draft.Accent;status.Text="Use a colour like #7364D3.";}
            }catch(Exception ex){DeskSpaceSettingsDiagnosticsV151.Report("Accent colour",ex);}
        };
        body.Children.Add(custom);
        body.Children.Add(Slider("Widget opacity",.65,1,draft.Opacity,
            v=>draft.Opacity=v,v=>$"{v:P0}"));
        body.Children.Add(Slider("Colour softness",0,32,draft.Softness,
            v=>{draft.Softness=(int)v;Draw();},v=>$"{v:0}"));
        body.Children.Add(Caption("Preview"));
        if(donut.Parent is Panel previous)previous.Children.Remove(donut);
        body.Children.Add(donut);Draw();
    }
    private void ThemePageRefresh(){Navigate("Theme");}
    private static FrameworkElement Slider(string label,double min,double max,
        double value,Action<double> changed,Func<double,string> display)
    {
        var stack=new StackPanel{Margin=new Thickness(0,7,16,14)};
        var text=new TextBlock{Text=label+" · "+display(value),Margin=new Thickness(0,0,0,4)};
        var slider=new System.Windows.Controls.Slider{Minimum=min,Maximum=max,Value=value,Width=370,
            HorizontalAlignment=HorizontalAlignment.Left};
        slider.ValueChanged+=(_,_)=>{
            try{
                text.Text=label+" · "+display(slider.Value);
                changed(slider.Value);
            }catch(Exception ex){DeskSpaceSettingsDiagnosticsV151.Report(label,ex);}
        };
        stack.Children.Add(text);stack.Children.Add(slider);return stack;
    }
    private void DisplaysPage()
    {
        body.Children.Add(new TextBlock{
            Text="Show the DeskSpace donut on one monitor or on every connected "+
                "monitor of this Windows computer. All widgets use the same "+
                "saved information and Excel mappings.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray});
        body.Children.Add(Caption("Widget display mode"));
        body.Children.Add(Button(
            (showEveryMonitorDraft?"○ ":"● ")+"Only my current monitor",
            ()=>{showEveryMonitorDraft=false;Navigate("Displays");}));
        body.Children.Add(Button(
            (showEveryMonitorDraft?"● ":"○ ")+"Every connected monitor",
            ()=>{showEveryMonitorDraft=true;Navigate("Displays");}));
        var monitors=System.Windows.Forms.Screen.AllScreens;
        body.Children.Add(Caption("Connected displays ("+monitors.Length+")"));
        foreach(var screen in monitors)
        {
            body.Children.Add(new TextBlock{
                Text=(screen.Primary?"Primary · ":"Display · ")+
                    screen.DeviceName+" — "+screen.Bounds.Width+" × "+
                    screen.Bounds.Height,
                Margin=new Thickness(0,3,0,6),
                TextWrapping=TextWrapping.Wrap});
        }
        body.Children.Add(new TextBlock{
            Text="Click Apply changes to save. DeskSpace automatically reconciles "+
                "monitors when screens connect or disconnect. The main donut and "+
                "the Windows auto-updater stay under a single host.",
            Margin=new Thickness(0,12,0,8),Foreground=Brushes.Gray,
            TextWrapping=TextWrapping.Wrap});
    }
    private void PresetsPage()
    {
        body.Children.Add(new TextBlock{Text=
            "Save and recall themes and widget size. Existing workspace profiles "+
            "continue to store centre mode, weather choice and pinned applications "+
            "without storing notes, Excel data or passwords.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray});
        body.Children.Add(Caption("Visual presets"));
        var name=new TextBox{Text="My DeskSpace",Width=260,
            HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,7,0,7)};
        body.Children.Add(name);
        body.Children.Add(Button("Save current appearance",()=>{
            try{
                DeskSpaceAppearancePresetsV148.Save(new DeskSpaceVisualPresetV148{
                    Name=name.Text.Trim(),Style=draft.Style,Mode=draft.Mode,
                    Accent=draft.Accent,Scale=draft.Scale,
                    Opacity=draft.Opacity,Softness=draft.Softness});
                Navigate("Presets");status.Text="Appearance preset saved.";
            }catch(Exception ex){status.Text=ex.Message;}
        }));
        foreach(var preset in DeskSpaceAppearancePresetsV148.Load()){
            var item=preset;
            body.Children.Add(Button(item.Name+" · "+item.Style+" · "+item.Accent,()=>{
                draft.Style=item.Style;draft.Mode=item.Mode;draft.Accent=item.Accent;
                draft.Scale=item.Scale;draft.Opacity=item.Opacity;
                draft.Softness=item.Softness;Apply();
                status.Text="Applied preset "+item.Name+".";
            }));
        }
        body.Children.Add(Caption("Existing workspace layouts"));
        body.Children.Add(Button("Save current workspace profile",()=>{
            try{
                var profile=DeskSpaceWorkspaceProfilesV85.Capture(name.Text,settings);
                DeskSpaceWorkspaceProfilesV85.Save(profile);
                Navigate("Presets");status.Text="Workspace profile saved.";
            }catch(Exception ex){status.Text=ex.Message;}
        }));
        foreach(var profile in DeskSpaceWorkspaceProfilesV85.Load().Where(p=>p!=null &&
            !string.IsNullOrWhiteSpace(p.Name))){
            var selected=profile;
            body.Children.Add(Button("Apply layout · "+selected.Name,()=>{
                try{DeskSpaceWorkspaceProfilesV85.Apply(selected,settings);
                    refresh();Navigate("Presets");status.Text="Layout profile applied.";}
                catch(Exception ex){status.Text=ex.Message;}
            }));
        }
    }
    private void GeneralPage()
    {
        body.Children.Add(new TextBlock{Text=
            "The existing DeskSpace general settings remain intact: "+
            "Windows startup, weather, visibility, and centre mode.",
            TextWrapping=TextWrapping.Wrap});
        body.Children.Add(Button("Open existing general settings",general));
    }
    private void MovePin(int direction)
    {
        int index=pinned.SelectedIndex;
        int destination=index+direction;
        if(index<0||destination<0||destination>=pinnedDraft.Count)return;
        (pinnedDraft[index],pinnedDraft[destination])=
            (pinnedDraft[destination],pinnedDraft[index]);
        pinned.Items.Refresh();pinned.SelectedIndex=destination;
    }
    private void AddPin()
    {
        var chooser=new OpenFileDialog{Filter="Applications|*.exe;*.lnk|All files|*.*",
            Title="Pin a Windows application to DeskSpace"};
        if(chooser.ShowDialog()!=true)return;
        if(pinnedDraft.Contains(chooser.FileName,StringComparer.OrdinalIgnoreCase))return;
        pinnedDraft.Add(chooser.FileName);pinned.Items.Refresh();
    }
    private void RemovePin()
    {
        int i=pinned.SelectedIndex;
        if(i<0||i>=pinnedDraft.Count)return;
        pinnedDraft.RemoveAt(i);pinned.Items.Refresh();
    }
    private static Point Polar(double cx,double cy,double r,double degrees)
    {
        var a=degrees*Math.PI/180;return new Point(cx+r*Math.Cos(a),cy+r*Math.Sin(a));
    }
    private static Geometry Wedge(double centre,double inner,double outer,
        double start,double end)
    {
        var p=new PathFigure{StartPoint=Polar(centre,centre,outer,start),IsClosed=true};
        p.Segments.Add(new ArcSegment(Polar(centre,centre,outer,end),
            new Size(outer,outer),0,false,SweepDirection.Clockwise,true));
        p.Segments.Add(new LineSegment(Polar(centre,centre,inner,end),true));
        p.Segments.Add(new ArcSegment(Polar(centre,centre,inner,start),
            new Size(inner,inner),0,false,SweepDirection.Counterclockwise,true));
        var geometry=new PathGeometry();geometry.Figures.Add(p);return geometry;
    }
    private void Draw()
    {
        donut.Children.Clear();
        var accent=DeskSpaceAppearanceV148.TryColor(draft.Accent,out var a)?a:Colors.MediumPurple;
        var dark=DeskSpaceAppearanceV148.IsDarkFor(draft);
        var backdrop=dark?Color.FromRgb(32,34,49):Colors.White;
        double blend=draft.Style=="Material"?.78:.88;
        blend=Math.Clamp(blend+(draft.Softness-16)*.003,.67,.94);
        var pale=DeskSpaceAppearanceV148.Mix(accent,backdrop,blend);
        var second=DeskSpaceAppearanceV148.Mix(accent,
            dark?Color.FromRgb(41,43,56):Colors.White,
            draft.Style=="Material"?.68:.80);
        donut.Background=new SolidColorBrush(dark?Color.FromRgb(27,29,43):
            Color.FromRgb(246,246,252));
        double radius=126*Math.Clamp(draft.Scale,.75,1.25);
        for(int i=0;i<12;i++)
        {
            bool outer=i>=6;int slot=i%6;
            double inner=outer?radius*.68:radius*.35;
            double outerRadius=outer?radius:radius*.64;
            double angle=-120+slot*60;
            var path=new System.Windows.Shapes.Path{
                Data=Wedge(145,inner,outerRadius,angle+1,angle+58),
                Fill=new SolidColorBrush(i%2==0?pale:second),
                Stroke=Brushes.White,StrokeThickness=2};
            donut.Children.Add(path);
            var pos=Polar(145,145,(inner+outerRadius)/2,angle+29);
            var label=new TextBlock{Text=Modules[i],FontSize=outer?10:9,
                TextAlignment=TextAlignment.Center,Width=76,TextWrapping=TextWrapping.Wrap,
                Foreground=new SolidColorBrush(dark?
                    Color.FromRgb(244,242,255):Color.FromRgb(48,43,73))};
            Canvas.SetLeft(label,pos.X-38);Canvas.SetTop(label,pos.Y-11);
            donut.Children.Add(label);
        }
        var centre=new Ellipse{Width=radius*.65,Height=radius*.65,
            Fill=new SolidColorBrush(accent),Stroke=Brushes.White,StrokeThickness=3};
        Canvas.SetLeft(centre,145-radius*.325);Canvas.SetTop(centre,145-radius*.325);
        donut.Children.Add(centre);
        var centerText=new TextBlock{Text="DeskSpace",Width=radius*.65,
            FontSize=12,Foreground=Brushes.White,FontWeight=FontWeights.Bold,
            TextAlignment=TextAlignment.Center};
        Canvas.SetLeft(centerText,145-radius*.325);
        Canvas.SetTop(centerText,137);donut.Children.Add(centerText);
    }
    private void Apply()
    {
        try
        {
            // Cancel/closing the studio has no effect on the shared pin list.
            // Apply is the single commit point for staged application changes.
            settings.PinnedApps ??= new List<string>();
            settings.PinnedApps.Clear();
            foreach(var app in pinnedDraft)settings.PinnedApps.Add(app);
            settings.Save();
            DeskSpaceAppearanceV148.Save(draft);
            DeskSpaceMultiMonitorV151.Configure(showEveryMonitorDraft);
            refresh();
            status.Text="Saved. Appearance, pinned applications and monitor preferences updated.";
        }
        catch(Exception ex){status.Text="Could not save settings: "+ex.Message;}
    }
}


// Only Settings errors are intercepted. Keep an inspectable report in Downloads
// while avoiding process-wide handlers that might swallow unrelated defects.
internal static class DeskSpaceSettingsDiagnosticsV151
{
    internal static void Report(string action,Exception error)
    {
        var message=action+": "+error.Message;
        try
        {
            var dir=System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),"Downloads");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir,
                "DeskSpace_Settings_Errors.log"),
                "["+DateTime.Now.ToString("O")+"] "+action+Environment.NewLine+
                error+Environment.NewLine+Environment.NewLine);
        }
        catch(Exception failure){System.Diagnostics.Debug.WriteLine(failure);}
        try{System.Windows.MessageBox.Show(
            "DeskSpace is still running. "+message+
            "\n\nDetails were recorded in Downloads > DeskSpace_Settings_Errors.log.",
            "DeskSpace settings",MessageBoxButton.OK,MessageBoxImage.Warning);}
        catch(Exception failure){System.Diagnostics.Debug.WriteLine(failure);}
    }
}
