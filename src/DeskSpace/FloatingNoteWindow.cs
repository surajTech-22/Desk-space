using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

public sealed class FloatingNoteWindow : Window
{
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly NoteItem note;
    private readonly Action save;
    public FloatingNoteWindow(NoteItem note, Action save)
    {
        this.note = note; this.save = save;
        Title = "Sticky Note — " + note.Title;
        Width = 340; Height = 330; Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(255, 249, 211));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(10) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
        var pin = new CheckBox { Content = "Always on top", IsChecked = true, Margin = new Thickness(0, 0, 18, 6) };
        pin.Checked += (_, _) => Topmost = true;
        pin.Unchecked += (_, _) => Topmost = false;
        toolbar.Children.Add(pin);
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        var title = new TextBox { Text = note.Title, FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 7) };
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var editor = new TextBox { Text = note.Content, FontSize = 15, TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(editor);
        void Changed()
        {
            note.Title = title.Text; note.Content = editor.Text; note.Updated = DateTime.Now;
            Title = "Sticky Note — " + note.Title;
            debounce.Stop(); debounce.Start();
        }
        title.TextChanged += (_, _) => Changed(); editor.TextChanged += (_, _) => Changed();
        debounce.Tick += (_, _) => { debounce.Stop(); this.save(); };
        Closing += (_, _) => { debounce.Stop(); this.save(); };
        Content = root;
    }
}
