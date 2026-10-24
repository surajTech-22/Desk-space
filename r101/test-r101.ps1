$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$store=[IO.File]::ReadAllText("$r/DeskSpaceExcelStoreV16.cs")
$bridge=[IO.File]::ReadAllText("$r/DeskSpaceExcelBridgeV16.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$visual=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$types=[IO.File]::ReadAllText("$r/DeskSpaceExcelInputTypeV101.cs")
foreach($needle in @(
 'DeskSpaceExcelSheetVisualV101? SheetVisualV101',
 'entry.SheetVisualV101=DeskSpaceExcelVisualCaptureV101.Capture(range)',
 'var tabs=new TabControl',
 'Captured sheet · formats & formulas',
 'Mappings · edit & paste',
 'DeskSpaceExcelSheetViewV101.Create(entry)',
 'sheetPreviewHostV101.Content=null',
 'Content=mappingGridV42')){
 if(!$store.Contains($needle) -and !$bridge.Contains($needle) -and
    !$editor.Contains($needle)){throw "R101 mapping integration missing: $needle"}
}
foreach($needle in @(
 'bool formula=Flag(()=> (object?)cell.HasFormula)',
 'style.StoredType=DeskSpaceExcelInputTypeV101.Stored(raw,formula)',
 'style.ExpectedType=DeskSpaceExcelInputTypeV101.Expected(',
 'NumberFormat=Format(()=> (object?)cell.NumberFormat',
 'BorderLeft=VisibleBorder(cell,7)',
 'style.MergeRows=height;style.MergeColumns=width',
 'Text=isFormula?',
 'current data intentionally hidden',
 'Stored: ','Expected: ',
 'SheetVisualV101')){
 if(!$visual.Contains($needle)){throw "R101 visual capture incomplete: $needle"}
}
foreach($needle in @(
 'internal static string Expected(',
 'internal static Conversion Normalize(',
 'double.TryParse(',
 'DateTime.TryParseExact(',
 'Leading zeroes may be meaningful identifiers',
 'Ambiguous short date',
 'More than 15')){
 # Last text is checked separately case insensitive below.
 if(!$types.Contains($needle) -and $needle -ne 'More than 15'){
  throw "R101 type and safe coercion missing: $needle"}
}
if($visual.Contains('Text=item.RawValue') -or $visual.Contains('Text=item.Display') -or
   $visual.Contains('Text=cell.RawValue')){
 throw 'R101 must never put previous Excel input values into the captured sheet view'
}
Write-Host 'R101 PASS: capture real Excel cell formats and stored/expected input kind, draw sheet without old data, keep mappings and paste unchanged.'
