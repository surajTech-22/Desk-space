$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'MainWindow.xaml'
if(!(Test-Path -LiteralPath $file)){throw 'R351 missing shipping Workspace XAML'}
$s=[IO.File]::ReadAllText($file)
$lf=[string][char]10;$s=$s.Replace(([string][char]13+$lf),$lf)
function One([string]$old,[string]$new,[string]$reason){
    $old=$old.Replace(([string][char]13+$lf),$lf)
    $new=$new.Replace(([string][char]13+$lf),$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R351 $reason must appear exactly once, found $count"}
    $script:s=$script:s.Replace($old,$new)
}
# Root cause: the R349 dark-selected TabItem Foreground was inherited by
# every child page, yielding white Settings labels on a white page surface.
# Keep selected navigation dark-on-pale and all content text readable.
One @'
              <Setter Property="Foreground" Value="#FFFFFF"/>
              <Setter Property="Background" Value="#2C6795"/>
'@ @'
              <Setter Property="Foreground" Value="#123755"/>
              <Setter Property="Background" Value="#BFDFF4"/>
'@ 'selected navigation foreground inheritance'
One @'
      <TabItem Tag="Settings" Header="⚙  Settings">
        <ScrollViewer><StackPanel Margin="24">
'@ @'
      <TabItem Tag="Settings" Header="⚙  Settings">
        <ScrollViewer VerticalScrollBarVisibility="Auto" Background="#F8FBFF">
          <StackPanel Margin="26" TextElement.Foreground="#243F5C">
'@ 'Settings scroll background and inherited text contrast'
One '<TextBlock Text="Settings" FontSize="28" FontWeight="Bold" Margin="0,0,0,14"/>' '<TextBlock Text="Settings" FontSize="28" FontWeight="Bold" Foreground="#1B3A57" Margin="0,0,0,14"/>' 'Settings page header'
One '<TextBlock x:Name="SettingsVersionText" FontSize="16" Margin="0,0,0,18"/>' '<TextBlock x:Name="SettingsVersionText" FontSize="16" Foreground="#496982" Margin="0,0,0,18"/>' 'installed version readability'
One '<TextBlock Text="Update channel" FontSize="16" FontWeight="SemiBold" Margin="0,14,0,7"/>' '<TextBlock Text="Update channel" Foreground="#1B3A57" FontSize="16" FontWeight="SemiBold" Margin="0,14,0,7"/>' 'channel label'
One @'
          <TextBlock Text="Stable receives approved releases only. Tester receives separate opt-in prereleases and automatic updates every five minutes. Switching channels preserves your workspace data and does not downgrade your installation." TextWrapping="Wrap" Foreground="#A2B3D0" Margin="0,8,0,0"/>
'@ @'
          <TextBlock Text="Stable receives approved releases only. Tester receives separate opt-in prereleases and automatic updates every five minutes. Switching channels preserves your workspace data and does not downgrade your installation."
                     TextWrapping="Wrap" Foreground="#41627F" Margin="0,12,0,0"/>
          <TextBlock Text="Privacy: local workspace files are not encrypted. Do not store passwords here."
                     TextWrapping="Wrap" Foreground="#526D87" FontSize="12"
                     Margin="0,24,0,0"/>
'@ 'readable explanatory notes and retained privacy warning'
One 'Text="Ready — local data is not encrypted in this preview"' 'Text="Ready — Workspace loaded"' 'modern status bar'
# Explicitly set Settings ComboBox text/background so its channels remain
# legible even with Windows theme differences.
One @'
          <ComboBox x:Name="UpdateChannelPickerV104" Width="260" HorizontalAlignment="Left" Height="34"
'@ @'
          <ComboBox x:Name="UpdateChannelPickerV104" Width="280" HorizontalAlignment="Left" Height="38"
                    Background="#FFFFFF" Foreground="#203D5A" BorderBrush="#7399BC"
'@ 'update channel picker contrast'
[xml]$parsed=$s
if($parsed.DocumentElement.LocalName -ne 'Window'){throw 'R351 invalid WPF document'}
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R351: actual shipping Settings text is dark on light, selected tab inherits dark text, privacy notice preserved.'
