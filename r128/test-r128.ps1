$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$ed=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$ctl=[IO.File]::ReadAllText("$r/DeskSpaceExcelUnifiedV126.cs")
$store=[IO.File]::ReadAllText("$r/DeskSpaceExcelStoreV16.cs")
$bridge=[IO.File]::ReadAllText("$r/DeskSpaceExcelBridgeV16.cs")
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$wheel=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
foreach($needle in @(
  'saved.Select(x=>new {Entry=x,IsFormat=false})',
  'formatLibraryV121.Select(x=>new {Entry=x,IsFormat=true})',
  'templates.ItemsSource=display.Select(x=>x.Entry).ToArray();',
  '" saved mappings · "',
  'object.ReferenceEquals(x,item)',
  'DeleteUnifiedEntryV126(item.Id,fromFormats)',
  'bin.SetValue(System.Windows.Shapes.Path.DataProperty,',
  'Geometry.Parse("M3,4',
  'Button.WidthProperty,20.0',
  'Button.BackgroundProperty,Brushes.Transparent',
  'Button.BorderThicknessProperty,new Thickness(0)',
  'trash.AppendChild(bin);',
  'Header="Captured Sheet"',
  'Header="Mappings"'
)){
  if(!$ed.Contains($needle)){throw "R128 missing shared list/delete icon: $needle"}
}
foreach($bad in @('trash.SetValue(Button.ContentProperty,"×")',
    'DeleteUnifiedEntryV126(item.Id,formatModeV126)',
    '" saved format mappings · "+matches.Length+" shown"'))
{
  if($ed.Contains($bad)){throw "R128 old split list or large X remains: $bad"}
}
foreach($needle in @(
   'bool isFormat=formatLibraryV121.Any(x=>object.ReferenceEquals(x,item));',
   'bool isInformation=saved.Any(x=>object.ReferenceEquals(x,item));',
   'formatModeV126=isFormat;',
   'ShowFormatV126(item);',
   'OnLibrarySelectedV42();',
   'DeskSpaceExcelStoreV16.LoadFormats()',
   'DeskSpaceExcelStoreV16.SaveFormats(records)',
   'DeskSpaceExcelStoreV16.Load()',
   'DeskSpaceExcelStoreV16.Save(records)'
)){
  if(!$ctl.Contains($needle)){throw "R128 source-owned selection/save missing: $needle"}
}
if($ctl.Contains('if(fromFormatLibrary!=formatModeV126)'))
{throw 'R128 still prevents cross-library item deletion'}
if(!$store.Contains('formats.dpapi') -or !$store.Contains('templates.dpapi'))
{throw 'Original encrypted libraries missing'}
if(!$bridge.Contains('Information paste requires verified matching headers.') -or
   !$wheel.Contains('Data paste blocked; nothing changed.'))
{throw 'Strict information header signature check was changed'}
if(!$format.Contains('HeadersIndependentV123(') -or
   !$format.Contains('DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)') -or
   !$format.Contains('book.SaveCopyAs(backup)'))
{throw 'Independent format-apply safety changed'}
Write-Host 'R128 PASS: BOTH mappings libraries always listed; selected record drives correct editor and paste engine; small discreet delete icon routes by origin; encrypted user data protected.'
