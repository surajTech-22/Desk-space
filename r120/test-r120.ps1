$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs')
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'MatchHeaderSignatureV120(',
 'SignatureAnchorsV120(',
 'c.Role=="Reference" && Normalize(c.RawValue).Length>2',
 'HeaderValueDecisionV120(',
 'UsedReferenceSignatureV120',
 'if(preview.UsedReferenceSignatureV120)',
 'if(!matched.Unique || matched.Row!=preview.StartRow',
 'string decision=HeaderValueDecisionV120(',
 'if(decision=="Ready" && (bool)cell.MergeCells)',
 'Only mapped values can be pasted.',
 'partially populated workbooks'
)){
 if(!$s.Contains($needle)){throw "R120 header/value fix missing: $needle"}
}
# Format-template R107 gate may exist as a helper, but must NOT be called
# from value Preview/Apply. Its only caller belongs in R109 formatting.
$start=$s.IndexOf('    public static DeskSpaceExcelProposalV16 Preview(')
$apply=$s.IndexOf('    public static string Apply(', $start)
if($start -lt 0 -or $apply -lt 0){throw 'R120 preview/apply method not found'}
$end=$s.IndexOf('    private static object? TypedValue(', $start)
if($end -lt 0){throw 'R120 typed value method not found'}
$preview=$s.Substring($start,$end-$start)
$applyBody=$s.Substring($apply)
if($preview.Contains('CheckAllCellsV116(') -or
   $preview.Contains('DeskSpaceExcelFullRangeGateV107') -or
   $applyBody.Contains('CheckAllCellsV116(')){
 throw 'R120 formatting occupancy gate is still being used for header/value autofill'
}
if(!$s.Contains('SourceKind') -or !$s.Contains('CoordinatesVerified')){
 throw 'R120 original live Excel verified-address protection changed'
}
if(!$ui.Contains('TestHeaderValueSignatureV120();') -or
   !$ui.Contains('R120 populated 8/30 destination must allow 22 safe value writes') -or
   !$ui.Contains('R120 ambiguous repeated signature must block paste')){
 throw 'R120 compiled WPF regression tests missing'
}
# Do not mutate the format-only writer in this pass.
if(!(Test-Path 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs')){
 throw 'R120 existing independent formatting engine missing'
}
Write-Host 'R120 static PASS: value signature & per-cell paste separate, 8/30 WPF test registered, formatting engine intact.'
