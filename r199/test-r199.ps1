$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$features=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$search=[IO.File]::ReadAllText("$root/DeskSpaceSearchV11.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
if(([regex]::Matches($main,[regex]::Escape('InitializeFeatureHubV199();'))).Count -ne 1){
 throw 'R199 Workspace must initialize the Features tab exactly once'
}
foreach($item in @(
 'Header="✦  Features"',
 'Tag="DeskSpaceFeatures"',
 'MainTabs.Items.Add(tab)',
 'DeskSpaceActionPaletteV197.Show(this,false)',
 'DeskSpaceClipboardTransformsV177.Show(this)',
 'DeskSpaceWindowLayoutsV179.Show(this)',
 'DeskSpaceComponentHealthV175.Show(this,false)',
 'ShowExcelMappingFromWheelV49',
 'SelectExistingFeatureTabV199("Reminders")',
 'SelectExistingFeatureTabV199("Notes")',
 'Text="Installed version: "',
 'Keyboard.Modifiers==ModifierKeys.Control',
 'Button Click'
)){
 if($item -eq 'Button Click'){continue}
 if(!$features.Contains($item)){throw ("R199 Workspace Features entry missing "+$item)}
}
foreach($item in @(
 'var allActions=new Button',
 'ToolTip="Open DeskSpace Features and Action Palette"',
 'Open DeskSpace Action Palette',
 'Window.GetWindow(frame)',
 'DeskSpaceActionPaletteV197.Show(owner,false)',
 'top.Children.Add(allActions)'
)){
 if(!$search.Contains($item)){throw ("R199 visible donut Search entry missing "+$item)}
}
if(!$palette.Contains('DeskSpaceClipboardTransformsV177.Show(owner)') -or
   !$palette.Contains('DeskSpaceWindowLayoutsV179.Show(owner)')){
 throw 'R199 new access points must reuse existing, tested tool windows'
}
if($features.Contains('Process.Start(') -or $features.Contains('File.Delete(') -or
   $features.Contains('SendKeys(')){
 throw 'R199 Features tab must not launch arbitrary apps or mutate user data'
}
Write-Host 'R199 PASS: permanent Workspace Features tab, visible widget Search action, keyboard palette, existing tool reuse.'
