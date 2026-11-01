$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceSettingsStudioV148.cs")
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$workspace=[IO.File]::ReadAllText("$root/MainWindow.xaml")
foreach($token in @(
 'public string Style {get;set;}="Soft";',
 'item.Style=item.Style=="Material"?"Material":"Soft";',
 'new[]{"Donut toolbar","Theme","Displays","Presets","General"}',
 'DeskSpaceAppearanceV148.Save(draft);',
 'DeskSpaceMultiMonitorV151.Configure(showEveryMonitorDraft);'
)){if(!$source.Contains($token)){throw ('R283 appearance baseline changed: '+$token)}}
foreach($token in @(
 'private void BuildCompact()',
 'private void BuildDonut()',
 'DeskSpaceAppearanceV148.SecondaryBrush',
 'DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast)'
)){if(!$widget.Contains($token)){throw ('R283 donut geometry baseline changed: '+$token)}}
if(!$deck.Contains('DeskSpaceMonitorDockV190.Attach(host,popup,panel);') -or
   !$deck.Contains('DeskSpaceSmartContextDeckV280.CreatePanel(')){
 throw 'R283 cannot modify Deck until monitor positioning and Smart Context integration exist'
}
if(!$workspace.Contains('x:Name="MainTabs"') -or
   !$workspace.Contains('x:Name="StatusText"') -or
   !$workspace.Contains('MainWindow_Closing')){
 throw 'R283 main Workspace structure is missing'
}
Copy-Item 'r283/DeskSpaceDesignV283.cs' "$root/DeskSpaceDesignV283.cs" -Force
Copy-Item 'r283/WidgetHostV283.cs' "$root/WidgetHost.cs" -Force
Copy-Item 'r283/DeskSpaceSettingsStudioV283.cs' "$root/DeskSpaceSettingsStudioV148.cs" -Force
Copy-Item 'r283/DeskSpaceDropShelfV283.cs' "$root/DeskSpaceDropShelfV15.cs" -Force
Copy-Item 'r283/DeskSpaceActionPaletteV283.cs' "$root/DeskSpaceActionPaletteV197.cs" -Force
Copy-Item 'r283/DeskSpaceFeatureHubV283.cs' "$root/DeskSpaceFeatureHubV199.cs" -Force
Copy-Item 'r283/MainWindowV283.xaml' "$root/MainWindow.xaml" -Force
Write-Host 'R283: modern design tokens, actual Workspace, donut, Deck, Settings, Features and Action Palette all reconstructed.'
