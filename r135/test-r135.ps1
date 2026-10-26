$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$w=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$d=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
$e=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$p=[IO.File]::ReadAllText("$r/DeskSpaceExcelRenderedPolicyV135.cs")
$s=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
function Require([string]$v,[string]$q,[string]$label){
 if(!$v.Contains($q)){throw "R135 $label missing $q"}
}
foreach($part in @('DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(',
'var sourceTablesV135=live.Visual.FeaturesV106!.Tables;',
'PreflightTableStylesV131(','DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
'book.SaveCopyAs(backup);','RestoreAfterFailure(live,before,',
'SetTableStyleV131(','BuildMissingStructure(live,originallyEmpty);',
'PopulateBlankFormulas(live);','wanted.UnderlineStyle!=observed.UnderlineStyle',
'x.TableStyle==expectedTable.TableStyle',
'An existing Excel value/formula changed unexpectedly.')){
 Require $w $part 'trusted Excel writer'
}
if($w.Contains('Rendered Excel Table/cell appearance differs at ')){
 throw 'R135 old computed-table-render mismatch is still active'
}
Require $d 'DeskSpaceExcelFormatWorkerV130.PreviewAsync(entry)' 'wheel preview'
Require $d 'if(!review.Eligible)' 'wheel eligibility gate'
Require $d 'DeskSpaceExcelFormatWorkerV130.ApplyAsync(' 'wheel execution'
if($d.Contains('DeskSpace · confirm FORMAT-only paste')){
 throw 'R135 redundant format donut modal remains'
}
if($e.Contains('DeskSpace — confirm Excel formatting')){
 throw 'R135 redundant format Workspace modal remains'
}
foreach($part in @('if(!t.FullySelected','string.IsNullOrWhiteSpace(t.TableStyle)',
'System._ComObject','row>=t.Row','column>=t.Column')){
 Require $p $part 'table specific policy'
}
foreach($part in @('TestExcelTableRenderedPolicyV135();',
'An invalid saved table style must not bypass',
'Non-table cell rendering must remain strictly verified',
'Partially captured tables may not relax')){
 Require $s $part 'compiled WPF safety regression'
}
Write-Host 'R135 STATIC PASS: single-click user-initiated format paste, mandatory full-range gate, verified backup, rollback, formula and exact ListObject checks; only Excel Table generated DisplayFormat comparison normalized; strict non-table check and compiled WPF regression present.'
Write-Warning 'Actual Office COM acceptance still requires an Excel-installed Windows runner; do not claim end-to-end paste success without that result.'