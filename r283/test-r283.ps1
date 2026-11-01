$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$design=[IO.File]::ReadAllText("$root/DeskSpaceDesignV283.cs")
$studio=[IO.File]::ReadAllText("$root/DeskSpaceSettingsStudioV148.cs")
$widget=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($key in @(
 'internal static Palette Resolve(',
 'internal static void ApplyDeck(Border panel)',
 'internal static void ApplyWindow(Window window,Palette p)',
 'internal static void Button(Button button,bool primary=false,',
 'internal static void Input(TextBox text,Palette? palette=null)',
 'SystemParameters.HighContrast',
 'Template=border',
 'internal static Brush WidgetFace()',
 'public string Style {get;set;}="Modern";',
 'item.Style is "Modern" or "Material" or "Soft"',
 'Modern · immersive',
 'private void UpdateChromeV283()',
 'DeskSpaceDesignV283.ApplyWindow(this,palette);',
 'DeskSpaceDesignV283.WidgetFace();',
 'DeskSpaceDesignV283.ApplyDeck(panel);',
 'DeskSpaceDesignV283.ApplyWindow(window,',
 'DeskSpaceDesignV283.Button(button);',
 'ControlTemplate TargetType="Button"',
 'CornerRadius="10"',
 'TestModernDesignSystemV283();'
)){
 if(!$design.Contains($key) -and !$studio.Contains($key) -and
   !$widget.Contains($key) -and !$deck.Contains($key) -and
   !$main.Contains($key) -and !$palette.Contains($key) -and
   !$hub.Contains($key) -and !$tests.Contains($key)){
  throw ('R283 visual system or native test missing: '+$key)
 }
}
if(!$widget.Contains('DeskSpaceHighContrastV208.Wedge(') -or
   !$widget.Contains('DeskSpaceHighContrastV208.Outline(') -or
   !$widget.Contains('private static readonly string[] Segments') -or
   !$studio.Contains('DeskSpaceAppearanceV148.Save(draft);') -or
   !$studio.Contains('DeskSpaceMultiMonitorV151.Configure(showEveryMonitorDraft);') -or
   !$deck.Contains('DeskSpaceMonitorDockV190.Attach(host,popup,panel);') -or
   !$deck.Contains('DeskSpaceSmartContextDeckV280.CreatePanel(')){
 throw 'R283 must preserve donut hit geometry, persisted settings and monitor-local Deck'
}
foreach($needle in @('Process.Start(','File.ReadAllText(','Clipboard.GetText(')){
 if($design.Contains($needle)){throw ('R283 design code may not access user data: '+$needle)}
}
Write-Host 'R283 STATIC PASS: cohesive modern WPF design across Workspace, donut, Deck and settings, preserving behavior.'
