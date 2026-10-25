$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$sheet=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$updater=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$xaml=[IO.File]::ReadAllText("$r/MainWindow.xaml")
$main=[IO.File]::ReadAllText("$r/MainWindow.xaml.cs")
foreach($part in @(
  'Text=isFormula?"ƒx":Placeholder(item)',
  'var scale=new ScaleTransform(1,1)',
  'HorizontalScrollBarVisibility=ScrollBarVisibility.Auto',
  'foreach(var setting in new[]{"Fit width","100%","150%","200%","300%"})',
  'surface.Children.Add(text);',
  'throw new InvalidOperationException(',
  'Combined captured area exceeds 1800 cells',
  'if(prior!=null)Layer(prior,priorRowShift,priorColumnShift)',
  'if(addition!=null)Layer(addition,newRowShift,newColumnShift)'
)){
 if(!$sheet.Contains($part)){throw "R104 missing legible sheet/merge: $part"}
}
foreach($part in @(
  '/releases/latest',
  '/releases?per_page=40',
  'tester-v',
  'public static string Channel',
  'public static void SetChannel(string value)',
  'if(channel=="tester")',
  'SHA256.HashData(data)',
  'Verify(installer,checksum,root)',
  'GetManifestResourceStream("DeskSpace.DeskSpaceUpdateSupervisor.ps1")',
  'PruneUpdateCache('
)){
 if(!$updater.Contains($part)){throw "R104 update safety/channel missing: $part"}
}
if(!$xaml.Contains('UpdateChannelPickerV104')){throw 'R104 XAML channel control missing'}
foreach($part in @('UpdateChannel_LoadedV104','UpdateChannel_ChangedV104')){
 if(!$xaml.Contains($part) -or !$main.Contains($part)){throw "R104 Settings event missing: $part"}
}
# This only checks source invariants. Windows compilation and actual screenshots
# must still pass; a string check is NOT proof that UI rendering is correct.
Write-Host 'R104 static safeguards PASS; Windows build and real WPF visual test still required.'
