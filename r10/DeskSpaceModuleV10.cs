using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DeskSpace;

// A module is a VIEW INSIDE WidgetShellWindow. No independent Notes,
// Reminders, Calendar, Search, Calculator or Clipboard popup is created.
public sealed class DeskSpaceModuleV10 : IDisposable
{
    private readonly WorkspaceStore store = new();
    private readonly WidgetSettingsV3 prefs;
    private readonly Action done;
    private readonly string previous;
    private CancellationTokenSource? queryCancel;
    private DispatcherTimer? debounce;
    private DeskSpaceVaultV10? vault;
    private DispatcherTimer? vaultTimeout;
    private bool disposed;
    public DeskSpaceModuleV10(WidgetSettingsV3 settings, string previousMode, Action finish)
    { prefs=settings;previous=previousMode;done=finish; }
    private static readonly SolidColorBrush Ink = new(Color.FromRgb(34,40,65));
    private static readonly SolidColorBrush Purple = new(Color.FromRgb(105,86,199));
    private static TextBlock Label(string s, int size=14) => new() { Text=s,Foreground=Ink,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,5) };
    private static Button Btn(string s,Action a)
    {
        var b=new Button { Content=s,Margin=new Thickness(0,4,7,4),Padding=new Thickness(12,7,12,7),MinHeight=34 };
        b.Click+=(_,_)=>a();return b;
    }
    private static void Fail(Exception e) => MessageBox.Show(e.Message,"DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
    private Workspace Load() => store.Load();
    private void Mutate(Action<Workspace> action)
    {
        var ws=Load();action(ws);store.Save(ws);
    }
    private static StackPanel Stack()=>new(){Margin=new Thickness(18)};
    private static WrapPanel Row()=>new(){Margin=new Thickness(0,4,0,6)};
    private static TextBox TextBox(string hint="",int height=36)=>new(){MinHeight=height,FontSize=15,Padding=new Thickness(9,6,9,6),ToolTip=hint,Margin=new Thickness(0,4,0,7)};
    public FrameworkElement Build(string mode)
    {
        var root = new DockPanel { Background=new SolidColorBrush(Color.FromRgb(248,247,253)) };
        var head=new Border { Background=new SolidColorBrush(Color.FromRgb(90,71,173)),Padding=new Thickness(17,12,17,12) };
        var top=new DockPanel();
        var back=Btn("← "+previous,done);back.Background=Brushes.White;back.MinWidth=86;
        DockPanel.SetDock(back,Dock.Right);top.Children.Add(back);
        var title=new TextBlock { Text="DeskSpace   /   "+mode,FontSize=19,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis };
        top.Children.Add(title);head.Child=top;DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
        var scroller=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var body=Stack();scroller.Content=body;root.Children.Add(scroller);
        switch(mode)
        {
            case "Search": BuildSearch(body);break;
            case "Notes": BuildNotes(body);break;
            case "Reminders": BuildReminders(body);break;
            case "Calendar": BuildCalendar(body);break;
            case "Clipboard": BuildClipboard(body);break;
            case "Password": BuildPassword(body);break;
            case "Calculator": BuildCalculator(body);break;
            case "Settings": body.Children.Add(Label("Widget preferences and quick tools",20));body.Children.Add(Btn("Open widget settings",()=>new WidgetOptionsWindow(prefs,()=>prefs.Save()).Show()));break;
            default: body.Children.Add(Label("Select a module from the donut"));break;
        }
        return root;
    }

    private void BuildSearch(StackPanel body)
    {
        body.Children.Add(Label("Search files · apps · folders · Windows settings · notes",15));
        var query=TextBox("Search this computer");
        var results=new ListBox { MinHeight=245,MaxHeight=350 };
        var status=Label("Start typing to search across accessible Windows folders.",12);
        var buttons=Row();var found=new List<SearchHit>();
        body.Children.Add(query);body.Children.Add(status);body.Children.Add(results);
        body.Children.Add(buttons);
        void Choose(Action<SearchHit> action)
        {
            if(results.SelectedItem is not SearchHit hit)return;
            try{action(hit);}catch(Exception e){Fail(e);}
        }
        void Open(SearchHit h)
        {
            if(h.Category=="Notes") Clipboard.SetText(h.NoteText??"");
            else if(!string.IsNullOrWhiteSpace(h.OpenTarget))
                Process.Start(new ProcessStartInfo(h.OpenTarget){UseShellExecute=true});
            done();
        }
        buttons.Children.Add(Btn("Open / launch",()=>Choose(Open)));
        buttons.Children.Add(Btn("Copy path",()=>Choose(h=>{Clipboard.SetText(h.OpenTarget);done();})));
        buttons.Children.Add(Btn("Copy file",()=>Choose(h=>{if(!File.Exists(h.OpenTarget))return;Clipboard.SetFileDropList(new StringCollection{h.OpenTarget});done();})));
        buttons.Children.Add(Btn("Pin app",()=>Choose(h=>{if(h.Category=="Applications"&&!prefs.PinnedApps.Contains(h.OpenTarget)){prefs.PinnedApps.Add(h.OpenTarget);prefs.Save();status.Text="Application pinned";}})));
        buttons.Children.Add(Btn("Open folder",()=>Choose(h=>{if(File.Exists(h.OpenTarget))Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{h.OpenTarget}\""){UseShellExecute=true});done();})));
        buttons.Children.Add(Btn("Windows Calculator",()=>{Process.Start(new ProcessStartInfo("calc.exe"){UseShellExecute=true});done();}));
        results.MouseDoubleClick+=(_,_)=>Choose(Open);
        query.KeyDown+=(_,e)=>{if(e.Key==Key.Escape){done();e.Handled=true;}else if(e.Key==Key.Enter){Choose(Open);e.Handled=true;}};
        debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(330)};
        debounce.Tick+=async (_,_)=>{
            debounce.Stop();queryCancel?.Cancel();queryCancel?.Dispose();queryCancel=new CancellationTokenSource();
            var token=queryCancel.Token;var term=query.Text.Trim();
            if(term.Length<2){results.ItemsSource=null;status.Text="Enter at least two characters";return;}
            status.Text="Searching…";
            try {
                found=await Task.Run(()=>SearchNow(term,Load(),token),token);
                if(token.IsCancellationRequested||disposed)return;
                results.ItemsSource=found;status.Text=found.Count+" matches (bounded filesystem scan)";
            }catch(OperationCanceledException){}catch(Exception ex){if(!token.IsCancellationRequested)status.Text="Search unavailable: "+ex.Message;}
        };
        query.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
        body.Loaded+=(_,_)=>query.Focus();
    }
    private static List<SearchHit> SearchNow(string term, Workspace ws,CancellationToken token)
    {
        var hits=new List<SearchHit>();
        foreach(var (name,uri) in new[]{
            ("Display","ms-settings:display"),("Bluetooth","ms-settings:bluetooth"),("Network","ms-settings:network"),
            ("Sound","ms-settings:sound"),("Windows Update","ms-settings:windowsupdate"),
            ("Default apps","ms-settings:defaultapps"),("Storage","ms-settings:storagesense"),
            ("Privacy","ms-settings:privacy"),("Taskbar","ms-settings:taskbar"),
            ("Date and time","ms-settings:dateandtime")})
            if(name.Contains(term,StringComparison.OrdinalIgnoreCase))
                hits.Add(new SearchHit{Category="Settings",Name=name,OpenTarget=uri,Location="Windows Settings"});
        foreach(var note in ws.Notes.Where(n=>n.Title.Contains(term,StringComparison.OrdinalIgnoreCase)||n.Content.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(25))
            hits.Add(new SearchHit{Category="Notes",Name=note.Title,NoteText=note.Content,Location="DeskSpace"});
        var roots=new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")
        };
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in roots)
        {
            if(!Directory.Exists(root))continue;
            var dirs=new Stack<string>();dirs.Push(root);
            var count=0;
            while(dirs.Count>0 && count<70000 && hits.Count<130)
            {
                token.ThrowIfCancellationRequested();
                var directory=dirs.Pop();
                string[] sub, files;
                try{sub=Directory.GetDirectories(directory);files=Directory.GetFiles(directory);}
                catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
                foreach(var d in sub)
                {
                    try{if((File.GetAttributes(d)&FileAttributes.ReparsePoint)==0)dirs.Push(d);}catch{}
                    if(Path.GetFileName(d).Contains(term,StringComparison.OrdinalIgnoreCase)&&seen.Add(d))
                        hits.Add(new SearchHit{Category="Files",Name=Path.GetFileName(d),Location=d,OpenTarget=d});
                }
                foreach(var file in files)
                {
                    if(++count>70000)break;
                    if(!Path.GetFileName(file).Contains(term,StringComparison.OrdinalIgnoreCase)||!seen.Add(file))continue;
                    bool app=file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||file.EndsWith(".url",StringComparison.OrdinalIgnoreCase);
                    hits.Add(new SearchHit{Category=app?"Applications":"Files",Name=Path.GetFileName(file),Location=file,OpenTarget=file});
                    if(hits.Count>=130)break;
                }
            }
        }
        return hits.OrderBy(h=>h.Category=="Applications"?0:h.Category=="Settings"?1:2).ThenBy(h=>h.Name).Take(120).ToList();
    }

    private void BuildNotes(StackPanel body)
    {
        body.Background=new SolidColorBrush(Color.FromRgb(255,247,204));
        body.Children.Add(Label("Sticky notes — saves to your existing DeskSpace notes",15));
        var picker=new ComboBox { MinHeight=34,DisplayMemberPath="Title",Margin=new Thickness(0,4,0,9) };
        var title=TextBox("Note title");var text=TextBox("Quick note",230);
        text.TextWrapping=TextWrapping.Wrap;text.AcceptsReturn=true;text.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        body.Children.Add(picker);body.Children.Add(title);body.Children.Add(text);
        var buttons=Row();body.Children.Add(buttons);
        Guid? editing=null;
        void Reload(Guid? selected=null)
        {
            var notes=Load().Notes.OrderByDescending(n=>n.Updated).ToList();
            picker.ItemsSource=notes;
            picker.SelectedItem=notes.FirstOrDefault(n=>n.Id==selected)??notes.FirstOrDefault();
        }
        picker.SelectionChanged+=(_,_)=>{
            if(picker.SelectedItem is NoteItem note){editing=note.Id;title.Text=note.Title;text.Text=note.Content;}
        };
        buttons.Children.Add(Btn("Save note",()=>{
            try {var id=editing??Guid.NewGuid();Mutate(ws=>{var note=ws.Notes.FirstOrDefault(n=>n.Id==id);
                if(note==null){note=new NoteItem{Id=id};ws.Notes.Add(note);}
                note.Title=string.IsNullOrWhiteSpace(title.Text)?"New note":title.Text.Trim();note.Content=text.Text;note.Updated=DateTime.Now;
            });editing=id;Reload(id);}catch(Exception e){Fail(e);}
        }));
        buttons.Children.Add(Btn("New",()=>{editing=null;picker.SelectedItem=null;title.Text="";text.Text="";title.Focus();}));
        buttons.Children.Add(Btn("Delete",()=>{
            if(editing is not Guid id)return;
            if(MessageBox.Show("Delete this note?","DeskSpace",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            try{Mutate(ws=>ws.Notes.RemoveAll(x=>x.Id==id));editing=null;Reload();}catch(Exception e){Fail(e);}
        }));
        Reload();
    }

    private void BuildReminders(StackPanel body)
    {
        body.Children.Add(Label("Reminders and tasks",21));
        var list=new ListBox{MinHeight=180,MaxHeight=240};body.Children.Add(list);
        var title=TextBox("Reminder title");
        var due=new DatePicker{SelectedDate=DateTime.Today,Margin=new Thickness(0,4,0,8)};
        var time=TextBox("HH:mm");time.Text="09:00";
        body.Children.Add(title);body.Children.Add(Label("Due date"));body.Children.Add(due);
        body.Children.Add(Label("Time (24-hour HH:mm)"));body.Children.Add(time);
        void Reload(){list.ItemsSource=Load().Reminders.OrderBy(r=>r.Done).ThenBy(r=>r.Due).ToList();}
        var buttons=Row();body.Children.Add(buttons);
        buttons.Children.Add(Btn("Add reminder",()=>{
            if(string.IsNullOrWhiteSpace(title.Text)||due.SelectedDate==null)return;
            if(!TimeSpan.TryParse(time.Text,out var clock)){MessageBox.Show("Use HH:mm time.");return;}
            try{Mutate(ws=>ws.Reminders.Add(new ReminderItem{Title=title.Text.Trim(),Due=due.SelectedDate.Value.Date+clock}));title.Clear();Reload();}catch(Exception e){Fail(e);}
        }));
        buttons.Children.Add(Btn("Complete / reopen",()=>{
            if(list.SelectedItem is not ReminderItem selected)return;
            try{Mutate(ws=>{var r=ws.Reminders.FirstOrDefault(x=>x.Id==selected.Id);if(r!=null)r.Done=!r.Done;});Reload();}catch(Exception e){Fail(e);}
        }));
        buttons.Children.Add(Btn("Delete",()=>{
            if(list.SelectedItem is not ReminderItem selected)return;
            try{Mutate(ws=>ws.Reminders.RemoveAll(x=>x.Id==selected.Id));Reload();}catch(Exception e){Fail(e);}
        }));
        Reload();
    }

    private void BuildCalendar(StackPanel body)
    {
        body.Children.Add(Label("Planner · Day / Week / Month / Agenda",21));
        var row=Row();body.Children.Add(row);
        DateTime cursor=DateTime.Today;
        var view=new ComboBox{ItemsSource=new[]{"Month","Week","Day","Agenda"},SelectedIndex=0,Width=130,Margin=new Thickness(0,0,10,0)};
        var previous=Btn("◀",()=>{cursor=cursor.AddDays(view.SelectedItem as string=="Month"?-30:view.SelectedItem as string=="Week"?-7:-1);Reload();});
        var next=Btn("▶",()=>{cursor=cursor.AddDays(view.SelectedItem as string=="Month"?30:view.SelectedItem as string=="Week"?7:1);Reload();});
        row.Children.Add(view);row.Children.Add(previous);row.Children.Add(next);
        var dayTitle=Label("",17);body.Children.Add(dayTitle);
        var calendar=new Calendar{SelectionMode=CalendarSelectionMode.SingleDate,Margin=new Thickness(0,2,0,8)};
        body.Children.Add(calendar);
        var events=new ListBox{MinHeight=125,MaxHeight=245};body.Children.Add(events);
        body.Children.Add(Label("New event"));var title=TextBox("Event title");body.Children.Add(title);
        var date=new DatePicker{SelectedDate=DateTime.Today};body.Children.Add(date);
        var repeat=new ComboBox{ItemsSource=new[]{"None","Daily","Weekly","Monthly","Yearly"},SelectedIndex=0,Margin=new Thickness(0,7,0,7)};
        body.Children.Add(Label("Repeat"));body.Children.Add(repeat);
        IEnumerable<EventItem> Occurrences(DateTime from,DateTime to)
        {
            foreach(var item in Load().Events)
            {
                var d=item.Date;
                for(var i=0;i<500&&d<=to;i++)
                {
                    if(d>=from) yield return new EventItem{Id=item.Id,Date=d,Title=item.Title,Recurrence=item.Recurrence};
                    if(item.Recurrence=="Daily")d=d.AddDays(1);
                    else if(item.Recurrence=="Weekly")d=d.AddDays(7);
                    else if(item.Recurrence=="Monthly")d=d.AddMonths(1);
                    else if(item.Recurrence=="Yearly")d=d.AddYears(1);
                    else break;
                }
            }
        }
        void Reload()
        {
            var mode=view.SelectedItem as string??"Month";
            var from=mode switch{
                "Day"=>cursor.Date,
                "Week"=>cursor.Date.AddDays(-(int)cursor.DayOfWeek),
                "Agenda"=>cursor.Date,
                _=>new DateTime(cursor.Year,cursor.Month,1)
            };
            var to=mode switch{"Day"=>from.AddDays(1),"Week"=>from.AddDays(7),"Agenda"=>from.AddDays(60),_=>from.AddMonths(1)};
            dayTitle.Text=mode+" · "+from.ToString("d MMM yyyy")+(mode=="Day"?"":" — "+to.AddDays(-1).ToString("d MMM yyyy"));
            events.ItemsSource=Occurrences(from,to).OrderBy(x=>x.Date).Take(100).ToList();
            calendar.DisplayDate=cursor;
            calendar.Visibility=mode=="Agenda"?Visibility.Collapsed:Visibility.Visible;
        }
        view.SelectionChanged+=(_,_)=>Reload();
        calendar.SelectedDatesChanged+=(_,_)=>{if(calendar.SelectedDate is DateTime selected){cursor=selected;date.SelectedDate=selected;Reload();}};
        var buttons=Row();body.Children.Add(buttons);
        buttons.Children.Add(Btn("Add event",()=>{
            if(string.IsNullOrWhiteSpace(title.Text)||date.SelectedDate==null)return;
            try{Mutate(ws=>ws.Events.Add(new EventItem{Title=title.Text.Trim(),Date=date.SelectedDate.Value,Recurrence=repeat.SelectedItem as string??"None"}));title.Clear();Reload();}catch(Exception e){Fail(e);}
        }));
        buttons.Children.Add(Btn("Delete selected",()=>{
            if(events.SelectedItem is not EventItem e)return;
            try{Mutate(ws=>ws.Events.RemoveAll(x=>x.Id==e.Id));Reload();}catch(Exception ex){Fail(ex);}
        }));
        Reload();
    }

    private void BuildClipboard(StackPanel body)
    {
        body.Children.Add(Label("Clipboard · recent text and temporary file shelf",20));
        body.Children.Add(Label("History is memory-only and is cleared when the widget exits. Original files are never deleted.",12));
        var list=new ListBox{MinHeight=160,MaxHeight=230};body.Children.Add(list);
        var shelf=new ListBox{MinHeight=95,MaxHeight=135};body.Children.Add(Label("Temporary file shelf"));body.Children.Add(shelf);
        void Reload(){list.ItemsSource=DeskSpaceClipboardV10.History.ToList();shelf.ItemsSource=DeskSpaceClipboardV10.Shelf.ToList();}
        var row=Row();body.Children.Add(row);
        row.Children.Add(Btn("Copy selected",()=>{
            if(list.SelectedItem is DeskSpaceClipboardV10.ClipboardEntry entry)
            {try{Clipboard.SetText(entry.Text);done();}catch(Exception e){Fail(e);}}
        }));
        row.Children.Add(Btn("Hold copied files",()=>{DeskSpaceClipboardV10.HoldCurrentFiles();Reload();}));
        row.Children.Add(Btn("Copy shelved files",()=>{
            if(DeskSpaceClipboardV10.Shelf.Count==0)return;
            var paths=new StringCollection();
            paths.AddRange(DeskSpaceClipboardV10.Shelf.Where(File.Exists).ToArray());
            if(paths.Count>0)Clipboard.SetFileDropList(paths);
            // Do not claim external paste succeeded: the user must confirm it.
        }));
        row.Children.Add(Btn("Pasted — clear shelf",()=>{DeskSpaceClipboardV10.ClearShelf();Reload();}));
        row.Children.Add(Btn("Clear history",()=>{DeskSpaceClipboardV10.ClearHistory();Reload();}));
        Reload();
    }

    private void BuildPassword(StackPanel body)
    {
        body.Children.Add(Label("Encrypted Password Vault",22));
        body.Children.Add(Label("Uses AES-256-GCM and a master password. No browser autofill yet. Do not reuse another account's password.",12));
        vault=new DeskSpaceVaultV10();
        var auth=new PasswordBox{MinHeight=35,Margin=new Thickness(0,6,0,8)};
        var authTitle=Label(vault.IsInitialized?"Enter master password":"Create a master password (12+ characters)");
        body.Children.Add(authTitle);body.Children.Add(auth);
        var entries=new ListBox{MinHeight=160,MaxHeight=210};
        var label=TextBox("Website or account label");
        var username=TextBox("Username");
        var secret=new PasswordBox{MinHeight=34,Margin=new Thickness(0,4,0,8)};
        var url=TextBox("Website URL");
        var editor=new StackPanel{Visibility=Visibility.Collapsed};
        editor.Children.Add(entries);editor.Children.Add(label);editor.Children.Add(username);
        editor.Children.Add(Label("Password"));editor.Children.Add(secret);editor.Children.Add(url);body.Children.Add(editor);
        var row=Row();body.Children.Add(row);
        DateTime lastAccess=DateTime.UtcNow;Guid? current=null;
        void Refresh()
        {
            entries.ItemsSource=vault.Entries.Select(e=>new VaultRow(e.Id,e.Label,e.Username)).ToList();
        }
        entries.SelectionChanged+=(_,_)=>{
            if(entries.SelectedItem is not VaultRow item)return;
            var record=vault.Entries.FirstOrDefault(e=>e.Id==item.Id);
            if(record==null)return;
            current=record.Id;label.Text=record.Label;username.Text=record.Username;url.Text=record.Website;
            secret.Password=record.Password;lastAccess=DateTime.UtcNow;
        };
        var unlock=Btn(vault.IsInitialized?"Unlock vault":"Create vault",()=>{
            try{
                vault.Unlock(auth.Password);auth.Clear();editor.Visibility=Visibility.Visible;auth.Visibility=Visibility.Collapsed;
                authTitle.Text="Vault unlocked — locks automatically after 3 minutes";
                lastAccess=DateTime.UtcNow;Refresh();
            }catch(Exception e){Fail(new InvalidOperationException("Cannot unlock vault. Check the master password or encrypted vault file.",e));}
        });
        row.Children.Add(unlock);
        row.Children.Add(Btn("Save entry",()=>{
            if(!vault.IsUnlocked)return;
            try{vault.Upsert(new DeskSpaceVaultV10.Secret{Id=current??Guid.NewGuid(),Label=label.Text,Username=username.Text,Password=secret.Password,Website=url.Text});
                current=null;label.Clear();username.Clear();secret.Clear();url.Clear();lastAccess=DateTime.UtcNow;Refresh();
            }catch(Exception e){Fail(e);}
        }));
        row.Children.Add(Btn("New",()=>{current=null;label.Clear();username.Clear();secret.Clear();url.Clear();}));
        row.Children.Add(Btn("Copy password",()=>{
            if(!vault.IsUnlocked||entries.SelectedItem is not VaultRow selected)return;
            var found=vault.Entries.FirstOrDefault(x=>x.Id==selected.Id);
            if(found==null)return;Clipboard.SetText(found.Password);lastAccess=DateTime.UtcNow;
        }));
        row.Children.Add(Btn("Delete",()=>{
            if(!vault.IsUnlocked||entries.SelectedItem is not VaultRow selected)return;
            if(MessageBox.Show("Delete this password?","DeskSpace",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            vault.Delete(selected.Id);Refresh();
        }));
        row.Children.Add(Btn("Lock",()=>{vault.Lock();editor.Visibility=Visibility.Collapsed;auth.Visibility=Visibility.Visible;authTitle.Text="Vault locked";secret.Clear();}));
        vaultTimeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};
        vaultTimeout.Tick+=(_,_)=>{
            if(vault.IsUnlocked && DateTime.UtcNow-lastAccess>TimeSpan.FromMinutes(3))
            {vault.Lock();editor.Visibility=Visibility.Collapsed;auth.Visibility=Visibility.Visible;authTitle.Text="Vault locked (inactivity)";secret.Clear();}
        };
        vaultTimeout.Start();
        body.PreviewMouseDown+=(_,_)=>lastAccess=DateTime.UtcNow;
        body.PreviewKeyDown+=(_,_)=>lastAccess=DateTime.UtcNow;
    }
    private sealed record VaultRow(Guid Id,string Label,string Username)
    { public override string ToString()=>Label+"   ·   "+Username; }

    private void BuildCalculator(StackPanel body)
    {
        body.Children.Add(Label("Calculator and quick calculations",20));
        var expr=TextBox("Enter a calculation, e.g. (250 + 80) * 1.18");body.Children.Add(expr);
        var answer=Label("0",28);body.Children.Add(answer);
        var row=Row();body.Children.Add(row);
        row.Children.Add(Btn("Calculate",()=>{
            try {
                var input=expr.Text.Trim();
                if(input.Length>120||!Regex.IsMatch(input,@"^[0-9\s+*/().,%\-]+$"))throw new FormatException("Only arithmetic expressions are supported.");
                var value=new DataTable().Compute(input,"");
                answer.Text=Convert.ToString(value)??"0";
            }catch(Exception e){answer.Text="Invalid calculation: "+e.Message;}
        }));
        row.Children.Add(Btn("Copy result",()=>{Clipboard.SetText(answer.Text);done();}));
        row.Children.Add(Btn("Open Windows Calculator",()=>{
            try{Process.Start(new ProcessStartInfo("calc.exe"){UseShellExecute=true});done();}catch(Exception e){Fail(e);}
        }));
        expr.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){((Button)row.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}};
    }
    public void Dispose()
    {
        disposed=true;queryCancel?.Cancel();queryCancel?.Dispose();debounce?.Stop();
        vaultTimeout?.Stop();vault?.Dispose();
    }
}
