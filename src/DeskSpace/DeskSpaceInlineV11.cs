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
internal sealed class DeskSpaceInlineV11 : IDisposable
{
    private readonly string module;
    private readonly string previous;
    private readonly Action done;
    private readonly WorkspaceStore store=new();
    private readonly List<UIElement> subscriptionOwners=new();

    public DeskSpaceInlineV11(string module,string previous,Action done)
    {this.module=module;this.previous=previous;this.done=done;}

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
        var header=new Grid{Height=44,Margin=new Thickness(8,2,6,2)};
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(116)});
        header.Children.Add(new TextBlock{Text=module=="Notes"?"✎  Sticky note":
            module=="Reminders"?"◉  Reminders":"▣  Clipboard",
            FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=TextInk,
            VerticalAlignment=VerticalAlignment.Center});
        var back=Button("← "+previous,done);
        Grid.SetColumn(back,1);header.Children.Add(back);DockPanel.SetDock(header,Dock.Top);
        dock.Children.Add(header);
        var scroller=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var body=new StackPanel{Margin=new Thickness(9,1,9,8)};
        scroller.Content=body;dock.Children.Add(scroller);outer.Child=dock;
        if(module=="Notes")BuildNotes(body);
        else if(module=="Reminders")BuildReminders(body);
        else BuildClipboard(body);
        return outer;
    }
    private void BuildNotes(StackPanel body)
    {
        var notes=new ComboBox{Height=32,DisplayMemberPath="Title",Margin=new Thickness(2,2,2,5)};
        var title=Field("Note title");
        var memo=Field("Edit note",(int)Math.Clamp(SystemParameters.WorkArea.Height*.16,90,150));
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
        Reload();
    }
    private void BuildReminders(StackPanel body)
    {
        body.Children.Add(Text("REMINDERS · SOUND & ACTIONS",12));
        var list=new ListBox{MinHeight=55,MaxHeight=92,
            Margin=new Thickness(2,0,2,4)};
        body.Children.Add(list);
        var title=Field("Reminder or task title");
        body.Children.Add(title);
        var due=new DatePicker{SelectedDate=DateTime.Today,
            Width=152,Margin=new Thickness(2,2,2,3)};
        var time=Field("Enter 72345, 122345 or 72345 PM",32);
        time.Text="09:00:00";time.Width=148;
        var clockRow=new WrapPanel();
        clockRow.Children.Add(due);clockRow.Children.Add(time);
        body.Children.Add(Text("Date and time · seconds supported",12));
        body.Children.Add(clockRow);
        var preview=Text("Time: 09:00:00",11);
        body.Children.Add(preview);
        void ShowParsedTime()
        {
            preview.Text=DeskSpaceReminderTimeV78.TryParse(time.Text,
                out _,out var normal)
                ?"Time: "+normal
                :"Enter HMMSS or HHMMSS, optional AM/PM";
        }
        time.TextChanged+=(_,_)=>ShowParsedTime();
        time.LostKeyboardFocus+=(_,_)=>{
            if(DeskSpaceReminderTimeV78.TryParse(time.Text,
                out _,out var normalized))
                time.Text=normalized;
        };

        var tune=new ComboBox{Width=151,Height=29,Margin=new Thickness(2),
            ItemsSource=new[]{"Exclamation","Beep","Asterisk","Hand",
                "None","Custom WAV"},SelectedItem="Exclamation"};
        var wav=Field("Select custom WAV sound",31);
        wav.Width=279;
        var sounds=new WrapPanel();
        sounds.Children.Add(tune);
        sounds.Children.Add(Button("Test sound",()=>{
            DeskSpaceDueShelfV74.PlaySound(new ReminderItem{
                AlertSound=tune.SelectedItem?.ToString()??"Exclamation",
                AlertSoundFile=wav.Text.Trim()
            });
        }));
        sounds.Children.Add(Button("Browse WAV",()=>{
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Filter="WAV sound (*.wav)|*.wav",CheckFileExists=true
            };
            if(dialog.ShowDialog()==true){
                wav.Text=dialog.FileName;tune.SelectedItem="Custom WAV";
            }
        }));
        body.Children.Add(Text("Sound / Tune",12));
        body.Children.Add(sounds);body.Children.Add(wav);
        void UpdateWav()=>wav.Visibility=
            tune.SelectedItem?.ToString()=="Custom WAV"?
                Visibility.Visible:Visibility.Collapsed;
        tune.SelectionChanged+=(_,_)=>UpdateWav();
        UpdateWav();

        var action=new ComboBox{Width=155,Height=29,
            Margin=new Thickness(2),ItemsSource=new[]{
                "None","Open application","Open file"
            },SelectedItem="None"};
        var target=Field("Choose executable or file",31);
        target.Width=279;
        var actions=new WrapPanel();
        actions.Children.Add(action);
        actions.Children.Add(Button("Perform action",()=>{
            action.Focus();action.IsDropDownOpen=true;
        }));
        actions.Children.Add(Button("Browse target",()=>{
            var mode=action.SelectedItem?.ToString()??"None";
            if(mode=="None"){action.IsDropDownOpen=true;return;}
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Filter=mode=="Open application"
                    ?"Applications (*.exe)|*.exe"
                    :"All files (*.*)|*.*",CheckFileExists=true
            };
            if(dialog.ShowDialog()==true)target.Text=dialog.FileName;
        }));
        body.Children.Add(Text("Perform action when due (optional)",12));
        body.Children.Add(actions);body.Children.Add(target);
        void UpdateAction()=>target.Visibility=
            action.SelectedItem?.ToString()=="None"?
                Visibility.Collapsed:Visibility.Visible;
        action.SelectionChanged+=(_,_)=>UpdateAction();
        UpdateAction();

        void Reload()=>list.ItemsSource=store.Load().Reminders
            .Where(x=>!x.Done).OrderBy(x=>x.Due).ToList();
        var row=new WrapPanel();body.Children.Add(row);
        row.Children.Add(Button("+ Add",()=>{
            if(string.IsNullOrWhiteSpace(title.Text)||due.SelectedDate==null)
                return;
            if(!DeskSpaceReminderTimeV78.TryParse(time.Text,
                out var parsed,out var normalized)){
                MessageBox.Show("Invalid time. Try 72345, 122345, "+
                    "7:23:45 PM, or 23:59:59.");return;
            }
            time.Text=normalized;
            var actionName=action.SelectedItem?.ToString()??"None";
            var actionPath=target.Text.Trim();
            if(!DeskSpaceDueShelfV74.IsAllowedAction(actionName,actionPath)){
                MessageBox.Show("Choose an existing .exe or a valid file.");
                return;
            }
            if(tune.SelectedItem?.ToString()=="Custom WAV" &&
               (!System.IO.File.Exists(wav.Text.Trim()) ||
                !System.IO.Path.GetExtension(wav.Text.Trim())
                    .Equals(".wav",StringComparison.OrdinalIgnoreCase))){
                MessageBox.Show("Choose an existing WAV file.");return;
            }
            try{
                Edit(ws=>ws.Reminders.Add(new ReminderItem{
                    Title=title.Text.Trim(),Due=due.SelectedDate.Value.Date+parsed,
                    AlertSound=tune.SelectedItem?.ToString()??"Exclamation",
                    AlertSoundFile=wav.Text.Trim(),AlertAction=actionName,
                    AlertTarget=actionPath
                }));
                title.Clear();Reload();
            }catch(Exception e){Error(e);}
        }));
        row.Children.Add(Button("Complete",()=>{
            if(list.SelectedItem is not ReminderItem selected)return;
            try{
                Edit(ws=>{
                    var r=ws.Reminders.FirstOrDefault(x=>x.Id==selected.Id);
                    if(r!=null)r.Done=true;
                });
                Reload();
            }catch(Exception e){Error(e);}
        }));
        row.Children.Add(Button("Delete",()=>{
            if(list.SelectedItem is not ReminderItem selected)return;
            try{
                Edit(ws=>ws.Reminders.RemoveAll(x=>x.Id==selected.Id));
                Reload();
            }catch(Exception e){Error(e);}
        }));
        Reload();
    }

    private void BuildClipboard(StackPanel body)
    {
        body.Children.Add(Text("Recent copied text · memory-only history",13));
        var history=new ListBox{MaxHeight=115,MinHeight=66,Margin=new Thickness(2)};
        var shelf=new ListBox{MaxHeight=88,MinHeight=56,Margin=new Thickness(2)};
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
        Reload();
    }
    public void Dispose(){subscriptionOwners.Clear();}
}