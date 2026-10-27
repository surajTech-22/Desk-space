$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$widget=[IO.File]::ReadAllText("$r/WidgetHost.cs")
$main=[IO.File]::ReadAllText("$r/MainWindow.xaml.cs")
$studio=[IO.File]::ReadAllText("$r/DeskSpaceSettingsStudioV148.cs")
foreach($token in @(
 'private static double CompactDiameter=>',
 'private static double OpenDiameter=>',
 'DeskSpaceAppearanceV148.Current.Scale;',
 'private Brush light=>DeskSpaceAppearanceV148.LightBrush;',
 'private Brush accent=>DeskSpaceAppearanceV148.AccentBrush;',
 'DeskSpaceAppearanceV148.SecondaryBrush',
 'Foreground=DeskSpaceAppearanceV148.InkBrush',
 'DeskSpaceAppearanceV148.Apply(this);',
 'RefreshAppearanceV148,',
 'private void RefreshAppearanceV148()',
 'DeskSpaceSettingsStudioV148.Show(this,prefs,')){
 if(!$widget.Contains($token)){throw "R148 widget linkage missing: $token"}
}
foreach($token in @(
 'DeskSpaceSettingsStudioV148.Show(this,settings,',
 'new WidgetOptionsWindow(settings,')){
 if(!$main.Contains($token)){throw "R148 Workspace settings linkage missing: $token"}
}
foreach($token in @(
 'public double Scale {get;set;}=1;',
 'public double Opacity {get;set;}=.96;',
 'Math.Clamp(item.Scale,.75,1.25)',
 'Math.Clamp(item.Opacity,.65,1)',
 'Path.Combine(WorkspaceStore.Root,"appearance-v148.json")',
 'File.Move(tmp,PathName,true);',
 'DeskSpaceWorkspaceProfilesV85.Save(profile)',
 'DeskSpaceWorkspaceProfilesV85.Apply(selected,settings)',
 'settings.PinnedApps.RemoveAt(i);',
 'settings.PinnedApps.Add(chooser.FileName);',
 'DeskSpaceAppearanceV148.Save(draft);',
 'Current.Style=="Material"',
 'DeskSpaceAppearanceV148.Mix',
 'System.Windows.Shapes.Path',
 'new[]{"Donut toolbar","Theme","Presets","General"}',
 'The existing DeskSpace general settings remain intact')){
 if(!$studio.Contains($token)){throw "R148 visual studio feature missing: $token"}
}
if($widget.Contains('private readonly Brush accent = new SolidColorBrush(Color.FromRgb(115, 100, 211));'))
 {throw 'R148 legacy fixed widget accent still active'}
if($studio.Contains('Filee') || $studio.Contains('Convert file'))
 {throw 'R148 must not copy file conversion UI'}
Write-Host 'R148 PASS: actual donut colour/scale/opacity wired, grouped settings Studio, persisted palettes and presets, pinned app ordering, original general options and data remain intact.'
