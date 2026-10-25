$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'

# R103 first migrates capture data; R104 replaces only preview and updater.
foreach($pair in @(
  @('r104/DeskSpaceExcelSheetViewV104.cs',"$r/DeskSpaceExcelSheetViewV101.cs"),
  @('r104/DeskSpacePublicUpdaterV104.cs',"$r/DeskSpacePublicUpdaterV44.cs")
)){
 if(!(Test-Path -LiteralPath $pair[0])){throw "R104 missing $($pair[0])"}
 Copy-Item -LiteralPath $pair[0] -Destination $pair[1] -Force
}
function Swap([string]$file,[string]$before,[string]$after,[string]$label)
{
 $nl=[string][char]13+[string][char]10
 $raw=[IO.File]::ReadAllText($file).Replace($nl,[string][char]10)
 $before=$before.Replace($nl,[string][char]10)
 $after=$after.Replace($nl,[string][char]10)
 $matches=([regex]::Matches($raw,[regex]::Escape($before))).Count
 if($matches -ne 1){throw "R104 $label: expected one integration anchor, got $matches"}
 [IO.File]::WriteAllText($file,$raw.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
$xaml="$r/MainWindow.xaml"
Swap $xaml @'
          <TextBlock Text="Automatic updates enabled: public stable GitHub Releases, SHA-256 verified, no sign-in required. Check for Updates also works manually." TextWrapping="Wrap" Foreground="#536693" Margin="0,14,0,0"/>
'@ @'
          <TextBlock Text="Update channel" FontSize="16" FontWeight="SemiBold" Margin="0,14,0,7"/>
          <ComboBox x:Name="UpdateChannelPickerV104" Width="260" HorizontalAlignment="Left" Height="34"
                    Loaded="UpdateChannel_LoadedV104" SelectionChanged="UpdateChannel_ChangedV104">
            <ComboBoxItem Tag="stable" Content="Stable — regular releases"/>
            <ComboBoxItem Tag="tester" Content="Tester — early builds"/>
          </ComboBox>
          <TextBlock Text="Stable receives approved releases only. Tester receives separate opt-in prereleases and automatic updates every five minutes. Switching channels preserves your workspace data and does not downgrade your installation." TextWrapping="Wrap" Foreground="#536693" Margin="0,8,0,0"/>
'@ 'add channel control to Workspace Settings'
$main="$r/MainWindow.xaml.cs"
Swap $main @'
    private void SafeSave()
'@ @'
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
'@ 'wire Settings selector to updater'
Write-Host 'R104 applied: responsive readable Excel preview and independent tester updates.'
