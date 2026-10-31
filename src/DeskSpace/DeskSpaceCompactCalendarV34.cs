using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskSpace;

// Calendar preview is a complete, interactive view inside the same 408-DIP
// widget, not a new Window. The existing full Calendar remains unchanged.
internal sealed class DeskSpaceCompactCalendarV34
{
    private readonly WorkspaceStore store = new();
    private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime selected = DateTime.Today;
    private readonly SolidColorBrush ink = new(Color.FromRgb(36,44,72));
    private readonly SolidColorBrush muted = new(Color.FromRgb(111,120,148));
    private readonly SolidColorBrush accent = new(Color.FromRgb(105,84,202));
    private readonly SolidColorBrush light = new(Color.FromRgb(239,235,252));

    public FrameworkElement Create(Action expand, Action close)
    {
        var canvas = new Grid { Width = 408, Height = 408,
            Background = new SolidColorBrush(Color.FromRgb(249,248,254)),
            Clip = new EllipseGeometry(new Rect(0,0,408,408)) };
        var panel = new StackPanel { Width = 280, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        canvas.Children.Add(panel);

        var top = new Grid { Margin = new Thickness(0,0,0,4) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        var exit = SmallButton("←",close);
        top.Children.Add(exit);
        var heading = new TextBlock { Text = "Calendar", FontSize = 17,
            FontWeight = FontWeights.SemiBold, Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
        Grid.SetColumn(heading,1);top.Children.Add(heading);
        var addEvent = SmallButton("+",expand);
        addEvent.FontSize=17;
        addEvent.ToolTip="Add calendar event in full planner";
        Grid.SetColumn(addEvent,2);top.Children.Add(addEvent);
        var expandButton = SmallButton("Expand ↗",expand);
        expandButton.FontSize = 11;
        Grid.SetColumn(expandButton,3);top.Children.Add(expandButton);
        panel.Children.Add(top);

        var days = new UniformGrid { Columns=7, Rows=6, Height=156 };
        var agenda = new TextBlock { TextWrapping=TextWrapping.Wrap,Foreground=ink,
            FontSize=11,MaxHeight=25,TextTrimming=TextTrimming.CharacterEllipsis,
            TextAlignment=TextAlignment.Center,Margin=new Thickness(4,0,4,3) };
        var nav = new Grid { Margin=new Thickness(0,0,0,4) };
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(37) });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(37) });
        var caption = new TextBlock { Foreground=ink,FontSize=14,
            FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,
            TextAlignment=TextAlignment.Center };
        Grid.SetColumn(caption,1);nav.Children.Add(caption);
        var prev = SmallButton("‹",()=>{month=month.AddMonths(-1);Draw();});
        var next = SmallButton("›",()=>{month=month.AddMonths(1);Draw();});
        next.SetValue(Grid.ColumnProperty,2);
        nav.Children.Add(prev);nav.Children.Add(next);panel.Children.Add(nav);

        var weekday = new UniformGrid { Columns=7,Rows=1,Margin=new Thickness(1,0,1,1) };
        foreach(var x in new[]{"S","M","T","W","T","F","S"})
            weekday.Children.Add(new TextBlock { Text=x,Foreground=muted,
                FontSize=11,TextAlignment=TextAlignment.Center });
        panel.Children.Add(weekday);
        panel.Children.Add(days);

        var line = new Border { Background=new SolidColorBrush(Color.FromRgb(223,220,234)),
            Height=1,Margin=new Thickness(5,3,5,5) };
        panel.Children.Add(line);
        panel.Children.Add(agenda);
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        var today = SmallButton("Today",()=>{selected=DateTime.Today;month=new DateTime(selected.Year,selected.Month,1);Draw();});
        today.Margin=new Thickness(4,0,4,0);
        bottom.Children.Add(today);
        var full = SmallButton("Open planner ↗",expand);
        full.Margin=new Thickness(4,0,4,0);
        full.Background=accent;full.Foreground=Brushes.White;
        Grid.SetColumn(full,1);bottom.Children.Add(full);
        panel.Children.Add(bottom);

        void Draw()
        {
            caption.Text=month.ToString("MMMM yyyy");
            days.Children.Clear();
            int offset=(int)month.DayOfWeek;
            for(int i=0;i<42;i++)
            {
                int d=i-offset+1;
                if(d<1 || d>DateTime.DaysInMonth(month.Year,month.Month))
                {
                    days.Children.Add(new Border { Width=31,Height=23 });continue;
                }
                var date=new DateTime(month.Year,month.Month,d);
                bool chosen=date.Date==selected.Date;
                bool current=date.Date==DateTime.Today;
                var btn=new Button { Content=d.ToString(),Width=34,Height=30,
                    Padding=new Thickness(0),Margin=new Thickness(1,1,1,1),
                    BorderThickness=new Thickness(current && !chosen?1:0),
                    BorderBrush=accent,
                    Background=chosen?accent:Brushes.Transparent,
                    Foreground=chosen?Brushes.White:ink,
                    FontSize=11,FontWeight=chosen?FontWeights.SemiBold:FontWeights.Normal,
                    Cursor=System.Windows.Input.Cursors.Hand };
                btn.Click+=(_,_)=>{selected=date;Draw();};
                days.Children.Add(btn);
            }
            try
            {
                var ev=store.Load().Events
                    .Where(x=>OccursOn(x,selected))
                    .Select(x=>x.Title)
                    .Take(2).ToArray();
                agenda.Text=selected.ToString("ddd, d MMM")+"  ·  "+
                    (ev.Length==0?"No events":string.Join("  •  ",ev));
            }
            catch(Exception) { agenda.Text=selected.ToString("ddd, d MMM")+"  ·  Events unavailable"; }
        }
        Draw();
        return canvas;
    }

    private static bool OccursOn(EventItem e, DateTime date)
    {
        // Match the full planner's recurrence expansion, including the
        // AddMonths rollover behavior for dates such as January 31.
        DateTime day=date.Date, at=e.Date;
        for(int i=0;i<500 && at.Date<=day;i++)
        {
            if(at.Date==day)return true;
            if(e.Recurrence=="Daily")at=at.AddDays(1);
            else if(e.Recurrence=="Weekly")at=at.AddDays(7);
            else if(e.Recurrence=="Monthly")at=at.AddMonths(1);
            else if(e.Recurrence=="Yearly")at=at.AddYears(1);
            else return false;
        }
        return false;
    }
    private static Button SmallButton(string title,Action act)
    {
        var b=new Button { Content=title,Height=26,MinWidth=32,
            Padding=new Thickness(6,2,6,2),
            Background=new SolidColorBrush(Color.FromRgb(237,233,249)),
            BorderThickness=new Thickness(0),Foreground=new SolidColorBrush(Color.FromRgb(64,53,116)),
            Cursor=System.Windows.Input.Cursors.Hand,
            FontWeight=FontWeights.Medium };
        b.Click+=(_,_)=>act();
        return b;
    }
}
