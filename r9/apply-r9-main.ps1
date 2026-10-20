$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$xaml = 'DeskSpace_Windows_R1/DeskSpace/MainWindow.xaml'
$main = [IO.File]::ReadAllText($xaml).Replace("`r`n","`n")
function EditMain([string]$before,[string]$after) {
    $before=$before.Replace("`r`n","`n")
    $after=$after.Replace("`r`n","`n")
    if (!$script:main.Contains($before)) { throw "R9 main XAML anchor missing: $before" }
    $script:main = $script:main.Replace($before,$after)
}
EditMain 'Title="DeskSpace — Windows workspace preview"' 'Title="DeskSpace — Workspace"'
EditMain 'WINDOWS WORKSPACE  ·  LOCAL PREVIEW · DO NOT STORE PASSWORDS' 'YOUR WORKSPACE  ·  PRIVATE LOCAL DATA  ·  NO PASSWORD VAULT YET'
EditMain '<TabControl x:Name="MainTabs" Margin="10" Background="White">' @'
<TabControl x:Name="MainTabs" Margin="10" Background="White" TabStripPlacement="Left">
      <TabControl.ItemContainerStyle>
        <Style TargetType="TabItem">
          <Setter Property="MinWidth" Value="174"/>
          <Setter Property="Padding" Value="12,11"/>
          <Setter Property="FontSize" Value="14"/>
          <Setter Property="Foreground" Value="#27394F"/>
        </Style>
      </TabControl.ItemContainerStyle>
'@
$headers=@{
'<TabItem Header="Dashboard">'='<TabItem Tag="Home" Header="⌂  Home">'
'<TabItem Header="Sticky Notes">'='<TabItem Tag="Notes" Header="✎  Notes">'
'<TabItem Header="Calendar">'='<TabItem Tag="Calendar" Header="▦  Calendar">'
'<TabItem Header="Reminders">'='<TabItem Tag="Reminders" Header="◉  Reminders">'
'<TabItem Header="Quick Copy">'='<TabItem Tag="Clipboard" Header="▣  Clipboard">'
'<TabItem Header="Documents">'='<TabItem Tag="Documents" Header="▤  Documents">'
'<TabItem x:Name="DataTabPage" Header="Custom Data Tabs">'='<TabItem x:Name="DataTabPage" Tag="Custom Data" Header="▥  Custom Data">'
'<TabItem Header="Clock &amp; Timer">'='<TabItem Tag="Clock" Header="◷  Clock &amp; Timer">'
}
foreach($entry in $headers.GetEnumerator()){ EditMain $entry.Key $entry.Value }
$actions=@{
'Content="Add Sticky Note"'='Content="✎  Add Sticky Note"'
'Content="Open Floating Note"'='Content="✎  Open Floating Note"'
'Content="Floating Calendar"'='Content="▦  Floating Calendar"'
'Content="Floating Reminders"'='Content="◉  Floating Reminders"'
'Content="Floating Clock"'='Content="◷  Floating Clock"'
'Content="Create Data Tab"'='Content="▥  Create Data Tab"'
'Content="View Data Tabs"'='Content="▥  View Data Tabs"'
}
foreach($entry in $actions.GetEnumerator()){ EditMain $entry.Key $entry.Value }
EditMain ('<Button Content="▥  View Data Tabs" Click="GoTabs_Click" Padding="16,10" Margin="0,0,10,10"/>'+[Environment]::NewLine+'          </WrapPanel>') @'
            <Button Content="▥  View Data Tabs" Click="GoTabs_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="⌕  Universal Search" Click="UniversalSearch_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="▤  Recent Files" Click="RecentFiles_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="▣  Pinned Apps" Click="PinnedApps_Click" Padding="16,10" Margin="0,0,10,10"/>
            <Button Content="⚙  Widget Settings" Click="WidgetSettings_Click" Padding="16,10" Margin="0,0,10,10"/>
          </WrapPanel>
'@
# Replace the general "prototype" paragraph with factually accurate feature availability.
EditMain 'Everything here is a local Windows-first prototype. Password vault, Winman import, sync, Android app, and Windows 11 Widgets-board provider are not included in this package.' 'Available today: notes, calendar, reminders, clock, quick copy, documents, custom data, recent files, pinned apps and universal search. Password encryption, Winman integration, Android, and cloud sync are not included; do not store passwords.'
# The clock page receives actual weather info from the shared weather service.
EditMain '<TextBlock Text="Local clock" FontSize="16" Margin="0,0,0,22"/>' @'
          <TextBlock x:Name="ClockWeather" Text="Weather: select city in widget settings" FontSize="17" Foreground="#536693" Margin="0,0,0,22"/>
'@
$additional=@'
      <TabItem Tag="Tools" Header="⚒  Tools">
        <ScrollViewer><StackPanel Margin="24">
          <TextBlock Text="Tools and Quick Access" FontSize="27" FontWeight="Bold" Margin="0,0,0,16"/>
          <TextBlock Text="Use the same live tools available from the desktop donut." FontSize="15" Margin="0,0,0,18"/>
          <WrapPanel>
            <Button Content="⌕  Search this computer" Click="UniversalSearch_Click" Padding="18,12" Margin="0,0,12,12"/>
            <Button Content="▤  Recent generated files" Click="RecentFiles_Click" Padding="18,12" Margin="0,0,12,12"/>
            <Button Content="▣  Pin and launch apps" Click="PinnedApps_Click" Padding="18,12" Margin="0,0,12,12"/>
            <Button Content="⚙  Donut options" Click="WidgetSettings_Click" Padding="18,12" Margin="0,0,12,12"/>
            <Button Content="◷  Show donut" Click="ShowWidget_Click" Padding="18,12" Margin="0,0,12,12"/>
          </WrapPanel>
          <TextBlock Text="Search supports accessible files, installed shortcuts, Windows Settings, and DeskSpace content. Indexing every drive and every application is not yet implemented." TextWrapping="Wrap" Foreground="#637286" Margin="0,24,0,0"/>
        </StackPanel></ScrollViewer>
      </TabItem>
      <TabItem Tag="Modules" Header="▦  Modules">
        <ScrollViewer><StackPanel Margin="24">
          <TextBlock Text="Modules" FontSize="28" FontWeight="Bold" Margin="0,0,0,16"/>
          <TextBlock Text="Open an existing working module. More integrations will be introduced separately." TextWrapping="Wrap" Margin="0,0,0,16"/>
          <WrapPanel>
            <Button Tag="Notes" Content="✎  Notes" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Calendar" Content="▦  Calendar" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Reminders" Content="◉  Reminders" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Clipboard" Content="▣  Clipboard" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Documents" Content="▤  Documents" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Custom Data" Content="▥  Custom Data" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Clock" Content="◷  Clock &amp; Timer" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
            <Button Tag="Tools" Content="⚒  Tools &amp; Search" Click="ModuleNavigate_Click" Padding="20,15" Margin="0,0,10,12"/>
          </WrapPanel>
          <TextBlock Text="Not yet implemented: encrypted password vault, native Windows autofill, Winman import, Android app, cloud synchronization." TextWrapping="Wrap" Foreground="#7E4C24" Margin="0,22,0,0"/>
        </StackPanel></ScrollViewer>
      </TabItem>
      <TabItem Tag="Settings" Header="⚙  Settings">
        <ScrollViewer><StackPanel Margin="24">
          <TextBlock Text="Settings" FontSize="28" FontWeight="Bold" Margin="0,0,0,14"/>
          <TextBlock x:Name="SettingsVersionText" FontSize="16" Margin="0,0,0,18"/>
          <WrapPanel>
            <Button Content="⚙  Widget &amp; Weather" Click="WidgetSettings_Click" Padding="18,11" Margin="0,0,10,10"/>
            <Button Content="◷  Show Widget" Click="ShowWidget_Click" Padding="18,11" Margin="0,0,10,10"/>
            <Button Content="↻  Check for Updates" Click="CheckForUpdates_Click" Padding="18,11" Margin="0,0,10,10"/>
          </WrapPanel>
          <TextBlock Text="Automatic updates use your authorized private GitHub releases; no password or GitHub token is stored in DeskSpace." TextWrapping="Wrap" Foreground="#536693" Margin="0,14,0,0"/>
        </StackPanel></ScrollViewer>
      </TabItem>
'@
if (!$main.Contains('    </TabControl>')) {throw 'Cannot insert R9 module navigation'}
$main=$main.Replace('    </TabControl>', $additional + '    </TabControl>')
[IO.File]::WriteAllText($xaml,$main,[Text.UTF8Encoding]::new($false))
$source='DeskSpace_Windows_R1/DeskSpace/MainWindow.xaml.cs'
$cs=[IO.File]::ReadAllText($source).Replace("`r`n","`n")
$anchor='    private void SafeSave()'
if(!$cs.Contains($anchor)){throw 'R9 MainWindow event anchor missing'}
$handlers=@'
    private void UniversalSearch_Click(object sender,RoutedEventArgs e)
    {
        new UniversalSearchWindow(() => data, WidgetSettingsV3.Load()).Show();
    }
    private void RecentFiles_Click(object sender,RoutedEventArgs e)
    {
        var service = new RecentFilesService();
        service.Start();
        var window = new RecentFilesWindow(service);
        window.Closed += (_,_) => service.Dispose();
        window.Show();
    }
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        new AppPinsWindow(WidgetSettingsV3.Load()).Show();
    }
    private void WidgetSettings_Click(object sender,RoutedEventArgs e)
    {
        var settings=WidgetSettingsV3.Load();
        new WidgetOptionsWindow(settings,() => { settings.Save();settings.ApplyWindowsStartup(); }).Show();
    }
    private void ShowWidget_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.EnsureRunning(force:true);
    }
    private async void CheckForUpdates_Click(object sender,RoutedEventArgs e)
    {
        await DeskSpaceUpdater.CheckAsync(this);
    }
    private void ModuleNavigate_Click(object sender,RoutedEventArgs e)
    {
        if(sender is Button button && button.Tag is string id)
        {
            foreach(var tab in MainTabs.Items.OfType<TabItem>())
                if(string.Equals(tab.Tag as string,id,StringComparison.OrdinalIgnoreCase))
                { MainTabs.SelectedItem=tab; break; }
        }
    }
'@
$cs=$cs.Replace($anchor,$handlers + $anchor)
$needle='        VersionButton.Content = "v" + DeskSpaceUpdater.CurrentVersion;'
if(!$cs.Contains($needle)){throw 'R8 main-version control not found'}
$cs=$cs.Replace($needle,$needle+[Environment]::NewLine+
'        SettingsVersionText.Text = "DeskSpace version " + DeskSpaceUpdater.CurrentVersion;')
$needle='        ClockText.Text = DateTime.Now.ToString("dddd, dd MMM yyyy\nHH:mm:ss", CultureInfo.CurrentCulture);'
if(!$cs.Contains($needle)){throw 'R9 original clock tick not found'}
$cs=$cs.Replace($needle,$needle+[Environment]::NewLine+@'
        var local=WidgetSettingsV3.Load();
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
'@)
[IO.File]::WriteAllText($source,$cs,[Text.UTF8Encoding]::new($false))
Write-Host 'R9 main module navigation, labels, real module links, tools, settings and live clock conditions applied.'
