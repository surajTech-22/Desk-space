$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$window=Join-Path $root 'MainWindow.xaml'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
foreach($path in @($window,$palette)) {
    if(!(Test-Path -LiteralPath $path)){throw ('R349 missing actual shipping source '+$path)}
}
$lf=[string][char]10
$crlf=[string][char]13+$lf
function One([string]$path,[string]$old,[string]$replacement,[string]$name) {
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $old=$old.Replace($crlf,$lf)
    $replacement=$replacement.Replace($crlf,$lf)
    $matches=([regex]::Matches($s,[regex]::Escape($old))).Count
    if($matches -ne 1){throw "R349 $name must match exactly once; found $matches"}
    [IO.File]::WriteAllText($path,$s.Replace($old,$replacement),
        [Text.UTF8Encoding]::new($false))
}
# Rework the EXACT R232 shipping Workspace, not the newer unshipped
# r283 proof-of-concept. No handlers, routes or data files changed.
One $window 'Title="DeskSpace — Workspace" Width="1120" Height="760" MinWidth="760" MinHeight="520"' 'Title="DeskSpace — Control Center" Width="1220" Height="800" MinWidth="780" MinHeight="550"' 'larger resizable workspace'
One $window 'Closing="MainWindow_Closing" Background="#F3F6FB">' @'
Closing="MainWindow_Closing" Background="#EEF3F9" Foreground="#1F344F"
        FontFamily="Segoe UI Variable, Segoe UI"
        UseLayoutRounding="True" SnapsToDevicePixels="True">
  <Window.Resources>
    <Style TargetType="Button">
      <Setter Property="Background" Value="#E1ECF9"/>
      <Setter Property="Foreground" Value="#163656"/>
      <Setter Property="BorderBrush" Value="#A8BED8"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="13,9"/>
      <Setter Property="MinHeight" Value="36"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="ButtonChrome" CornerRadius="10"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                RecognizesAccessKey="True"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="ButtonChrome" Property="Background" Value="#D2E5F6"/>
                <Setter TargetName="ButtonChrome" Property="BorderBrush" Value="#5388B5"/>
              </Trigger>
              <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter TargetName="ButtonChrome" Property="BorderBrush" Value="#1679B4"/>
                <Setter TargetName="ButtonChrome" Property="BorderThickness" Value="2"/>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="ButtonChrome" Property="Opacity" Value=".48"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType="TextBox">
      <Setter Property="Background" Value="#F9FBFF"/>
      <Setter Property="Foreground" Value="#203A57"/>
      <Setter Property="BorderBrush" Value="#A8BED7"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="10,7"/>
      <Setter Property="CaretBrush" Value="#176C9D"/>
    </Style>
    <Style TargetType="ListBox">
      <Setter Property="Background" Value="#F8FBFF"/>
      <Setter Property="Foreground" Value="#203A57"/>
      <Setter Property="BorderBrush" Value="#B5C9DE"/>
      <Setter Property="BorderThickness" Value="1"/>
    </Style>
  </Window.Resources>
'@ 'accessible global controls and native keyboard focus'
One $window '<Border DockPanel.Dock="Top" Background="#15263D" Padding="16,11">' @'
<Border DockPanel.Dock="Top" Padding="24,17"
            BorderBrush="#3D6084" BorderThickness="0,0,0,1">
      <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
          <GradientStop Color="#132642" Offset="0"/>
          <GradientStop Color="#1D4162" Offset=".65"/>
          <GradientStop Color="#245774" Offset="1"/>
        </LinearGradientBrush>
      </Border.Background>
'@ 'gradient masthead'
One $window '<TextBlock Text="▦  DeskSpace" FontSize="23" FontWeight="Bold" Foreground="White"/>' '<TextBlock Text="◈  DeskSpace" FontSize="27" FontWeight="Bold" Foreground="White"/>' 'updated brand'
One $window 'Text="YOUR WORKSPACE  ·  PRIVATE LOCAL DATA  ·  NO PASSWORD VAULT YET"' 'Text="YOUR DESKTOP  ·  LOCAL FIRST  ·  ONE WORKSPACE"' 'masthead secondary text'
One $window '<StatusBar DockPanel.Dock="Bottom"><TextBlock x:Name="StatusText"' '<StatusBar DockPanel.Dock="Bottom" Background="#DCE8F5" Foreground="#24435F" BorderBrush="#B0C7DB" BorderThickness="0,1,0,0"><TextBlock x:Name="StatusText"' 'status bar visual'
One $window '<TabControl x:Name="MainTabs" Margin="10" Background="White" TabStripPlacement="Left">' '<TabControl x:Name="MainTabs" Margin="16" Background="#F9FBFE" Foreground="#1F3857" TabStripPlacement="Left">' 'side navigation canvas'
One $window @'
      <TabControl.ItemContainerStyle>
        <Style TargetType="TabItem">
          <Setter Property="MinWidth" Value="174"/>
          <Setter Property="Padding" Value="12,11"/>
          <Setter Property="FontSize" Value="14"/>
          <Setter Property="Foreground" Value="#27394F"/>
        </Style>
      </TabControl.ItemContainerStyle>
'@ @'
      <TabControl.ItemContainerStyle>
        <Style TargetType="TabItem">
          <Setter Property="MinWidth" Value="190"/>
          <Setter Property="Padding" Value="17,13"/>
          <Setter Property="Margin" Value="3,3,8,3"/>
          <Setter Property="MinHeight" Value="43"/>
          <Setter Property="FontSize" Value="14"/>
          <Setter Property="FontWeight" Value="SemiBold"/>
          <Setter Property="Foreground" Value="#254463"/>
          <Setter Property="Background" Value="#E8F1FB"/>
          <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
              <Setter Property="Foreground" Value="#FFFFFF"/>
              <Setter Property="Background" Value="#2C6795"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter Property="BorderBrush" Value="#4A98BC"/>
            </Trigger>
          </Style.Triggers>
        </Style>
      </TabControl.ItemContainerStyle>
'@ 'modern navigational tab rail'
One $window @'
        <ScrollViewer><StackPanel Margin="24">
          <TextBlock Text="Your Workspace" FontSize="30" FontWeight="Bold"/>
'@ @'
        <ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel Margin="26">
          <Border CornerRadius="20" Background="#173554"
                  BorderBrush="#477999" BorderThickness="1"
                  Padding="24" Margin="0,0,0,20">
            <StackPanel>
              <TextBlock Text="DESKSPACE / CONTROL CENTER"
                         FontSize="11" FontWeight="SemiBold"
                         Foreground="#A4E0EC" Margin="0,0,0,10"/>
              <TextBlock Text="Your desktop. One clear space."
                         FontSize="30" FontWeight="Bold"
                         Foreground="#FAFDFF" TextWrapping="Wrap"/>
              <TextBlock Text="Search files, manage notes, switch apps and keep your essentials within reach."
                         FontSize="14" Foreground="#D2E5F3" TextWrapping="Wrap"
                         Margin="0,10,0,0"/>
            </StackPanel>
          </Border>
          <TextBlock Text="Overview" FontSize="26" FontWeight="Bold"/>
'@ 'responsive welcome hero and overview'
One $window @'
          <TextBlock Text="Quick Actions" FontSize="20" FontWeight="SemiBold"/>
          <WrapPanel Margin="0,10,0,20">
'@ @'
          <TextBlock Text="Launch pad" FontSize="20" FontWeight="SemiBold"
                     Margin="0,7,0,12"/>
          <Border Background="#FFFFFF" BorderBrush="#C5D6E7"
                  BorderThickness="1" CornerRadius="17" Padding="15"
                  Margin="0,0,0,20">
          <WrapPanel Margin="0,4,0,4">
'@ 'grouped action tiles'
One $window @'
          </WrapPanel>
          <TextBlock Text="Available today:
'@ @'
          </WrapPanel>
          </Border>
          <TextBlock Text="Available today:
'@ 'preserve footer and close grouped actions'
# Modernize the actual pre-R283 native Action Palette. Preserve all commands.
One $palette 'Title="DeskSpace actions",Width=530,Height=420,' 'Title="DeskSpace · Command Center",Width=610,Height=540,' 'command window'
One $palette 'MinWidth=420,MinHeight=320,Owner=owner,' 'MinWidth=440,MinHeight=360,Owner=owner,' 'command window minimum'
One $palette 'Background=Brushes.White,ShowInTaskbar=false,' 'Background=new SolidColorBrush(Color.FromRgb(244,248,253)),ShowInTaskbar=false,' 'command background'
One $palette 'var root=new DockPanel{Margin=new Thickness(16)};' 'var root=new DockPanel{Margin=new Thickness(24)};' 'command spacing'
One $palette 'Text="DeskSpace actions",FontSize=20,FontWeight=FontWeights.SemiBold,' 'Text="DESKSPACE / QUICK ACTIONS",FontSize=25,FontWeight=FontWeights.Bold,' 'command heading'
One $palette 'Foreground=new SolidColorBrush(Color.FromRgb(36,27,65)),' 'Foreground=new SolidColorBrush(Color.FromRgb(24,52,82)),' 'command heading contrast'
One $palette 'var input=new TextBox{FontSize=15,Padding=new Thickness(9),
            MinHeight=38,Margin=new Thickness(0,0,0,10)};' @'
var input=new TextBox{
            FontSize=16,Padding=new Thickness(12),
            MinHeight=42,Margin=new Thickness(0,0,0,12),
            Background=Brushes.White,
            Foreground=new SolidColorBrush(Color.FromRgb(25,57,85)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(151,183,211))};
'@ 'larger search field'
One $palette 'var list=new ListBox{BorderThickness=new Thickness(0),
            Background=Brushes.White};' @'
var list=new ListBox{BorderThickness=new Thickness(1),
            BorderBrush=new SolidColorBrush(Color.FromRgb(198,215,233)),
            Background=new SolidColorBrush(Color.FromRgb(248,251,255))};
'@ 'command results surface'
One $palette 'var item=new ListBoxItem{Content=box,Tag=command,Padding=new Thickness(6)};' @'
var item=new ListBoxItem{
                    Content=box,Tag=command,Padding=new Thickness(12,9),
                    Margin=new Thickness(3),
                    Background=new SolidColorBrush(Color.FromRgb(234,242,252)),
                    Foreground=new SolidColorBrush(Color.FromRgb(28,53,82))};
'@ 'command selection card'
$tokens=$null;$errors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile(
    'r349/apply-r349.ps1',[ref]$tokens,[ref]$errors)
if($errors.Count){throw ('R349 PowerShell syntax: '+($errors|Out-String))}
Write-Host 'R349: refreshed real R232 Workspace and Command Center; existing monitor/Deck/widget/Excel functionality untouched.'
