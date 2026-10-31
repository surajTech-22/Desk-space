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
internal sealed class DeskSpaceInlineV36 : IDisposable
{
    private readonly string module;
    private readonly string previous;
    private readonly Action done;
    private readonly bool quickAddRequested;
    private Action? startNew;
    private readonly WorkspaceStore store=new();
    private readonly List<UIElement> subscriptionOwners=new();

    public DeskSpaceInlineV36(string module,string previous,Action done,bool openNew=false)
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
        var back=Button("← Circle",done);
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

        var initialSound=DeskSpaceReminderPreferencesV81.Load();
        var tune=new ComboBox{Width=151,Height=29,Margin=new Thickness(2),
            ItemsSource=new[]{"Exclamation","Beep","Asterisk","Hand",
                "None","Custom WAV"},SelectedItem=initialSound.Name};
        var wav=Field("Select custom WAV sound",31);
        wav.Width=279;
        wav.Text=initialSound.File;
        var soundStatus=Text("",11);
        var suppressSoundSave=false;
        void StoreSoundDefaults()
        {
            if(suppressSoundSave)return;
            var current=tune.SelectedItem?.ToString()??"Exclamation";
            if(!DeskSpaceReminderPreferencesV81.Save(current,wav.Text.Trim(),
                out var failure))
                soundStatus.Text="Could not save sound preference: "+failure;
            else soundStatus.Text="";
        }
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
        body.Children.Add(soundStatus);
        void UpdateWav()=>wav.Visibility=
            tune.SelectedItem?.ToString()=="Custom WAV"?
                Visibility.Visible:Visibility.Collapsed;
        tune.SelectionChanged+=(_,_)=>{UpdateWav();StoreSoundDefaults();};
        wav.TextChanged+=(_,_)=>StoreSoundDefaults();
        UpdateWav();

        var action=new ComboBox{Width=155,Height=29,
            Margin=new Thickness(2),ItemsSource=new[]{
                "None","Open application","Open file","Open URL"
            },SelectedItem="None"};
        // App/file targets are stored internally, but their visible label
        // comes from the real selected app/file name, never the path.
        string selectedPath="";
        string selectedName="";
        var target=Field("Paste website URL",31);
        target.Width=279;
        target.Visibility=Visibility.Collapsed;
        var selectedText=Text("",12);
        selectedText.TextWrapping=TextWrapping.Wrap;
        var selectionRow=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(2,2,2,2)};
        var selectedIcon=new Image{Width=23,Height=23,
            Stretch=Stretch.Uniform,
            Margin=new Thickness(1,0,6,0)};
        selectionRow.Children.Add(selectedIcon);
        selectionRow.Children.Add(selectedText);
        var searchHost=new StackPanel{Margin=new Thickness(2,3,2,2)};
        var browseFile=Button("Browse file",()=>{
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Filter="All files (*.*)|*.*",CheckFileExists=true};
            if(dialog.ShowDialog()==true)
            {
                selectedPath=dialog.FileName;
                selectedName=System.IO.Path.GetFileName(selectedPath);
                selectedIcon.Source=DeskSpaceApplicationIconsV65.Get(selectedPath);
                selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                    "Open file",selectedPath,selectedName);
            }
        });
        var browseApp=Button("Browse app",()=>{
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Filter="Applications (*.exe;*.lnk)|*.exe;*.lnk",
                CheckFileExists=true};
            if(dialog.ShowDialog()==true)
            {
                selectedPath=dialog.FileName;
                selectedName=System.IO.Path.GetFileNameWithoutExtension(
                    selectedPath);
                selectedIcon.Source=DeskSpaceApplicationIconsV65.Get(selectedPath);
                selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                    "Open application",selectedPath,selectedName);
            }
        });
        var controls=new WrapPanel();
        controls.Children.Add(action);
        controls.Children.Add(Button("Perform action",()=>{
            action.Focus();action.IsDropDownOpen=true;
        }));
        controls.Children.Add(browseFile);
        controls.Children.Add(browseApp);
        body.Children.Add(Text("Perform action when due (optional)",12));
        body.Children.Add(controls);
        body.Children.Add(searchHost);
        body.Children.Add(target);
        body.Children.Add(selectionRow);
        var urlNote=Text("Paste a URL. DeskSpace uses the default browser "+
            "and prepares an existing browser about 12 seconds early.",11);
        urlNote.TextWrapping=TextWrapping.Wrap;
        body.Children.Add(urlNote);
        void UpdateUrlLabel()
        {
            if(action.SelectedItem?.ToString()!="Open URL")return;
            selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                "Open URL",target.Text);
        }
        target.TextChanged+=(_,_)=>UpdateUrlLabel();
        void UpdateAction()
        {
            string mode=action.SelectedItem?.ToString()??"None";
            title.Visibility=DeskSpaceReminderPresentationV81.NeedsTitle(mode)
                ?Visibility.Visible:Visibility.Collapsed;
            selectedPath="";selectedName="";
            selectedIcon.Source=null;selectedText.Text="";
            selectedText.Visibility=mode=="None"?
                Visibility.Collapsed:Visibility.Visible;
            selectionRow.Visibility=mode=="None"?
                Visibility.Collapsed:Visibility.Visible;
            browseFile.Visibility=mode=="Open file"?
                Visibility.Visible:Visibility.Collapsed;
            browseApp.Visibility=mode=="Open application"?
                Visibility.Visible:Visibility.Collapsed;
            target.Visibility=mode=="Open URL"?
                Visibility.Visible:Visibility.Collapsed;
            urlNote.Visibility=mode=="Open URL"?
                Visibility.Visible:Visibility.Collapsed;
            searchHost.Children.Clear();
            searchHost.Visibility=mode is ("Open application" or "Open file")?
                Visibility.Visible:Visibility.Collapsed;
            if(mode is "Open application" or "Open file")
            {
                searchHost.Children.Add(DeskSpaceReminderPickerV81.Create(
                    mode,(path,name)=>{
                        selectedPath=path;selectedName=name;
                        selectedIcon.Source=DeskSpaceApplicationIconsV65.Get(path);
                        selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                            mode,path,name);
                    }));
            }
            if(mode=="Open URL")UpdateUrlLabel();
        }
        action.SelectionChanged+=(_,_)=>UpdateAction();
        UpdateAction();

        var automation=new DeskSpaceAutomationEditorV84(
            ()=>{
                string mode=action.SelectedItem?.ToString()??"None";
                return (mode,mode=="Open URL"?target.Text.Trim():selectedPath);
            },
            (mode,path)=>{
                action.SelectedItem=mode;
                if(mode=="Open URL")target.Text=path;
                else
                {
                    selectedPath=path;
                    selectedName=DeskSpaceReminderPresentationV81.TargetName(mode,path);
                    selectedIcon.Source=DeskSpaceApplicationIconsV65.Get(path);
                    selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                        mode,path,selectedName);
                }
            });
        body.Children.Add(automation.Create());

        var recurrence=new ComboBox{Width=157,Height=28,
            Margin=new Thickness(2),ItemsSource=DeskSpaceReminderScheduleV83.RecurrenceOptions,
            SelectedItem="None"};
        var repeatDays=Field("Mon,Wed,Fri",30);repeatDays.Width=152;
        var repeatEvery=Field("Number of days",30);
        repeatEvery.Width=97;repeatEvery.Text="1";
        var repeatRow=new WrapPanel();
        repeatRow.Children.Add(recurrence);
        repeatRow.Children.Add(repeatDays);
        repeatRow.Children.Add(repeatEvery);
        body.Children.Add(Text("Repeat / custom schedule",12));
        body.Children.Add(repeatRow);
        void ShowRepeatOptions()
        {
            var rule=recurrence.SelectedItem?.ToString()??"None";
            repeatDays.Visibility=rule=="Selected days"?
                Visibility.Visible:Visibility.Collapsed;
            repeatEvery.Visibility=rule=="Every N days"?
                Visibility.Visible:Visibility.Collapsed;
        }
        recurrence.SelectionChanged+=(_,_)=>ShowRepeatOptions();
        ShowRepeatOptions();
        int IntervalDays()=>int.TryParse(repeatEvery.Text,out var number)?
            Math.Clamp(number,1,365):1;

        void Reload()
        {
            var active=store.Load().Reminders.Where(x=>!x.Done)
                .OrderBy(x=>x.Due).ToList();
            list.ItemsSource=active;
            list.Visibility=active.Count==0?Visibility.Collapsed:Visibility.Visible;
        }
        var row=new WrapPanel();body.Children.Add(row);
        row.Children.Add(Button("+ Add",()=>{
            string selectedAction=action.SelectedItem?.ToString()??"None";
            if((DeskSpaceReminderPresentationV81.NeedsTitle(selectedAction) &&
                string.IsNullOrWhiteSpace(title.Text))||due.SelectedDate==null)
                return;
            if(!DeskSpaceReminderTimeV78.TryParse(time.Text,
                out var parsed,out var normalized)){
                MessageBox.Show("Invalid time. Try 72345, 122345, "+
                    "7:23:45 PM, or 23:59:59.");return;
            }
            time.Text=normalized;
            var actionName=selectedAction;
            var actionPath=actionName=="Open URL"?
                target.Text.Trim():selectedPath;
            if(actionName=="Open URL" &&
               DeskSpaceReminderBrowserV80.TryUrl(actionPath,out var normalizedUrl))
                actionPath=normalizedUrl;
            if(!DeskSpaceDueShelfV74.IsAllowedAction(actionName,actionPath)){
                MessageBox.Show("Choose an existing application/file or a valid http(s) URL.");
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
                    Title=DeskSpaceReminderPresentationV81.ResolveTitle(
                        title.Text,actionName,actionPath,selectedName),
                    Due=due.SelectedDate.Value.Date+parsed,
                    AlertSound=tune.SelectedItem?.ToString()??"Exclamation",
                    AlertSoundFile=wav.Text.Trim(),AlertAction=actionName,
                    AlertTarget=actionPath,
                    RepeatPattern=recurrence.SelectedItem?.ToString()??"None",
                    RepeatDays=repeatDays.Text.Trim(),
                    RepeatEveryDays=IntervalDays(),
                    AutomationSteps=automation.Steps,
                    TriggerCondition=automation.ConditionMode,
                    TriggerTarget=automation.ConditionTarget
                }));
                title.Clear();Reload();
            }catch(Exception e){Error(e);}
        }));
        row.Children.Add(Button("Save changes",()=>{
            if(list.SelectedItem is not ReminderItem selected)return;
            string mode=action.SelectedItem?.ToString()??"None";
            string path=mode=="Open URL"?target.Text.Trim():selectedPath;
            if(mode=="Open URL" &&
               DeskSpaceReminderBrowserV80.TryUrl(path,out var cleanUrl))
                path=cleanUrl;
            if(!DeskSpaceDueShelfV74.IsAllowedAction(mode,path))return;
            if(!DeskSpaceReminderTimeV78.TryParse(time.Text,out var parsed,out _)
                ||due.SelectedDate==null)return;
            try
            {
                Edit(ws=>{
                    var entry=ws.Reminders.FirstOrDefault(x=>x.Id==selected.Id);
                    if(entry==null)return;
                    entry.Title=DeskSpaceReminderPresentationV81.ResolveTitle(
                        title.Text,mode,path,selectedName);
                    var editedDue=due.SelectedDate.Value.Date+parsed;
                    bool changedSchedule=entry.Due!=editedDue ||
                        entry.RepeatPattern!=(recurrence.SelectedItem?.ToString()??"None");
                    entry.Due=editedDue;
                    entry.AlertAction=mode;entry.AlertTarget=path;
                    entry.AlertSound=tune.SelectedItem?.ToString()??"Exclamation";
                    entry.AlertSoundFile=wav.Text.Trim();
                    entry.RepeatPattern=recurrence.SelectedItem?.ToString()??"None";
                    entry.RepeatDays=repeatDays.Text.Trim();
                    entry.RepeatEveryDays=IntervalDays();
                    entry.AutomationSteps=automation.Steps;
                    entry.TriggerCondition=automation.ConditionMode;
                    entry.TriggerTarget=automation.ConditionTarget;
                    if(changedSchedule)
                    {
                        entry.SnoozedUntil=null;entry.SnoozeOnly=false;
                        entry.LastAlert=null;
                    }
                });
                Reload();
            }catch(Exception e){Error(e);}
        }));
        row.Children.Add(Button("History",()=>{
            var entries=DeskSpaceReminderActionHistoryV83.ReadRecent(12);
            MessageBox.Show(entries.Count==0?"No actions run yet":
                string.Join(Environment.NewLine,
                    entries.Select(x=>x.At.ToString("g")+"  "+x.Title+
                        "  "+x.Result)),
                "DeskSpace reminder action history");
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
        list.SelectionChanged+=(_,_)=>{
            if(list.SelectedItem is not ReminderItem entry)return;
            title.Text=entry.Title;
            automation.Load(entry);
            recurrence.SelectedItem=DeskSpaceReminderScheduleV83.RecurrenceOptions
                .Contains(entry.RepeatPattern)?entry.RepeatPattern:"None";
            repeatDays.Text=entry.RepeatDays??"";
            repeatEvery.Text=entry.RepeatEveryDays.ToString();
            due.SelectedDate=entry.Due.Date;
            time.Text=entry.Due.ToString("HH:mm:ss");
            suppressSoundSave=true;
            tune.SelectedItem=entry.AlertSound is "Exclamation" or "Beep"
                or "Asterisk" or "Hand" or "None" or "Custom WAV"?
                entry.AlertSound:"Exclamation";
            wav.Text=entry.AlertSoundFile??"";
            suppressSoundSave=false;
            UpdateWav();
            action.SelectedItem=string.IsNullOrWhiteSpace(entry.AlertAction)?
                "None":entry.AlertAction;
            if(entry.AlertAction=="Open URL")
                target.Text=entry.AlertTarget;
            else if(entry.AlertAction is "Open application" or "Open file")
            {
                selectedPath=entry.AlertTarget;
                selectedName=DeskSpaceReminderPresentationV81.TargetName(
                    entry.AlertAction,entry.AlertTarget);
                selectedIcon.Source=DeskSpaceApplicationIconsV65.Get(selectedPath);
                selectedText.Text=DeskSpaceReminderPresentationV81.ActionLabel(
                    entry.AlertAction,selectedPath,selectedName);
            }
        };
        // Quick-add from the donut's + button must open this SAME editor.
        startNew=()=>{
            list.SelectedItem=null;
            title.Clear();
            action.SelectedItem="None";
            recurrence.SelectedItem="None";
            repeatDays.Clear();repeatEvery.Text="1";
            automation.Reset();
            due.SelectedDate=DateTime.Today;
            time.Text="09:00:00";
            title.Focus();
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
        actions.Children.Add(Button("Quick actions",()=>{
            string clip=history.SelectedItem is DeskSpaceClipboardV10.ClipboardEntry entry?
                entry.Text:(Clipboard.ContainsText()?Clipboard.GetText():"");
            if(!string.IsNullOrWhiteSpace(clip))
                DeskSpaceClipboardQuickActionsV85.Open(clip);
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