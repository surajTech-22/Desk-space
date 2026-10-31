using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskSpace;

public partial class MainWindow : Window
{
    private readonly WorkspaceStore store = new();
    private readonly Workspace data;
    private readonly DispatcherTimer uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly System.Windows.Forms.NotifyIcon tray;
    private FloatingDashboardWindow? desktopWidget;
    private bool loadingEditor;
    private bool readOnlyRecoveryMode;
    private DataTab? activeTab;
    private DataTable? table;
    private DateTime? timerDeadline;
    private WidgetSettingsV3? _settingsCache;
    private DateTime _nextSettingsRead = DateTime.MinValue;
    public MainWindow()
    {
        DeskSpaceSmoothScrollV41.Enable();
        InitializeComponent();
        InitializeFeatureHubV199();
        InitializeExcelNavigationV49();
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
        // Register once per process, covering Workspace windows even when
        // a separate widget/tray process is active.
        DeskSpaceGracefulExitV230.Attach(this);
        VersionButton.Content = "v" + DeskSpaceUpdater.CurrentVersion;
        SettingsVersionText.Text = "DeskSpace version " + DeskSpaceUpdater.CurrentVersion;
        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "")!.Handle,
            Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        try { data = store.Load(); }
        catch (IOException ex)
        {
            MessageBox.Show(ex.Message + "\n\nThe app will open with an empty in-memory workspace. Do not save over the original until it is recovered.",
                            "Could not load workspace", MessageBoxButton.OK, MessageBoxImage.Error);
            data = new Workspace();
            readOnlyRecoveryMode = true;
        }
        tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "DeskSpace"
        };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show DeskSpace", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        trayMenu.Items.Add("Show Floating Widget", null, (_, _) => Dispatcher.Invoke(ShowFloatingWidget));
        trayMenu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        tray.ContextMenuStrip = trayMenu;
        NotesList.ItemsSource = data.Notes;
        var activeReminders=System.Windows.Data.CollectionViewSource.GetDefaultView(data.Reminders);
        activeReminders.Filter=item=>item is ReminderItem reminder && !reminder.Done;
        RemindersList.ItemsSource=activeReminders;
        SnippetsList.ItemsSource = data.Snippets;
        DocumentsList.ItemsSource = data.Documents;
        DataTabList.ItemsSource = data.DataTabs;
        EventCalendar.SelectedDate = DateTime.Today;
        ReminderDate.SelectedDate = DateTime.Today;
        if (data.Notes.Count > 0) NotesList.SelectedIndex = 0;
        if (data.Snippets.Count > 0) SnippetsList.SelectedIndex = 0;
        if (data.DataTabs.Count > 0) DataTabList.SelectedIndex = 0;
        RefreshEvents(); RefreshDashboard();
        uiTimer.Tick += (_, _) => Tick();
        uiTimer.Start();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SafeSave(); };
        Tick();
        if (WidgetSettingsV3.Load().ShowAtLaunch)
            Dispatcher.BeginInvoke(new Action(ShowFloatingWidget), DispatcherPriority.ApplicationIdle);
    }
    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void ShowFloatingWidget() => WidgetHostLauncher.EnsureRunning(true);
    private void ShowWidget_Click(object sender, RoutedEventArgs e) => WidgetHostLauncher.EnsureRunning(force:true);
    private async void GitHubUpdates_Click(object sender, RoutedEventArgs e) => await DeskSpaceUpdateCheckV42.CheckAsync(this);
    private void OpenGitHubBuilds()
    {
        MessageBox.Show(
            "DeskSpace's GitHub source is private. Source commits cannot update this installed app. " +
            "Automatic downloads require an authenticated update feed or a public binary-only release channel. " +
            "Until then, open your GitHub builds, download the verified installer and run it to upgrade. " +
            "Your existing workspace data stays in LocalAppData.",
            "DeskSpace - updates", MessageBoxButton.OK, MessageBoxImage.Information);
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName = "https://github.com/surajTech-22/Desk-space/actions/workflows/build-installer.yml",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot open GitHub"); }
    }
    private void AppVersion_Click(object sender,RoutedEventArgs e)
    {
        MessageBox.Show("DeskSpace " + DeskSpaceUpdater.CurrentVersion +
            "\n\nAutomatic updates: Enabled via private GitHub releases" +
            "\nYour workspace data stays in " + WorkspaceStore.Root,
            "DeskSpace version information",MessageBoxButton.OK,MessageBoxImage.Information);
    }
    private void UniversalSearch_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
    private void RecentFiles_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
    private void WidgetSettings_Click(object sender,RoutedEventArgs e)
    {
        var settings=WidgetSettingsV3.Load();
        DeskSpaceSettingsStudioV148.Show(this,settings,()=>{
            settings.Save();settings.ApplyWindowsStartup();
        },()=>new WidgetOptionsWindow(settings,
            ()=>{settings.Save();settings.ApplyWindowsStartup();}).Show());
    }
    private async void CheckForUpdates_Click(object sender,RoutedEventArgs e)
    {
        await DeskSpaceUpdateCheckV42.CheckAsync(this);
    }
    private void ModuleNavigate_Click(object sender,RoutedEventArgs e)
    {
        if(sender is Button button && button.Tag is string id)
        {
            foreach(var tab in MainTabs.Items.OfType<TabItem>())
                if(string.Equals(tab.Tag as string,id,StringComparison.OrdinalIgnoreCase))
                { MainTabs.SelectedItem=tab; break; }
        }
    }    // The mapping UI is genuinely embedded in the main application.
    // Both the left navigation tab and the Home/Modules shortcuts
    // select the same persistent instance, not a separate floating window.
    private DeskSpaceExcelV16? workspaceExcel;
    private void ExcelMainHost_Loaded(object sender,RoutedEventArgs e)
    {
        if(workspaceExcel!=null)return;
        workspaceExcel=new DeskSpaceExcelV16(
            ()=>MainTabs.SelectedIndex=0,inMainWorkspace:true);
        ExcelMainHost.Children.Add(workspaceExcel.Create());
    }
    private void ExcelModule_Click(object sender,RoutedEventArgs e)
    {
        MainTabs.SelectedItem=ExcelMainTab;
    }
    private bool updateChannelReadyV104;
    private void UpdateChannel_LoadedV104(object sender,RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.ComboBox picker)return;
        updateChannelReadyV104=false;
        picker.SelectedIndex=DeskSpacePublicUpdaterV44.Channel=="tester"?1:0;
        updateChannelReadyV104=true;
    }
    private async void UpdateChannel_ChangedV104(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!updateChannelReadyV104 || sender is not System.Windows.Controls.ComboBox picker ||
           picker.SelectedItem is not System.Windows.Controls.ComboBoxItem choice)
            return;
        string requested=choice.Tag?.ToString()??"stable";
        if(requested==DeskSpacePublicUpdaterV44.Channel)return;
        try
        {
            DeskSpacePublicUpdaterV44.SetChannel(requested);
            await DeskSpacePublicUpdaterV44.CheckAsync(this,false);
        }
        catch(Exception error)
        {
            System.Windows.MessageBox.Show(this,"Channel change failed: "+error.Message,
                "DeskSpace updates",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

    private void SafeSave()
    {
        if (readOnlyRecoveryMode) { StatusText.Text = "SAVE DISABLED: damaged workspace must be recovered or renamed before editing"; return; }
        try { store.Save(data); StatusText.Text = "Saved locally · " + DateTime.Now.ToLongTimeString(); }
        catch (Exception ex) { StatusText.Text = "SAVE FAILED: " + ex.Message; MessageBox.Show(ex.Message, "Save failure", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void QueueSave() { saveTimer.Stop(); saveTimer.Start(); }
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        saveTimer.Stop(); SaveGridInternal(); SafeSave(); tray.Dispose();
    }
    private void Tick()
    {
        ClockText.Text = DateTime.Now.ToString("dddd, dd MMM yyyy\nHH:mm:ss", CultureInfo.CurrentCulture);
        if (_settingsCache == null || DateTime.UtcNow >= _nextSettingsRead)
        {
            _settingsCache = WidgetSettingsV3.Load();
            _nextSettingsRead = DateTime.UtcNow.AddMinutes(1);
        }
        var local=_settingsCache;
        if (local.ShowWeather)
        {
            var weather=DeskSpaceWeather.Latest;
            var display=weather != null && weather.City.Equals(local.WeatherCity,StringComparison.OrdinalIgnoreCase)
                ? DeskSpaceWeather.Icon(weather.Code,weather.IsDay)+" "+Math.Round(weather.Celsius)+"°C   "+DeskSpaceWeather.Description(weather.Code)+" • "+weather.City
                : "Weather in "+local.WeatherCity+" — connecting";
            if (ClockWeather.Text != display) ClockWeather.Text=display;
            if (DateTime.Now.Second==0) _=DeskSpaceWeather.RefreshAsync(local.WeatherCity);
        }
        else if (ClockWeather.Text != "Weather is disabled in widget settings")
            ClockWeather.Text="Weather is disabled in widget settings";
        DashboardTime.Text = DateTime.Now.ToString("dddd, d MMMM yyyy · h:mm tt", CultureInfo.CurrentCulture);
        if (timerDeadline is DateTime until)
        {
            var remaining = until - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                timerDeadline = null; TimerRemaining.Text = "Finished"; tray.ShowBalloonTip(5000, "Timer finished", "Your countdown has finished.", System.Windows.Forms.ToolTipIcon.Info);
            }
            else TimerRemaining.Text = remaining.ToString(remaining.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss");
        }
        bool updated = false;
        foreach (var r in data.Reminders)
        {
            if (!r.Done && r.LastAlert is null && r.Due <= DateTime.Now)
            {
                r.LastAlert = DateTime.Now;
                tray.ShowBalloonTip(8000, "DeskSpace reminder", r.Title, System.Windows.Forms.ToolTipIcon.Info);
                updated = true;
            }
        }
        if (updated) { RemindersList.Items.Refresh(); SafeSave(); RefreshDashboard(); }
    }
    private void RefreshDashboard()
    {
        DashboardSummary.Text = $"{data.Notes.Count} notes  ·  {data.Reminders.Count(r => !r.Done)} pending reminders  ·  {data.Events.Count(e => e.Date.Date == DateTime.Today)} events today  ·  {data.Documents.Count} documents  ·  {data.DataTabs.Count} custom data tabs";
    }
    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var note = new NoteItem { Title = "New note" };
        data.Notes.Add(note); NotesList.Items.Refresh(); NotesList.SelectedItem = note;
        MainTabs.SelectedIndex = 1; SafeSave(); RefreshDashboard();
    }
    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        loadingEditor = true;
        var note = NotesList.SelectedItem as NoteItem;
        NoteTitle.Text = note?.Title ?? ""; NoteContent.Text = note?.Content ?? "";
        NoteTitle.IsEnabled = NoteContent.IsEnabled = note != null;
        loadingEditor = false;
    }
    private void NoteEditor_Changed(object sender, TextChangedEventArgs e)
    {
        if (loadingEditor || NotesList.SelectedItem is not NoteItem item) return;
        item.Title = NoteTitle.Text; item.Content = NoteContent.Text; item.Updated = DateTime.Now;
        NotesList.Items.Refresh(); QueueSave();
    }
    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem item) return;
        data.Notes.Remove(item); NotesList.Items.Refresh(); desktopWidget?.ReloadNotes(); SafeSave(); RefreshDashboard();
    }
    private void FloatNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem note)
        {
            if (data.Notes.Count == 0) { AddNote_Click(sender, e); }
            else NotesList.SelectedItem = data.Notes[0];
            note = NotesList.SelectedItem as NoteItem;
        }
        if (note is null) return;
        var window = new FloatingNoteWindow(note, () => { NotesList.Items.Refresh(); SafeSave(); });
        window.Show();
        desktopWidget?.ReloadNotes(note);
    }
    private void CalendarSelection_Changed(object sender, SelectionChangedEventArgs e) => RefreshEvents();
    private void RefreshEvents()
    {
        if (EventsList is null || EventCalendar is null) return;
        var selected = (EventCalendar.SelectedDate ?? DateTime.Today).Date;
        EventsList.ItemsSource = data.Events.Where(x => x.Date.Date == selected).OrderBy(x => x.Date).ToList();
    }
    private void AddEvent_Click(object sender, RoutedEventArgs e)
    {
        var title = EventTitle.Text.Trim(); if (title.Length == 0) return;
        data.Events.Add(new EventItem { Title = title, Date = (EventCalendar.SelectedDate ?? DateTime.Today).Date });
        EventTitle.Clear(); RefreshEvents(); SafeSave(); RefreshDashboard();
    }
    private void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        if (EventsList.SelectedItem is EventItem ev) { data.Events.RemoveAll(x => x.Id == ev.Id); RefreshEvents(); SafeSave(); RefreshDashboard(); }
    }
    private string reminderSelectedPath="";
    private string reminderSelectedName="";
    private bool reminderSoundLoading=false;
    private DeskSpaceAutomationEditorV84? reminderAutomation;
    private void ReminderAutomationHost_Loaded(object sender,RoutedEventArgs e)
    {
        if(reminderAutomation!=null || ReminderAutomationHost==null)return;
        reminderAutomation=new DeskSpaceAutomationEditorV84(
            ()=>{
                var mode=ReminderMode(ReminderActionChoice);
                return (mode,mode=="Open URL"?
                    ReminderActionPath.Text.Trim():reminderSelectedPath);
            },
            (mode,path)=>{
                foreach(System.Windows.Controls.ComboBoxItem option in
                    ReminderActionChoice.Items)
                    if(option.Content?.ToString()==mode)
                    {ReminderActionChoice.SelectedItem=option;break;}
                if(mode=="Open URL")ReminderActionPath.Text=path;
                else SetSelectedReminderTarget(path,
                    DeskSpaceReminderPresentationV81.TargetName(mode,path));
            });
        ReminderAutomationHost.Children.Add(reminderAutomation.Create());
    }

    private static string ReminderMode(System.Windows.Controls.ComboBox? choice)
        => (choice?.SelectedItem as System.Windows.Controls.ComboBoxItem)?
           .Content?.ToString()??"None";

    private void ReminderTime_TextChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if(ReminderTimePreview==null || ReminderTime==null)return;
        ReminderTimePreview.Text=DeskSpaceReminderTimeV78.TryParse(
            ReminderTime.Text,out _,out var display)
            ?"Time: "+display
            :"Enter HMMSS / HHMMSS; add AM or PM for 12-hour mode";
    }
    private void ReminderTime_LostKeyboardFocus(object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if(DeskSpaceReminderTimeV78.TryParse(ReminderTime.Text,
            out _,out var display))ReminderTime.Text=display;
    }
    private void ReminderSoundChoice_Loaded(object sender,RoutedEventArgs e)
    {
        if(ReminderSoundChoice==null||ReminderSoundPath==null)return;
        reminderSoundLoading=true;
        var defaults=DeskSpaceReminderPreferencesV81.Load();
        foreach(System.Windows.Controls.ComboBoxItem option
            in ReminderSoundChoice.Items)
            if(option.Content?.ToString()==defaults.Name)
            {
                ReminderSoundChoice.SelectedItem=option;break;
            }
        ReminderSoundPath.Text=defaults.File;
        reminderSoundLoading=false;
        ReminderSoundChoice_SelectionChanged(
            ReminderSoundChoice,new SelectionChangedEventArgs(
                System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                Array.Empty<object>(),Array.Empty<object>()));
    }
    private void SaveReminderSoundDefault()
    {
        if(reminderSoundLoading||ReminderSoundChoice==null||
           ReminderSoundPath==null)return;
        bool success=DeskSpaceReminderPreferencesV81.Save(
            ReminderMode(ReminderSoundChoice),ReminderSoundPath.Text,
            out var message);
        if(ReminderSoundSaveStatus!=null)
            ReminderSoundSaveStatus.Text=success?"":
                "Could not save sound: "+message;
    }
    private void ReminderSoundPath_TextChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
        =>SaveReminderSoundDefault();
    private void ReminderSoundChoice_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(ReminderSoundPath!=null)
            ReminderSoundPath.Visibility=
                ReminderMode(ReminderSoundChoice)=="Custom WAV"?
                    Visibility.Visible:Visibility.Collapsed;
        SaveReminderSoundDefault();
    }

    private void SetSelectedReminderTarget(string path,string displayName)
    {
        reminderSelectedPath=path;reminderSelectedName=displayName;
        RefreshReminderActionLabel();
    }
    private void RefreshReminderActionLabel()
    {
        if(ReminderActionDisplay==null||ReminderActionChoice==null)return;
        string mode=ReminderMode(ReminderActionChoice);
        string target=mode=="Open URL"?
            ReminderActionPath?.Text??"":reminderSelectedPath;
        ReminderActionDisplay.Text=
            DeskSpaceReminderPresentationV81.ActionLabel(
                mode,target,reminderSelectedName);
        if(ReminderActionIcon!=null)
        {
            var icon=mode is "Open application" or "Open file"?
                DeskSpaceApplicationIconsV65.Get(reminderSelectedPath):null;
            ReminderActionIcon.Source=icon;
            ReminderActionIcon.Visibility=icon==null?
                Visibility.Collapsed:Visibility.Visible;
        }
    }
    private void ReminderActionPath_TextChanged(object sender,
        System.Windows.Controls.TextChangedEventArgs e)
        =>RefreshReminderActionLabel();

    private void ReminderActionChoice_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(ReminderActionPath==null||ReminderActionChoice==null)return;
        string mode=ReminderMode(ReminderActionChoice);
        reminderSelectedPath="";reminderSelectedName="";
        ReminderTitle.Visibility=
            DeskSpaceReminderPresentationV81.NeedsTitle(mode)?
                Visibility.Visible:Visibility.Collapsed;
        ReminderActionPath.Visibility=mode=="Open URL"?
            Visibility.Visible:Visibility.Collapsed;
        ReminderActionPath.ToolTip="Paste the web address";
        if(ReminderBrowseFileButton!=null)
            ReminderBrowseFileButton.Visibility=mode=="Open file"?
                Visibility.Visible:Visibility.Collapsed;
        if(ReminderBrowseAppButton!=null)
            ReminderBrowseAppButton.Visibility=mode=="Open application"?
                Visibility.Visible:Visibility.Collapsed;
        if(ReminderUrlNote!=null)
            ReminderUrlNote.Visibility=mode=="Open URL"?
                Visibility.Visible:Visibility.Collapsed;
        if(ReminderEmbeddedSearch!=null)
        {
            ReminderEmbeddedSearch.Children.Clear();
            ReminderEmbeddedSearch.Visibility=mode is "Open application" or "Open file"?
                Visibility.Visible:Visibility.Collapsed;
            if(mode is "Open application" or "Open file")
                ReminderEmbeddedSearch.Children.Add(
                    DeskSpaceReminderPickerV81.Create(mode,
                        (path,name)=>SetSelectedReminderTarget(path,name)));
        }
        RefreshReminderActionLabel();
    }
    private void ReminderChooseAction_Click(object sender,RoutedEventArgs e)
    {
        ReminderActionChoice.IsDropDownOpen=true;
    }
    private void ReminderTestSound_Click(object sender,RoutedEventArgs e)
    {
        DeskSpaceDueShelfV74.PlaySound(new ReminderItem{
            AlertSound=ReminderMode(ReminderSoundChoice),
            AlertSoundFile=ReminderSoundPath.Text.Trim()
        });
    }
    private void ReminderBrowseWav_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFileDialog{
            Filter="WAV audio (*.wav)|*.wav",CheckFileExists=true
        };
        if(dialog.ShowDialog()==true)
        {
            ReminderSoundPath.Text=dialog.FileName;
            ReminderSoundChoice.SelectedIndex=5;
        }
    }
    private void ReminderBrowseFile_Click(object sender,RoutedEventArgs e)
    {
        if(ReminderMode(ReminderActionChoice)!="Open file")return;
        var d=new Microsoft.Win32.OpenFileDialog{
            Filter="All files (*.*)|*.*",CheckFileExists=true};
        if(d.ShowDialog()==true)
            SetSelectedReminderTarget(d.FileName,
                System.IO.Path.GetFileName(d.FileName));
    }
    private void ReminderBrowseTarget_Click(object sender,RoutedEventArgs e)
    {
        if(ReminderMode(ReminderActionChoice)!="Open application")return;
        var d=new Microsoft.Win32.OpenFileDialog{
            Filter="Applications (*.exe;*.lnk)|*.exe;*.lnk",
            CheckFileExists=true};
        if(d.ShowDialog()==true)
            SetSelectedReminderTarget(d.FileName,
                System.IO.Path.GetFileNameWithoutExtension(d.FileName));
    }

    private string RepeatChoiceName()=>
        ReminderRepeatChoice?.SelectedItem is System.Windows.Controls.ComboBoxItem item?
            item.Content?.ToString()??"None":"None";
    private int RepeatIntervalDays()=>int.TryParse(ReminderRepeatEvery?.Text,
        out var days)?Math.Clamp(days,1,365):1;

    private void ReminderRepeatChoice_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(ReminderRepeatDays==null||ReminderRepeatEvery==null)return;
        var rule=RepeatChoiceName();
        ReminderRepeatDays.Visibility=rule=="Selected days"?
            Visibility.Visible:Visibility.Collapsed;
        ReminderRepeatEvery.Visibility=rule=="Every N days"?
            Visibility.Visible:Visibility.Collapsed;
    }
    private void ReminderHistory_Click(object sender,RoutedEventArgs e)
    {
        var recent=DeskSpaceReminderActionHistoryV83.ReadRecent(25);
        MessageBox.Show(recent.Count==0?"No actions have run yet":
            string.Join(Environment.NewLine,
                recent.Select(a=>a.At.ToString("g")+"  "+a.Title+
                    "  "+a.Result)),
            "DeskSpace reminder action history");
    }

    private void AddReminder_Click(object sender,RoutedEventArgs e)
    {
        string action=ReminderMode(ReminderActionChoice);
        if(DeskSpaceReminderPresentationV81.NeedsTitle(action) &&
           string.IsNullOrWhiteSpace(ReminderTitle.Text))return;
        if(!DeskSpaceReminderTimeV78.TryParse(ReminderTime.Text,
            out var scheduled,out var normalized))
        {
            MessageBox.Show("Time examples: 72345 = 07:23:45, "+
                "122345 = 12:23:45. Optional AM/PM.");
            return;
        }
        ReminderTime.Text=normalized;
        string path=action=="Open URL"?
            ReminderActionPath.Text.Trim():reminderSelectedPath;
        if(action=="Open URL" &&
           DeskSpaceReminderBrowserV80.TryUrl(path,out var cleaned))
            path=cleaned;
        if(!DeskSpaceDueShelfV74.IsAllowedAction(action,path))
        {
            MessageBox.Show("Choose an existing application/file or valid http(s) URL.");
            return;
        }
        string sound=ReminderMode(ReminderSoundChoice);
        string wav=ReminderSoundPath.Text.Trim();
        if(sound=="Custom WAV" &&
           (!System.IO.File.Exists(wav) ||
            !System.IO.Path.GetExtension(wav)
              .Equals(".wav",StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Choose an existing WAV file.");return;
        }
        data.Reminders.Add(new ReminderItem{
            Title=DeskSpaceReminderPresentationV81.ResolveTitle(
                ReminderTitle.Text,action,path,reminderSelectedName),
            Due=(ReminderDate.SelectedDate??DateTime.Today).Date+scheduled,
            AlertSound=sound,AlertSoundFile=wav,
            AlertAction=action,AlertTarget=path,
            RepeatPattern=RepeatChoiceName(),
            RepeatDays=ReminderRepeatDays.Text.Trim(),
            RepeatEveryDays=RepeatIntervalDays(),
            AutomationSteps=reminderAutomation?.Steps??new(),
            TriggerCondition=reminderAutomation?.ConditionMode??"None",
            TriggerTarget=reminderAutomation?.ConditionTarget??""
        });
        ReminderTitle.Clear();
        RemindersList.Items.Refresh();
        SafeSave();
        RefreshDashboard();
    }
    private void RemindersList_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(RemindersList?.SelectedItem is not ReminderItem selected ||
           ReminderActionChoice==null)return;
        ReminderTitle.Text=selected.Title;
        ReminderDate.SelectedDate=selected.Due.Date;
        ReminderTime.Text=selected.Due.ToString("HH:mm:ss");
        reminderAutomation?.Load(selected);
        foreach(System.Windows.Controls.ComboBoxItem option
            in ReminderRepeatChoice.Items)
            if(option.Content?.ToString()==selected.RepeatPattern)
            {ReminderRepeatChoice.SelectedItem=option;break;}
        ReminderRepeatDays.Text=selected.RepeatDays??"";
        ReminderRepeatEvery.Text=selected.RepeatEveryDays.ToString();
        reminderSoundLoading=true;
        foreach(System.Windows.Controls.ComboBoxItem item
            in ReminderSoundChoice.Items)
            if(item.Content?.ToString()==selected.AlertSound)
            {
                ReminderSoundChoice.SelectedItem=item;break;
            }
        ReminderSoundPath.Text=selected.AlertSoundFile??"";
        reminderSoundLoading=false;
        ReminderSoundChoice_SelectionChanged(
            ReminderSoundChoice,new SelectionChangedEventArgs(
                System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                Array.Empty<object>(),Array.Empty<object>()));
        foreach(System.Windows.Controls.ComboBoxItem item
            in ReminderActionChoice.Items)
            if(item.Content?.ToString()==selected.AlertAction)
            {
                ReminderActionChoice.SelectedItem=item;break;
            }
        if(selected.AlertAction=="Open URL")
            ReminderActionPath.Text=selected.AlertTarget;
        else
            SetSelectedReminderTarget(selected.AlertTarget,
                DeskSpaceReminderPresentationV81.TargetName(
                    selected.AlertAction,selected.AlertTarget));
    }

    private void SaveReminderChanges_Click(object sender,RoutedEventArgs e)
    {
        if(RemindersList.SelectedItem is not ReminderItem selected)return;
        string mode=ReminderMode(ReminderActionChoice);
        string path=mode=="Open URL"?
            ReminderActionPath.Text.Trim():reminderSelectedPath;
        if(mode=="Open URL" &&
           DeskSpaceReminderBrowserV80.TryUrl(path,out var normalized))
            path=normalized;
        if(!DeskSpaceDueShelfV74.IsAllowedAction(mode,path))return;
        if(!DeskSpaceReminderTimeV78.TryParse(ReminderTime.Text,
            out var clock,out _) )return;
        selected.Title=DeskSpaceReminderPresentationV81.ResolveTitle(
            ReminderTitle.Text,mode,path,reminderSelectedName);
        var nextDue=(ReminderDate.SelectedDate??DateTime.Today).Date+clock;
        if(selected.Due!=nextDue ||
           selected.RepeatPattern!=RepeatChoiceName())
        {
            selected.LastAlert=null;
            selected.SnoozedUntil=null;
            selected.SnoozeOnly=false;
        }
        selected.Due=nextDue;
        selected.RepeatPattern=RepeatChoiceName();
        selected.RepeatDays=ReminderRepeatDays.Text.Trim();
        selected.RepeatEveryDays=RepeatIntervalDays();
        selected.AlertAction=mode;
        selected.AlertTarget=path;
        selected.AlertSound=ReminderMode(ReminderSoundChoice);
        selected.AlertSoundFile=ReminderSoundPath.Text.Trim();
        selected.AutomationSteps=reminderAutomation?.Steps??new();
        selected.TriggerCondition=reminderAutomation?.ConditionMode??"None";
        selected.TriggerTarget=reminderAutomation?.ConditionTarget??"";
        RemindersList.Items.Refresh();SafeSave();RefreshDashboard();
    }

    private void DoneReminder_Click(object sender,RoutedEventArgs e)
    {
        if(RemindersList.SelectedItem is not ReminderItem item)return;
        item.Done=true;
        RemindersList.Items.Refresh();
        SafeSave();
        RefreshDashboard();
    }
    private void DeleteReminder_Click(object sender,RoutedEventArgs e)
    {
        if(RemindersList.SelectedItem is not ReminderItem item)return;
        data.Reminders.Remove(item);
        RemindersList.Items.Refresh();
        SafeSave();
        RefreshDashboard();
    }
    private void NewSnippet_Click(object sender, RoutedEventArgs e)
    {
        var entry = new SnippetItem(); data.Snippets.Add(entry); SnippetsList.Items.Refresh(); SnippetsList.SelectedItem = entry; SafeSave();
    }
    private void DeleteSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (SnippetsList.SelectedItem is SnippetItem item) { data.Snippets.Remove(item); SnippetsList.Items.Refresh(); SafeSave(); }
    }
    private void SnippetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        loadingEditor = true;
        var item = SnippetsList.SelectedItem as SnippetItem;
        SnippetName.Text = item?.Name ?? ""; SnippetContent.Text = item?.Text ?? "";
        SnippetName.IsEnabled = SnippetContent.IsEnabled = item != null;
        loadingEditor = false;
    }
    private void SnippetEditor_Changed(object sender, TextChangedEventArgs e)
    {
        if (loadingEditor || SnippetsList.SelectedItem is not SnippetItem item) return;
        item.Name = SnippetName.Text; item.Text = SnippetContent.Text; SnippetsList.Items.Refresh(); QueueSave();
    }
    private void CopySnippet_Click(object sender, RoutedEventArgs e)
    {
        if (SnippetsList.SelectedItem is not SnippetItem item) return;
        System.Windows.Clipboard.SetText(item.Text);
        StatusText.Text = "Copied text. Paste with Ctrl+V in your target application.";
    }
    private void ImportDocument_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Select document", Filter = "All files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            string extension = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (new [] { ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".com" }.Contains(extension))
                throw new InvalidOperationException("Executable or script files cannot be imported as documents.");
            var relative = store.ImportAttachment(dlg.FileName);
            data.Documents.Add(new DocumentItem { Name = Path.GetFileName(dlg.FileName), StoredFilename = relative });
            DocumentsList.Items.Refresh(); SafeSave(); RefreshDashboard();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void OpenDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsList.SelectedItem is DocumentItem item)
        {
            try { Process.Start(new ProcessStartInfo(store.AttachmentPath(item.StoredFilename)) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError(ex); }
        }
    }
    private void CopyDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsList.SelectedItem is DocumentItem item)
        {
            var collection = new StringCollection { store.AttachmentPath(item.StoredFilename) };
            try { System.Windows.Clipboard.SetFileDropList(collection); StatusText.Text = "File copied. Paste into a compatible application or folder."; }
            catch (Exception ex) { ShowError(ex); }
        }
    }
    private void DeleteDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsList.SelectedItem is not DocumentItem item) return;
        if (MessageBox.Show("Remove this document from the workspace?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        data.Documents.Remove(item); DocumentsList.Items.Refresh(); SafeSave(); RefreshDashboard();
        // Attachment is retained for recovery; no unprompted permanent deletion.
    }
    private string? Prompt(string title, string defaultText)
    {
        var dlg = new Window { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title, Width = 360, Height = 150, ResizeMode = ResizeMode.NoResize };
        var root = new StackPanel { Margin = new Thickness(12) };
        var field = new TextBox { Text = defaultText, Margin = new Thickness(0, 0, 0, 12) };
        var okay = new Button { Content = "Save", Width = 85, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, IsDefault = true };
        okay.Click += (_, _) => dlg.DialogResult = true;
        root.Children.Add(field); root.Children.Add(okay); dlg.Content = root;
        field.SelectAll(); field.Focus();
        return dlg.ShowDialog() == true ? field.Text.Trim() : null;
    }
    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt("New information tab", "My Excel data");
        if (string.IsNullOrWhiteSpace(name)) return;
        SaveGridInternal();
        var entry = new DataTab { Name = name };
        data.DataTabs.Add(entry); DataTabList.Items.Refresh(); DataTabList.SelectedItem = entry;
        MainTabs.SelectedItem = DataTabPage; SafeSave(); RefreshDashboard();
    }
    private void RenameTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataTabList.SelectedItem is not DataTab tab) return;
        var name = Prompt("Rename tab", tab.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        tab.Name = name; DataTabList.Items.Refresh(); RefreshGridMessage(); SafeSave();
    }
    private void DeleteTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataTabList.SelectedItem is not DataTab tab) return;
        if (MessageBox.Show($"Remove tab '{tab.Name}'? Attached sources are retained for recovery.", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        if (ReferenceEquals(activeTab, tab)) { activeTab = null; table = null; GridEditor.ItemsSource = null; }
        data.DataTabs.Remove(tab); DataTabList.Items.Refresh(); SafeSave(); RefreshDashboard();
    }
    private void DataTabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        GridEditor.CommitEdit(DataGridEditingUnit.Cell, true);
        GridEditor.CommitEdit(DataGridEditingUnit.Row, true);
        SaveGridInternal();
        activeTab = DataTabList.SelectedItem as DataTab;
        ShowGrid();
    }
    private void ShowGrid()
    {
        table = null;
        GridEditor.ItemsSource = null;
        if (activeTab is null) { DataTabTitle.Text = "Create or select a tab"; GridWarning.Text = ""; return; }
        DataTabTitle.Text = activeTab.Name;
        var next = new DataTable("cells");
        int columns = Math.Max(12, activeTab.Cells.Count == 0 ? 12 : activeTab.Cells.Max(x => x.Count));
        columns = Math.Min(256, columns);
        for (int c = 0; c < columns; c++) next.Columns.Add(MatrixCodec.ExcelColumn(c), typeof(string));
        int rows = Math.Max(30, activeTab.Cells.Count);
        for (int r = 0; r < rows; r++)
        {
            var row = next.NewRow();
            if (r < activeTab.Cells.Count)
                for (int c = 0; c < Math.Min(columns, activeTab.Cells[r].Count); c++) row[c] = activeTab.Cells[r][c];
            next.Rows.Add(row);
        }
        table = next; GridEditor.ItemsSource = table.DefaultView;
        RefreshGridMessage();
    }
    private void RefreshGridMessage()
    {
        if (activeTab is null) return;
        GridWarning.Text = activeTab.OcrNeedsReview
            ? "OCR DRAFT: screenshot text and cell positions are NOT verified. Compare with the saved original image and correct every cell before using paste."
            : activeTab.ExcelSnapshotDirty && activeTab.ExcelSnapshot is not null
                ? "Excel snapshot is older than edited grid. Copy the edited grid for values, or capture a fresh Excel snapshot for rich paste."
                : "To paste into Excel: select the target top-left cell, then use 'Copy cells for Excel' and Ctrl+V. Rich Excel snapshots require desktop Excel.";
    }
    private void GridEditor_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is DataGridTextColumn column) { column.Width = new DataGridLength(130); column.MinWidth = 80; }
    }
    private void GridEditor_LoadingRow(object sender, DataGridRowEventArgs e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    private void GridEditor_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        var editedTab = activeTab;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (editedTab is not null && ReferenceEquals(editedTab, activeTab))
            { editedTab.ExcelSnapshotDirty = true; SaveGridInternal(); RefreshGridMessage(); QueueSave(); }
        }), DispatcherPriority.Background);
    }
    private void SaveGridInternal()
    {
        if (activeTab is null || table is null) return;
        var cells = new List<List<string>>();
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            cells.Add(row.ItemArray.Select(x => x is DBNull ? "" : Convert.ToString(x) ?? "").ToList());
        }
        while (cells.Count > 0 && cells[^1].All(string.IsNullOrEmpty)) cells.RemoveAt(cells.Count - 1);
        int rightmost = 0;
        foreach (var row in cells)
            for (int i = row.Count - 1; i >= 0; i--)
                if (!string.IsNullOrEmpty(row[i])) { rightmost = Math.Max(rightmost, i + 1); break; }
        foreach (var row in cells) if (row.Count > rightmost) row.RemoveRange(rightmost, row.Count - rightmost);
        activeTab.Cells = cells;
    }
    private void SaveGrid_Click(object sender, RoutedEventArgs e) { GridEditor.CommitEdit(DataGridEditingUnit.Cell, true); GridEditor.CommitEdit(DataGridEditingUnit.Row, true); SaveGridInternal(); SafeSave(); }
    private void AddGridRows_Click(object sender, RoutedEventArgs e)
    {
        if (table is null) return;
        for (int i = 0; i < 20; i++) table.Rows.Add(table.NewRow());
        StatusText.Text = "Added 20 editable rows";
    }
    private (int row, int col) GridStart()
    {
        int r = Math.Max(0, GridEditor.Items.IndexOf(GridEditor.CurrentItem));
        int c = Math.Max(0, GridEditor.CurrentCell.Column?.DisplayIndex ?? 0);
        return (r, c);
    }
    private void ImportClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null || table is null) return;
        if (!System.Windows.Clipboard.ContainsText()) { MessageBox.Show("Copy an Excel range or tabular text first."); return; }
        string text = System.Windows.Clipboard.GetText().TrimEnd('\0');
        if (text.Length == 0) return;
        var parsed = MatrixCodec.Parse(text);
        if (parsed.Count == 0) return;
        if (parsed.Count > 5000 || parsed.Max(x => x.Count) > 256) { MessageBox.Show("Max 5,000 rows and 256 columns per paste."); return; }
        GridEditor.CommitEdit(DataGridEditingUnit.Cell, true); GridEditor.CommitEdit(DataGridEditingUnit.Row, true);
        SaveGridInternal();
        var (row, col) = GridStart();
        MatrixCodec.Paste(activeTab.Cells, parsed, row, col);
        activeTab.ExcelSnapshotDirty = true;
        ShowGrid(); SafeSave();
        StatusText.Text = $"Imported {parsed.Count} rows at {MatrixCodec.ExcelColumn(col)}{row + 1}. Review the cells.";
    }
    private void CopyGrid_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null || table is null) return;
        GridEditor.CommitEdit(DataGridEditingUnit.Cell, true); GridEditor.CommitEdit(DataGridEditingUnit.Row, true);
        SaveGridInternal();
        if (activeTab.Cells.Count == 0) { MessageBox.Show("This tab has no cell data."); return; }
        if (activeTab.OcrNeedsReview && MessageBox.Show("OCR data is not verified. Copy anyway?", "Unverified OCR", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        System.Windows.Clipboard.SetText(MatrixCodec.Serialize(activeTab.Cells));
        StatusText.Text = "Grid copied as Excel-compatible rows/columns. Select target cell in Excel and press Ctrl+V.";
    }
    private void PasteGridToExcel_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null || table is null) return;
        GridEditor.CommitEdit(DataGridEditingUnit.Cell, true); GridEditor.CommitEdit(DataGridEditingUnit.Row, true);
        SaveGridInternal();
        if (activeTab.Cells.Count == 0) { MessageBox.Show("Grid is empty."); return; }
        if (activeTab.OcrNeedsReview) { MessageBox.Show("OCR has not been reviewed. Compare every cell to the original and mark reviewed before pasting."); return; }
        try
        {
            var pos = ExcelInterop.ActivePosition();
            int rows = activeTab.Cells.Count, cols = activeTab.Cells.Max(x => x.Count);
            if (MessageBox.Show($"Paste {rows} × {cols} TEXT cells starting at Excel {pos.Address}?\n\nThis overwrites destination values and sets their format to Text. It does NOT restore fonts, borders, formulas, or merges. Confirm the target selection is correct.",
                "Confirm Excel text paste", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            ExcelInterop.PasteCellsAsText(activeTab.Cells);
            StatusText.Text = $"Pasted {rows} rows and {cols} columns to selected Excel range as text.";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void CaptureExcel_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null) return;
        try
        {
            var snapshotName = store.ImportAttachmentPathForNew(".xlsx");
            var capture = ExcelInterop.Capture(store.AttachmentPath(snapshotName));
            activeTab.Cells = capture.Cells;
            activeTab.ExcelSnapshot = snapshotName;
            activeTab.ExcelSourceRow = capture.Row; activeTab.ExcelSourceCol = capture.Column;
            activeTab.ExcelRows = capture.Rows; activeTab.ExcelColumns = capture.Columns;
            activeTab.ExcelSnapshotDirty = false;
            activeTab.OcrNeedsReview = false;
            ShowGrid(); SafeSave();
            StatusText.Text = $"Saved Excel snapshot: {capture.Rows} × {capture.Columns} cells. Source workbook unchanged.";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void PasteExcel_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab?.ExcelSnapshot is null) { MessageBox.Show("First capture a range from desktop Excel."); return; }
        if (activeTab.OcrNeedsReview) { MessageBox.Show("Verify the OCR grid before pasting. This command pastes the captured Excel snapshot, not OCR edits."); return; }
        if (activeTab.ExcelSnapshotDirty) { MessageBox.Show("You edited cells since the Excel snapshot was captured. This command would paste the OLD snapshot. Copy the edited grid to Excel instead, or capture a fresh Excel snapshot."); return; }
        try
        {
            var pos = ExcelInterop.ActivePosition();
            if (MessageBox.Show($"Paste {activeTab.ExcelRows} rows × {activeTab.ExcelColumns} columns into Excel starting at {pos.Address}?\n\nThis overwrites destination cells, and may change their widths/heights. Formulas, merges and formatting are copied using Excel's native mechanism; verify workbook-specific links.",
                "Confirm Excel paste", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            ExcelInterop.PasteRich(activeTab, store.AttachmentPath(activeTab.ExcelSnapshot));
            StatusText.Text = "Pasted Excel snapshot to current selection. Review formulas and formatting before saving workbook.";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void ImportImage_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null) return;
        var dlg = new OpenFileDialog { Title = "Choose screenshot of spreadsheet/table", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|All files|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            activeTab.SourceImage = store.ImportAttachment(dlg.FileName);
            activeTab.OcrNeedsReview = true;
            activeTab.ExcelSnapshotDirty = true;
            RefreshGridMessage(); SafeSave();
            StatusText.Text = "Original screenshot saved. Run OCR to create a draft or enter/edit grid manually.";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void ViewImage_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab?.SourceImage is null) { MessageBox.Show("No screenshot attached to this tab."); return; }
        try
        {
            var image = new System.Windows.Controls.Image { Source = new BitmapImage(new Uri(store.AttachmentPath(activeTab.SourceImage))), Stretch = Stretch.Uniform };
            new Window { Owner = this, Title = "Original screenshot — compare each cell", Content = new ScrollViewer { Content = image }, Width = 900, Height = 650 }.Show();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async void RunOcr_Click(object sender, RoutedEventArgs e)
    {
        var tab = activeTab;
        if (tab?.SourceImage is null) { MessageBox.Show("Import a screenshot first."); return; }
        try
        {
            StatusText.Text = "Running optional local OCR. Original screenshot remains saved.";
            var draft = await ImageOcr.RecognizeDraftAsync(store.AttachmentPath(tab.SourceImage));
            if (MessageBox.Show($"OCR returned {draft.Count} approximate lines. These are NOT verified cell positions. Replace current grid with editable draft?", "OCR draft", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            tab.Cells = draft; tab.OcrNeedsReview = true; tab.ExcelSnapshotDirty = true;
            if (ReferenceEquals(tab, activeTab)) ShowGrid(); SafeSave();
            StatusText.Text = "OCR draft loaded. Compare against original screenshot and correct before marking reviewed.";
        }
        catch (System.ComponentModel.Win32Exception) { MessageBox.Show("Tesseract OCR is not installed or not on PATH. Screenshot is safely retained. Install local Tesseract and retry, or enter cells manually."); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void ReviewGrid_Click(object sender, RoutedEventArgs e)
    {
        if (activeTab is null) return;
        GridEditor.CommitEdit(DataGridEditingUnit.Cell, true);
        GridEditor.CommitEdit(DataGridEditingUnit.Row, true);
        SaveGridInternal();
        if (MessageBox.Show("Have you compared EVERY cell and blank position with the source screenshot? This marks the current grid as reviewed, not independently certified.", "Confirm manual review", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        activeTab.OcrNeedsReview = false; RefreshGridMessage(); DataTabList.Items.Refresh(); SafeSave();
    }
    private void FloatCalendar_Click(object sender, RoutedEventArgs e)
    {
        new FloatingPlannerWindow("calendar", data, () => { RefreshEvents(); RefreshDashboard(); SafeSave(); }).Show();
    }
    private void FloatReminders_Click(object sender, RoutedEventArgs e)
    {
        new FloatingPlannerWindow("reminders", data, () => { RemindersList.Items.Refresh(); RefreshDashboard(); SafeSave(); }).Show();
    }
    private void FloatClock_Click(object sender, RoutedEventArgs e)
    {
        new FloatingPlannerWindow("clock", data, () => { }).Show();
    }
    private void GoTabs_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedItem = DataTabPage;
    private void StartTimer_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimerMinutes.Text, out var minutes) || minutes < 1 || minutes > 1440) { MessageBox.Show("Choose 1–1440 minutes."); return; }
        timerDeadline = DateTime.Now.AddMinutes(minutes); Tick();
    }
    private void StopTimer_Click(object sender, RoutedEventArgs e) { timerDeadline = null; TimerRemaining.Text = "Stopped"; }
    private void ShowError(Exception ex) => MessageBox.Show(ex.Message, "DeskSpace", MessageBoxButton.OK, MessageBoxImage.Error);
}
