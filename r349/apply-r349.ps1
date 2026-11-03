$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$window=Join-Path $root 'MainWindow.xaml'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
foreach($path in @($window,$palette)){
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
    $s=$s.Replace($old,$replacement)
    [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
Write-Host 'R349 SHIPPING XAML HEADER';Get-Content -LiteralPath $window -TotalCount 130 | ForEach-Object {Write-Host $_}
Write-Host 'R349 SHIPPING PALETTE WINDOW';Select-String -LiteralPath $palette -Pattern 'Title=','DockPanel','Text="DeskSpace','var input=','item=new ListBoxItem' | ForEach-Object {Write-Host $_.Line}
# Only presentation. XAML element names and Click/Closing handlers are preserved.
One $window 'Title="DeskSpace — Workspace" Width="1120" Height="760" MinWidth="760" MinHeight="520"' 'Title="DeskSpace — Control Center" Width="1240" Height="810" MinWidth="780" MinHeight="560"' 'native resizable workspace dimensions'
One $window 'Closing="MainWindow_Closing" Background="#101829"' 'Closing="MainWindow_Closing" Background="#0D1423"' 'deep navy canvas'
One $window '<Setter Property="Background" Value="#293A5C"/>' '<Setter Property="Background" Value="#233754"/>' 'button restyle'
One $window '<Setter Property="BorderBrush" Value="#4F648A"/>' '<Setter Property="BorderBrush" Value="#496787"/>' 'button border'
One $window '<Setter Property="MinHeight" Value="33"/>' '<Setter Property="MinHeight" Value="36"/>' 'button hit area'
One $window '<Setter TargetName="chrome" Property="Background" Value="#3E5280"/>' '<Setter TargetName="chrome" Property="Background" Value="#385C83"/>' 'button pointer feedback'
One $window '<Setter TargetName="chrome" Property="BorderBrush" Value="#A5B7FF"/>' '<Setter TargetName="chrome" Property="BorderBrush" Value="#89D6E9"/>' 'keyboard focus feedback'
One $window '<Setter Property="Background" Value="#253550"/>' '<Setter Property="Background" Value="#1C2E49"/>' 'input surface'
One $window '<Setter Property="Background" Value="#19253E"/>' '<Setter Property="Background" Value="#15233A"/>' 'list surface'
One $window '<Border DockPanel.Dock="Top" Padding="23,15"
            BorderBrush="#465A82" BorderThickness="0,0,0,1">' @'
<Border DockPanel.Dock="Top" Padding="26,18"
            BorderBrush="#36587E" BorderThickness="0,0,0,1">
'@ 'masthead spacing and divider'
One $window '<GradientStop Color="#172544" Offset="0"/>' '<GradientStop Color="#101D33" Offset="0"/>' 'masthead left'
One $window '<GradientStop Color="#273B65" Offset=".66"/>' '<GradientStop Color="#1C3958" Offset=".66"/>' 'masthead center'
One $window '<GradientStop Color="#352D66" Offset="1"/>' '<GradientStop Color="#263F63" Offset="1"/>' 'masthead right'
One $window '<TextBlock Text="◈  DeskSpace" FontSize="25" FontWeight="Bold" Foreground="#F7FAFF"/>' '<TextBlock Text="◈  DeskSpace" FontSize="27" FontWeight="Bold" Foreground="#F7FBFF"/>' 'updated wordmark'
One $window 'Text="YOUR WORKSPACE   /   PRIVATE BY DESIGN"' 'Text="YOUR DESKTOP   •   LOCAL FIRST   •   MADE TO FLOW"' 'brand strapline'
One $window '<StatusBar DockPanel.Dock="Bottom" Background="#16223A"' '<StatusBar DockPanel.Dock="Bottom" Background="#111D30"' 'status chrome'
One $window '<TabControl x:Name="MainTabs" Margin="14" Background="#16223A"' '<TabControl x:Name="MainTabs" Margin="16" Background="#121F33"' 'navigation layout'
One $window '<Setter Property="MinWidth" Value="182"/>' '<Setter Property="MinWidth" Value="190"/>' 'navigation width'
One $window '<Setter Property="Padding" Value="17,13"/>' '<Setter Property="Padding" Value="18,14"/>' 'navigation touch targets'
One $window '<Setter Property="Background" Value="#1B2944"/>' '<Setter Property="Background" Value="#182940"/>' 'navigation default'
One $window '<Setter Property="Background" Value="#445B95"/>' '<Setter Property="Background" Value="#315F88"/>' 'navigation active'
One $window '<ScrollViewer><StackPanel Margin="24">
          <TextBlock Text="Your Workspace" FontSize="30" FontWeight="Bold"/>' @'
<ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel Margin="26">
          <Border CornerRadius="20" Padding="26" Margin="0,0,0,22"
                  Background="#1A304E" BorderBrush="#3B6383" BorderThickness="1">
            <StackPanel>
              <TextBlock Text="DESKSPACE / WORKSPACE" FontSize="11" FontWeight="SemiBold"
                         Foreground="#A6E2EE" Margin="0,0,0,9"/>
              <TextBlock Text="Your desktop. One clear space." FontSize="30"
                         FontWeight="Bold" Foreground="#F5F9FF" TextWrapping="Wrap"/>
              <TextBlock Text="Find files, manage notes, move between apps and keep your tools close."
                         FontSize="14" Foreground="#C6D8ED" TextWrapping="Wrap"
                         Margin="0,8,0,0"/>
            </StackPanel>
          </Border>
          <TextBlock Text="Overview" FontSize="25" FontWeight="Bold"/>
'@ 'new visible workspace welcome banner'
One $window '<TextBlock Text="Quick Actions" FontSize="20" FontWeight="SemiBold"/>
          <WrapPanel Margin="0,10,0,20">' @'
<TextBlock Text="Launch pad" FontSize="20" FontWeight="SemiBold"
                     Margin="0,4,0,12"/>
          <Border Background="#16263F" BorderBrush="#355270" BorderThickness="1"
                  CornerRadius="17" Padding="15" Margin="0,0,0,20">
          <WrapPanel Margin="0,4,0,4">
'@ 'action panel background and grouping'
One $window @'
          </WrapPanel>
          <TextBlock Text="Available today:
'@ @'
          </WrapPanel>
          </Border>
          <TextBlock Text="Available today:
'@ 'action panel closure'
# Update the existing Action Palette, not a second launcher or command engine.
One $palette 'Title="DeskSpace actions",Width=530,Height=420,' 'Title="DeskSpace · Command Center",Width=610,Height=540,' 'command center window size'
One $palette 'MinWidth=420,MinHeight=320,Owner=owner,' 'MinWidth=440,MinHeight=360,Owner=owner,' 'command center min size'
One $palette 'var root=new DockPanel{Margin=new Thickness(16)};' 'var root=new DockPanel{Margin=new Thickness(24)};' 'command center spacing'
One $palette 'Text="DeskSpace actions",FontSize=20,FontWeight=FontWeights.SemiBold,' 'Text="DESKSPACE / QUICK ACTIONS",FontSize=25,FontWeight=FontWeights.Bold,' 'command center heading'
One $palette 'Margin=new Thickness(0,0,0,8)' 'Margin=new Thickness(0,0,0,10)' 'command center title spacing'
One $palette 'var input=new TextBox{FontSize=15,Padding=new Thickness(9),
            MinHeight=38,Margin=new Thickness(0,0,0,10)};' @'
var input=new TextBox{FontSize=16,Padding=new Thickness(12),
            MinHeight=43,Margin=new Thickness(0,0,0,12)};
'@ 'larger action search field'
One $palette 'var item=new ListBoxItem{Content=box,Tag=command,Padding=new Thickness(6)};' @'
var item=new ListBoxItem{
                    Content=box,Tag=command,
                    Padding=new Thickness(14,10,14,10),
                    Margin=new Thickness(4),
                    Background=new SolidColorBrush(Color.FromRgb(27,45,69)),
                    Foreground=new SolidColorBrush(Color.FromRgb(242,248,255))};
'@ 'action result cards'
# Keep the active widget/Deck layout and geometry unchanged; do not
# inject legacy style libraries into the R232 shipping installer.
One $palette 'Background=Brushes.White,ShowInTaskbar=false,' 'Background=new SolidColorBrush(Color.FromRgb(12,22,38)),ShowInTaskbar=false,' 'command center canvas'
One $palette 'Foreground=new SolidColorBrush(Color.FromRgb(36,27,65)),' 'Foreground=new SolidColorBrush(Color.FromRgb(240,247,255)),' 'command heading contrast'
One $palette 'Foreground=Brushes.DimGray,FontSize=12,' 'Foreground=new SolidColorBrush(Color.FromRgb(176,196,224)),FontSize=12,' 'command helper contrast'
One $palette 'Background=Brushes.White};' 'Background=new SolidColorBrush(Color.FromRgb(15,29,48))};' 'command result surface'
$tokens=$null;$errors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile('r349/apply-r349.ps1',[ref]$tokens,[ref]$errors)
if($errors.Count){throw ('R349 PowerShell syntax: '+($errors|Out-String))}
# XML is fully parsed by the Windows WPF XAML compiler in the release test.
Write-Host 'R349: modern Workspace hero/navigation/action cards and Command Center, without modifying Deck/widget geometry.'
