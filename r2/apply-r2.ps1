$ErrorActionPreference = "Stop"
function Rewrite([string]$path, [string]$before, [string]$after) {
    if (!(Test-Path -LiteralPath $path)) { throw "R2 target missing: $path" }
    $raw = [System.IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    if (!$raw.Contains($before)) { throw "R2 expected pattern missing in $path : $before" }
    $raw = $raw.Replace($before, $after)
    [System.IO.File]::WriteAllText($path, $raw, [System.Text.UTF8Encoding]::new($false))
}
$app = "DeskSpace_Windows_R1/DeskSpace/MainWindow.xaml.cs"
Rewrite $app '    private readonly System.Windows.Forms.NotifyIcon tray;' @'
    private readonly System.Windows.Forms.NotifyIcon tray;
    private FloatingDashboardWindow? desktopWidget;
'@
Rewrite $app '        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });' @'
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show DeskSpace", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        trayMenu.Items.Add("Show Floating Widget", null, (_, _) => Dispatcher.Invoke(ShowFloatingWidget));
        trayMenu.Items.Add("GitHub Builds", null, (_, _) => Dispatcher.Invoke(OpenGitHubBuilds));
        tray.ContextMenuStrip = trayMenu;
'@
Rewrite $app '        Tick();' @'
        Tick();
        if (WidgetPreferences.Load().ShowAtLaunch)
            Dispatcher.BeginInvoke(new Action(ShowFloatingWidget), DispatcherPriority.ApplicationIdle);
'@
Rewrite $app '    private void SafeSave()' @'
    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void ShowFloatingWidget()
    {
        if (desktopWidget is { IsVisible: true })
        {
            desktopWidget.Activate();
            return;
        }
        desktopWidget = new FloatingDashboardWindow(data, SafeSave,
            () =>
            {
                var note = new NoteItem { Title = "New note" };
                data.Notes.Add(note);
                NotesList.Items.Refresh();
                NotesList.SelectedItem = note;
                SafeSave();
                RefreshDashboard();
            },
            ShowMainWindow,
            () => new FloatingPlannerWindow("calendar", data, () => { RefreshEvents(); RefreshDashboard(); SafeSave(); }).Show(),
            () => new FloatingPlannerWindow("reminders", data, () => { RemindersList.Items.Refresh(); RefreshDashboard(); SafeSave(); }).Show(),
            () => new FloatingPlannerWindow("clock", data, () => { }).Show(),
            OpenGitHubBuilds);
        desktopWidget.Closed += (_, _) => desktopWidget = null;
        desktopWidget.Show();
    }
    private void ShowWidget_Click(object sender, RoutedEventArgs e) => ShowFloatingWidget();
    private void GitHubUpdates_Click(object sender, RoutedEventArgs e) => OpenGitHubBuilds();
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
    private void SafeSave()
'@
Rewrite $app '        saveTimer.Stop(); SaveGridInternal(); SafeSave(); tray.Dispose();' '        desktopWidget?.Close(); saveTimer.Stop(); SaveGridInternal(); SafeSave(); tray.Dispose();'
Rewrite $app 'var window = new FloatingNoteWindow(note, () => { NotesList.Items.Refresh(); SafeSave(); }) { Owner = this };' 'var window = new FloatingNoteWindow(note, () => { NotesList.Items.Refresh(); SafeSave(); });'
Rewrite $app '        window.Show();' @'
        window.Show();
        desktopWidget?.ReloadNotes(note);
'@
Rewrite $app 'data.Notes.Remove(item); NotesList.Items.Refresh(); SafeSave(); RefreshDashboard();' 'data.Notes.Remove(item); NotesList.Items.Refresh(); desktopWidget?.ReloadNotes(); SafeSave(); RefreshDashboard();'
Rewrite $app ') { Owner = this }.Show();' ').Show();'
$xaml = "DeskSpace_Windows_R1/DeskSpace/MainWindow.xaml"
Rewrite $xaml 'Title="DeskSpace — Windows workspace preview"' 'Title="DeskSpace — Windows workspace R2"'
Rewrite $xaml '            <Button Content="Open Floating Note"' @'
            <Button Content="Show Desktop Widget" Click="ShowWidget_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="Check GitHub Builds" Click="GitHubUpdates_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="Open Floating Note"
'@
$iss = "installer/DeskSpace.iss"
Rewrite $iss '#define AppVersion "1.0.0-r1"' '#define AppVersion "1.1.0"'
Rewrite $iss 'AppVerName=DeskSpace R1' 'AppVerName=DeskSpace R2'
Write-Host "R2 widget code and installer version applied."
