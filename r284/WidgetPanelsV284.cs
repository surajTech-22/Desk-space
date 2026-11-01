using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

public sealed class WidgetDetailsWindow : Window
{
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(3)};
    private readonly TextBlock body=new(){TextWrapping=TextWrapping.Wrap,FontSize=15,Margin=new Thickness(14)};
    private readonly string mode;
    private readonly Func<Workspace> getData;
    public WidgetDetailsWindow(string mode,Func<Workspace> getData)
    {
        this.mode=mode;this.getData=getData;
        Title="DeskSpace · "+mode;Width=390;Height=425;Topmost=true;
        DeskSpaceDesignV283.ApplyWindow(this,DeskSpaceDesignV283.Current);
        body.Foreground=DeskSpaceDesignV283.Solid(
            DeskSpaceDesignV283.Current.Text);
        body.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(DeskSpaceDesignV283.Eyebrow(
            "DESKSPACE  /  "+mode.ToUpperInvariant()));
        content.Children.Add(new TextBlock{
            Text=mode,FontSize=24,FontWeight=FontWeights.Bold,
            Foreground=DeskSpaceDesignV283.Solid(
                DeskSpaceDesignV283.Current.Text),
            Margin=new Thickness(0,0,0,12)
        });
        content.Children.Add(DeskSpaceDesignV283.Card(body));
        Content=new ScrollViewer{Content=content,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        timer.Tick+=(_,_)=>Refresh();Closed+=(_,_)=>timer.Stop();timer.Start();Refresh();
    }
    private void Refresh()
    {
        var ws=getData();
        body.Text=mode switch {
            "Clock" => DateTime.Now.ToString("F"),
            "Calendar" => string.Join("\n",ws.Events.Where(x=>x.Date.Date>=DateTime.Today).OrderBy(x=>x.Date).Take(12).Select(x=>$"{x.Date:g}  {x.Title}").DefaultIfEmpty("No upcoming events")),
            "Notes"=> string.Join("\n\n",ws.Notes.OrderByDescending(x=>x.Updated).Take(5).Select(x=>$"{x.Title}\n{x.Content}").DefaultIfEmpty("No notes")),
            "Reminders" or "Tasks" => string.Join("\n",ws.Reminders.Where(x=>!x.Done).OrderBy(x=>x.Due).Take(20).Select(x=>$"{x.Due:g}  {x.Title}").DefaultIfEmpty("No pending reminders")),
            _=>DateTime.Now.ToString("D")
        };
    }
}

public sealed class WidgetOptionsWindow : Window
{
    public WidgetOptionsWindow(WidgetSettingsV3 prefs,Action apply)
    {
        Title="DeskSpace · Donut options";Width=470;Height=570;Topmost=true;
        MinWidth=370;MinHeight=430;
        DeskSpaceDesignV283.ApplyWindow(this,DeskSpaceDesignV283.Current);
        var panel=new StackPanel{Margin=new Thickness(23)};
        panel.Children.Add(DeskSpaceDesignV283.Eyebrow(
            "DESKSPACE  /  PREFERENCES"));
        panel.Children.Add(new TextBlock{
            Text="Live Widget",FontSize=25,FontWeight=FontWeights.Bold,
            Foreground=DeskSpaceDesignV283.Solid(
                DeskSpaceDesignV283.Current.Text),
            Margin=new Thickness(0,0,0,14)
        });
        AddToggle("Open widget with DeskSpace",prefs.ShowAtLaunch,v=>prefs.ShowAtLaunch=v);
        AddToggle("Start widget with Windows (background)",prefs.StartWithWindows,v=>prefs.StartWithWindows=v);
        AddToggle("Auto-dock when another app is fullscreen",prefs.AutoHideFullscreen,v=>prefs.AutoHideFullscreen=v);
        AddToggle("Keep donut visible during fullscreen",prefs.AlwaysVisible,v=>prefs.AlwaysVisible=v);
        AddToggle("Show live weather in clock",prefs.ShowWeather,v=>prefs.ShowWeather=v);
        panel.Children.Add(new TextBlock{Text="Weather city (no precise location used)",Margin=new Thickness(0,8,0,3)});
        var cities=new ComboBox{ItemsSource=DeskSpaceWeather.Cities.Select(c=>c.Name).ToArray(),SelectedItem=prefs.WeatherCity};
        cities.SelectionChanged+=(_,_)=>{if(cities.SelectedItem is string city)prefs.WeatherCity=city;};
        cities.Background=DeskSpaceDesignV283.Solid(
            DeskSpaceDesignV283.Current.Raised);
        cities.Foreground=DeskSpaceDesignV283.Solid(
            DeskSpaceDesignV283.Current.Text);
        panel.Children.Add(cities);
        panel.Children.Add(new TextBlock{Text="Centre display",Margin=new Thickness(0,8,0,3)});
        var modes=new ComboBox{ItemsSource=new []{"Clock","Date","Calendar","Notes","Reminders","Tasks"},SelectedItem=prefs.CenterMode};
        modes.SelectionChanged+=(_,_)=>prefs.CenterMode=(string)modes.SelectedItem;
        modes.Background=DeskSpaceDesignV283.Solid(
            DeskSpaceDesignV283.Current.Raised);
        modes.Foreground=DeskSpaceDesignV283.Solid(
            DeskSpaceDesignV283.Current.Text);
        panel.Children.Add(modes);
        var save=new Button{Content="Save preferences",Margin=new Thickness(0,20,0,0),Padding=new Thickness(8)};
        DeskSpaceDesignV283.Button(save,true);
        save.Click+=(_,_)=>{try{apply();Close();}catch(Exception ex){MessageBox.Show(ex.Message,"Settings could not be saved");}};
        panel.Children.Add(save);
        Content=new ScrollViewer{Content=panel,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void AddToggle(string name,bool value,Action<bool> change)
        {
            var b=new CheckBox{
                Content=name,IsChecked=value,Margin=new Thickness(0,0,0,12),
                Foreground=DeskSpaceDesignV283.Solid(
                    DeskSpaceDesignV283.Current.Text),
                MinHeight=30};
            b.Checked+=(_,_)=>change(true);b.Unchecked+=(_,_)=>change(false);panel.Children.Add(b);
        }
    }
}

public sealed class AppPinsWindow : Window
{
    private readonly WidgetSettingsV3 prefs;
    private readonly ListBox pins=new(){Margin=new Thickness(12)};
    public AppPinsWindow(WidgetSettingsV3 prefs)
    {
        this.prefs=prefs;Title="DeskSpace · Pinned applications";
        Width=540;Height=455;MinWidth=400;Topmost=true;
        DeskSpaceDesignV283.ApplyWindow(this,DeskSpaceDesignV283.Current);
        var root=new DockPanel();
        var heading=new TextBlock{
            Text="Pinned applications",FontSize=23,FontWeight=FontWeights.Bold,
            Foreground=DeskSpaceDesignV283.Solid(
                DeskSpaceDesignV283.Current.Text),
            Margin=new Thickness(20,18,16,12)
        };
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var bar=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(20,4,12,10)};
        Add("Pin an application",Pin);Add("Launch selected",Launch);Add("Unpin",Unpin);
        DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        DeskSpaceDesignV283.Selection(pins);
        root.Children.Add(pins);Content=root;Refresh();
        void Add(string text,Action action)
        {
            var b=new Button{Content=text,Margin=new Thickness(0,0,7,0)};
            DeskSpaceDesignV283.Button(b);
            b.Click+=(_,_)=>action();bar.Children.Add(b);
        }
    }
    private void Refresh(){pins.ItemsSource=null;pins.ItemsSource=prefs.PinnedApps.ToList();}
    private void Pin()
    {
        var dlg=new Microsoft.Win32.OpenFileDialog{Filter="Applications (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",CheckFileExists=true};
        if(dlg.ShowDialog()==true&&!prefs.PinnedApps.Contains(dlg.FileName,StringComparer.OrdinalIgnoreCase))
        {prefs.PinnedApps.Add(dlg.FileName);prefs.Save();Refresh();}
    }
    private void Launch(){if(pins.SelectedItem is string s)SafeRun(s);}
    private void Unpin(){if(pins.SelectedItem is string s){prefs.PinnedApps.Remove(s);prefs.Save();Refresh();}}
    private static void SafeRun(string path)
    {
        if(!File.Exists(path))return;
        try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
        catch(Exception e){MessageBox.Show(e.Message,"Cannot launch app");}
    }
}
