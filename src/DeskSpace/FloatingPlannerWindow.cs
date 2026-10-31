using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

public sealed class FloatingPlannerWindow : Window
{
    private readonly Workspace workspace;
    private readonly Action saved;
    private readonly DispatcherTimer? ticker;
    public FloatingPlannerWindow(string mode, Workspace workspace, Action saved)
    {
        this.workspace = workspace; this.saved = saved;
        Width = mode == "calendar" ? 385 : 355;
        Height = mode == "clock" ? 180 : 410;
        Title = "DeskSpace — " + mode;
        Background = new SolidColorBrush(Color.FromRgb(245, 249, 255));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;
        var root = new DockPanel { Margin = new Thickness(12) };
        var pin = new CheckBox { Content = "Always on top", IsChecked = true, Margin = new Thickness(0, 0, 0, 10) };
        pin.Checked += (_, _) => Topmost = true;
        pin.Unchecked += (_, _) => Topmost = false;
        DockPanel.SetDock(pin, Dock.Top); root.Children.Add(pin);
        if (mode == "clock")
        {
            var clock = new TextBlock { FontSize = 30, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            void UpdateClock() => clock.Text = DateTime.Now.ToString("ddd dd MMM yyyy\nHH:mm:ss");
            ticker.Tick += (_, _) => UpdateClock(); UpdateClock(); ticker.Start();
            Closed += (_, _) => ticker.Stop();
            root.Children.Add(clock);
        }
        else if (mode == "calendar")
        {
            var calendar = new Calendar { HorizontalAlignment = HorizontalAlignment.Center };
            DockPanel.SetDock(calendar, Dock.Top); root.Children.Add(calendar);
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 7) };
            var title = new TextBox { Width = 225, ToolTip = "New event title", Margin = new Thickness(0, 0, 5, 0) };
            var add = new Button { Content = "Add" };
            controls.Children.Add(title); controls.Children.Add(add);
            DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
            var events = new ListBox(); root.Children.Add(events);
            void Refresh() => events.ItemsSource = workspace.Events.Where(x => x.Date.Date == (calendar.SelectedDate ?? DateTime.Today).Date).ToList();
            calendar.SelectedDatesChanged += (_, _) => Refresh();
            add.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text)) return;
                workspace.Events.Add(new EventItem { Title = title.Text.Trim(), Date = (calendar.SelectedDate ?? DateTime.Today).Date });
                title.Text = ""; Refresh(); saved();
            };
            Refresh();
        }
        else if (mode == "reminders")
        {
            var title = new TextBox { Margin = new Thickness(0, 0, 0, 8), ToolTip = "New reminder" };
            DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var date = new DatePicker { SelectedDate = DateTime.Today, Width = 120 };
            var time = new TextBox { Text = "09:00", Width = 55, Margin = new Thickness(4, 0, 4, 0) };
            var add = new Button { Content = "Add", Margin = new Thickness(4, 0, 0, 0) };
            controls.Children.Add(date); controls.Children.Add(time); controls.Children.Add(add);
            DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
            var finish = new Button { Content = "Mark selected reminder done", Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(finish, Dock.Bottom); root.Children.Add(finish);
            var list = new ListBox(); root.Children.Add(list);
            void Refresh() => list.ItemsSource = workspace.Reminders.OrderBy(x => x.Due).ToList();
            add.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text) || !TimeSpan.TryParseExact(time.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var t) || t >= TimeSpan.FromDays(1))
                { MessageBox.Show("Enter a reminder and valid 24-hour time."); return; }
                workspace.Reminders.Add(new ReminderItem { Title = title.Text.Trim(), Due = (date.SelectedDate ?? DateTime.Today).Date + t });
                title.Text = ""; Refresh(); saved();
            };
            finish.Click += (_, _) =>
            {
                if (list.SelectedItem is ReminderItem item) { item.Done = !item.Done; Refresh(); saved(); }
            };
            Refresh();
        }
        Content = root;
    }
}
