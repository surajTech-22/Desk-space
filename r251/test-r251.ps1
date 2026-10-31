$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs')
$v=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFormulaMetadataV251.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const int MaxFormulaChars=8192',
 "formula[0]!='='",
 'if(order.Any(x=>!DeskSpaceFormulaMetadataV251.Valid(',
 'x.StoredType,x.FormulaR1C1))',
 'Captured formula metadata is incomplete',
 'TestExcelR1C1SourceIntegrityV251();'
)){
 if(!$s.Contains($needle) -and !$v.Contains($needle) -and !$t.Contains($needle)){
  throw 'R251 formula preflight missing '+$needle
 }
}
if($s.IndexOf('if(order.Any(x=>!DeskSpaceFormulaMetadataV251.Valid(') -gt
   $s.IndexOf('internal static string Apply(')){
 throw 'R251 metadata guard must run in preflight before formula writes'
}
Write-Host 'R251 STATIC PASS: incomplete R1C1 formula metadata cannot reach Excel paste.'
