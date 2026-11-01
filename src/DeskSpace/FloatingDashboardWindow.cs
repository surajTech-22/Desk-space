using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Desktop widget is not owned by the main window, so minimizing DeskSpace never hides it.
public sealed class FloatingDashboardWindow : Window
{
    private readonly Workspace data;
    private readonly Action save;
    private readonly Action addNote;
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly ComboBox picker = new() { MinWidth = 140 };
    private readonly TextBox heading = new() { FontSize = 16, FontWeight = FontWeights.Bold };
    private readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private NoteItem? current;
    private bool loading;

    public FloatingDashboardWindow(Workspace data, Action save, Action addNote, Action openMain,
        Action calendar, Action reminders, Action clock, Action updates)
    {
        this.data = data; this.save = save; this.addNote = addNote;
        Title = "DeskSpace - Floating Desktop Widget";
        Width = 350; Height = 440; MinWidth = 300; MinHeight = 300;
        ShowInTaskbar = false; Topmost = true; WindowStyle = WindowStyle.ToolWindow;
        Background = Brushes.White; WindowStartupLocation = WindowStartupLocation.Manual;
        var prefs = WidgetPreferences.Load();
        var area = SystemParameters.WorkArea;
        Left = prefs.Left is double x && x >= area.Left && x < area.Right - 80 ? x : Math.Max(area.Left, area.Right - Width - 25);
        Top = prefs.Top is double y && y >= area.Top && y < area.Bottom - 60 ? y : area.Top + 40;

        var layout = new DockPanel { Margin = new Thickness(12) };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "DeskSpace", FontSize = 20, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        AddButton(top, "Open app", openMain);
        DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top);

        var shortcuts = new WrapPanel { Margin = new Thickness(0, 0, 0, 9) };
        AddButton(shortcuts, "Calendar", calendar);
        AddButton(shortcuts, "Reminders", reminders);
        AddButton(shortcuts, "Clock", clock);
        AddButton(shortcuts, "Updates", updates);
        DockPanel.SetDock(shortcuts, Dock.Top); layout.Children.Add(shortcuts);

        var settings = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var pin = new CheckBox { Content = "Always on top", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        pin.Checked += (_, _) => Topmost = true; pin.Unchecked += (_, _) => Topmost = false;
        var launch = new CheckBox { Content = "Open at startup", IsChecked = prefs.ShowAtLaunch };
        launch.Checked += (_, _) => WidgetPreferences.SetLaunch(true);
        launch.Unchecked += (_, _) => WidgetPreferences.SetLaunch(false);
        settings.Children.Add(pin); settings.Children.Add(launch);
        DockPanel.SetDock(settings, Dock.Top); layout.Children.Add(settings);

        var selection = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var add = new Button { Content = "+ New note", Margin = new Thickness(6, 0, 0, 0) };
        add.Click += (_, _) => { addNote(); ReloadNotes(data.Notes.LastOrDefault()); };
        DockPanel.SetDock(add, Dock.Right); selection.Children.Add(add); selection.Children.Add(picker);
        picker.SelectionChanged += (_, _) => SelectNote(picker.SelectedItem as NoteItem);
        DockPanel.SetDock(selection, Dock.Top); layout.Children.Add(selection);
        heading.Margin = new Thickness(0, 0, 0, 7);
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        layout.Children.Add(editor);

        heading.TextChanged += (_, _) => Edited();
        editor.TextChanged += (_, _) => Edited();
        debounce.Tick += (_, _) => { debounce.Stop(); save(); };
        Closing += (_, _) => { debounce.Stop(); save(); WidgetPreferences.SavePosition(Left, Top); };
        Content = layout;
        ReloadNotes(data.Notes.FirstOrDefault());
    }

    private static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(6, 3, 6, 3) };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    public void ReloadNotes(NoteItem? preferred = null)
    {
        loading = true;
        var selected = preferred ?? current;
        picker.ItemsSource = null; picker.ItemsSource = data.Notes;
        picker.SelectedItem = selected != null && data.Notes.Contains(selected) ? selected : data.Notes.FirstOrDefault();
        loading = false; SelectNote(picker.SelectedItem as NoteItem);
    }
    private void SelectNote(NoteItem? note)
    {
        if (loading) return;
        current = note; loading = true;
        heading.Text = note?.Title ?? ""; editor.Text = note?.Content ?? "";
        heading.IsEnabled = editor.IsEnabled = note != null; loading = false;
    }
    private void Edited()
    {
        if (loading || current == null) return;
        current.Title = heading.Text; current.Content = editor.Text; current.Updated = DateTime.Now;
        debounce.Stop(); debounce.Start();
    }
}

// Widget placement/preferences are saved separately from workspace.json to preserve R1 data.
public sealed class WidgetPreferences
{
    public bool ShowAtLaunch { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    private static string PathName => Path.Combine(WorkspaceStore.Root, "widget-preferences.json");
    public static WidgetPreferences Load()
    {
        try { return File.Exists(PathName) ? JsonSerializer.Deserialize<WidgetPreferences>(File.ReadAllText(PathName)) ?? new() : new(); }
        catch { return new(); }
    }
    private static void Save(WidgetPreferences p)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(p));
        File.Move(temp, PathName, true);
    }
    public static void SetLaunch(bool show) { var p = Load(); p.ShowAtLaunch = show; Save(p); }
    public static void SavePosition(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        var p = Load(); p.Left = x; p.Top = y; Save(p);
    }
}
