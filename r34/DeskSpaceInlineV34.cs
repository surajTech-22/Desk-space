using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// Notes, Reminders and Clipboard transform the existing circle in place.
// They do not create top-level windows and keep their previous live module.
internal sealed class DeskSpaceInlineV34 : IDisposable
{
    private readonly string module;
    private readonly string previous;
    private readonly Action done;
    private readonly bool quickAddRequested;
    private Action? startNew;
    private readonly WorkspaceStore store=new();
    private readonly List<UIElement> subscriptionOwners=new();

    public DeskSpaceInlineV34(string module,string previous,Action done,bool openNew=false)
    {this.module=module;this.previous=previous;this.done=done;
     quickAddRequested=openNew;}

    private static readonly Brush TextInk=new SolidColorBrush(Color.FromRgb(55,48,89));
    private static Button Button(string text,Action run)
    {
        var b=new Button{Content=text,Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(2),FontSize=12,MinHeight=30,
            Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(205,196,229))};
        b.Click+=(_,e)=>{e.Handled=true;run();};return b;
    }
    private static TextBlock Text(string value,int font=13)=>new(){Text=value,FontSize=font,
        Foreground=TextInk,Margin=new Thickness(3,3,3,3),TextWrapping=TextWrapping.Wrap};
    private static TextBox Field(string tip,int height=35)=>new(){
        ToolTip=tip,Height=height,FontSize=14,Background=Brushes.White,
        Padding=new Thickness(8,5,8,5),Margin=new Thickness(2,3,2,6),
        BorderBrush=new SolidColorBrush(Color.FromRgb(203,191,226))};
    private void Edit(Action<Workspace> mutate)
    {var ws=store.Load();mutate(ws);store.Save(ws);}
    private static void Error(Exception exception)=>MessageBox.Show(exception.Message,
        "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);

    public FrameworkElement Create()
    {
        Brush paper = module=="Notes" ? new SolidColorBrush(Color.FromRgb(255,247,212))
            :module=="Reminders" ? new SolidColorBrush(Color.FromRgb(246,241,255))
            :new SolidColorBrush(Color.FromRgb(245,248,255));
        var outer=new Border{Background=paper,CornerRadius=new CornerRadius(17),
            BorderBrush=new SolidColorBrush(Color.FromRgb(211,201,229)),
            BorderThickness=new Thickness(1.2)};
        var dock=new DockPanel();
        var header=new Grid{Height=52,Margin=new Thickness(12,3,8,3)};
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(116)});
        header.Children.Add(new TextBlock{Text=module=="Notes"?"✎  Sticky note":
            module=="Reminders"?"◉  Reminders":"▣  Clipboard",
            FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=TextInk,
            VerticalAlignment=VerticalAlignment.Center});
        var back=Button("← "+previous,done);
        Grid.SetColumn(back,2);header.Children.Add(back);DockPanel.SetDock(header,Dock.Top);
        dock.Children.Add(header);
        var scroller=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var body=new StackPanel{Margin=new Thickness(12,2,12,12)};
        scroller.Content=body;dock.Children.Add(scroller);outer.Child=dock;
        if(module=="Notes")BuildNotes(body);
        else if(module=="Reminders")BuildReminders(body);
        else BuildClipboard(body);
        if(startNew!=null)
        {
            var add=Button("+",()=>startNew?.Invoke());
            add.FontSize=18;add.Width=32;add.Padding=new Thickness(0);
            add.ToolTip=module=="Notes"?"New note":
                module=="Reminders"?"New reminder":"Capture clipboard";
            Grid.SetColumn(add,1);
            header.Children.Add(add);
        }
        if(quickAddRequested)
            outer.Loaded+=(_,_)=>outer.Dispatcher.BeginInvoke(
                new Action(()=>startNew?.Invoke()),
                System.Windows.Threading.DispatcherPriority.Input);
        return outer;
    }
    private void BuildNotes(StackPanel body)
    {
        var notes=new ComboBox{Height=32,DisplayMemberPath="Title",Margin=new Thickness(2,2,2,5)};
        var title=Field("Note title");
        var memo=Field("Edit note",235);
        memo.AcceptsReturn=true;memo.TextWrapping=TextWrapping.Wrap;
        memo.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        body.Children.Add(notes);body.Children.Add(title);body.Children.Add(memo);
        var buttons=new WrapPanel();
        body.Children.Add(buttons);Guid? editing=null;
        void Reload(Guid? selected=null)
        {
            var records=store.Load().Notes.OrderByDescending(n=>n.Updated).ToList();
            notes.ItemsSource=records;
            notes.SelectedItem=records.FirstOrDefault(x=>x.Id==selected)??records.FirstOrDefault();
        }
        notes.SelectionChanged+=(_,_)=>{
            if(notes.SelectedItem is NoteItem n){editing=n.Id;title.Text=n.Title;memo.Text=n.Content;}
        };
        buttons.Children.Add(Button("Save",()=>{
            try{
                var id=editing??Guid.NewGuid();
                Edit(ws=>{
                    var n=ws.Notes.FirstOrDefault(x=>x.Id==id);
                    if(n==null){n=new NoteItem{Id=id};ws.Notes.Add(n);}
                    n.Title=string.IsNullOrWhiteSpace(title.Text)?"New note":title.Text.Trim();
                    n.Content=memo.Text;n.Updated=DateTime.Now;
                });
                editing=id;Reload(id);
            }catch(Exception e){Error(e);}
        }));
        buttons.Children.Add(Button("+ New",()=>{
            editing=null;notes.SelectedItem=null;title.Clear();memo.Clear();title.Focus();
        }));
        buttons.Children.Add(Button("Delete",()=>{
            if(editing is not Guid id)return;
            if(MessageBox.Show("Delete this note?","DeskSpace",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            try{Edit(ws=>ws.Notes.RemoveAll(n=>n.Id==id));editing=null;Reload();}catch(Exception e){Error(e);}
        }));
        startNew=()=>{
            editing=null;notes.SelectedItem=null;
            title.Clear();memo.Clear();title.Focus();
        };
        Reload();
    }
    private void BuildReminders(StackPanel body)
    {
        var items=new ListBox{MaxHeight=186,MinHeight=115,
            Margin=new Thickness(2,0,2,8)};
        body.Children.Add(items);
        var title=Field("Reminder title");
        var date=new DatePicker{SelectedDate=DateTime.Today,Margin=new Thickness(2,2,2,5)};
        var time=Field("Time, HH:mm");time.Text="09:00";
        body.Children.Add(Text("New reminder"));body.Children.Add(title);
        var row=new WrapPanel();
        row.Children.Add(date);time.Width=95;row.Children.Add(time);
        body.Children.Add(row);
        void Reload()=>items.ItemsSource=store.Load().Reminders
            .OrderBy(n=>n.Done).ThenBy(n=>n.Due).ToList();
        var actions=new WrapPanel();body.Children.Add(actions);
        actions.Children.Add(Button("+ Add",()=>{
            if(string.IsNullOrWhiteSpace(title.Text)||date.SelectedDate==null)return;
            if(!TimeSpan.TryParse(time.Text,out var tm)){MessageBox.Show("Use HH:mm time.");return;}
            try{Edit(ws=>ws.Reminders.Add(new ReminderItem{
                Title=title.Text.Trim(),Due=date.SelectedDate.Value.Date+tm
            }));title.Clear();Reload();}catch(Exception e){Error(e);}
        }));
        actions.Children.Add(Button("Complete",()=>{
            if(items.SelectedItem is not ReminderItem r)return;
            try{Edit(ws=>{var item=ws.Reminders.FirstOrDefault(x=>x.Id==r.Id);
                if(item!=null)item.Done=!item.Done;});Reload();}catch(Exception e){Error(e);}
        }));
        actions.Children.Add(Button("Delete",()=>{
            if(items.SelectedItem is not ReminderItem r)return;
            try{Edit(ws=>ws.Reminders.RemoveAll(x=>x.Id==r.Id));Reload();}catch(Exception e){Error(e);}
        }));
        startNew=()=>{
            items.SelectedItem=null;title.Clear();
            date.SelectedDate=DateTime.Today;
            time.Text="09:00";title.Focus();
        };
        Reload();
    }
    private void BuildClipboard(StackPanel body)
    {
        body.Children.Add(Text("Recent copied text · memory-only history",13));
        var history=new ListBox{MaxHeight=170,MinHeight=100,Margin=new Thickness(2)};
        var shelf=new ListBox{MaxHeight=125,MinHeight=70,Margin=new Thickness(2)};
        body.Children.Add(history);
        var actions=new WrapPanel();body.Children.Add(actions);
        body.Children.Add(Text("Temporary file shelf · originals never deleted",12));
        body.Children.Add(shelf);
        var fileActions=new WrapPanel();body.Children.Add(fileActions);
        void Reload()
        {
            history.ItemsSource=DeskSpaceClipboardV10.History.ToList();
            shelf.ItemsSource=DeskSpaceClipboardV10.Shelf.ToList();
        }
        actions.Children.Add(Button("Copy selected",()=>{
            if(history.SelectedItem is DeskSpaceClipboardV10.ClipboardEntry item)
            {Clipboard.SetText(item.Text);done();}
        }));
        actions.Children.Add(Button("Clear history",()=>{
            DeskSpaceClipboardV10.ClearHistory();Reload();
        }));
        fileActions.Children.Add(Button("Hold copied files",()=>{
            DeskSpaceClipboardV10.HoldCurrentFiles();Reload();
        }));
        fileActions.Children.Add(Button("Paste shelf",()=>{
            var files=new StringCollection();
            files.AddRange(DeskSpaceClipboardV10.Shelf.Where(File.Exists).ToArray());
            if(files.Count>0)Clipboard.SetFileDropList(files);
        }));
        fileActions.Children.Add(Button("Pasted • clear",()=>{
            DeskSpaceClipboardV10.ClearShelf();Reload();
        }));
        history.MouseDoubleClick+=(_,_)=>{
            if(history.SelectedItem is DeskSpaceClipboardV10.ClipboardEntry item)
            {Clipboard.SetText(item.Text);done();}
        };
        startNew=()=>{
            DeskSpaceClipboardV10.Poll();
            DeskSpaceClipboardV10.HoldCurrentFiles();
            Reload();
        };
        Reload();
    }
    public void Dispose(){subscriptionOwners.Clear();}
}