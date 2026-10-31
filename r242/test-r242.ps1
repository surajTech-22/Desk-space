$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFullRangeGateV107.cs'
$s=[IO.File]::ReadAllText($p)
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'for(int i=0;i<source.Count;i++)',
 '!ValidCellV242(source[i]) || !ValidCellV242(destination[i])',
 'internal static bool ValidCellV242(Cell? cell)',
 'if(string.IsNullOrWhiteSpace(value))return "";',
 '"DATE" or "NUMBER"=>"Number"',
 '"FORMULA"=>"Formula"',
 'return cell.HasData?type!="Blank":type=="Blank"'
)){
 if(!$s.Contains($needle)){throw ('R242 Excel safety missing '+$needle)}
}
if($s.IndexOf('!ValidCellV242(source[i])') -gt
   $s.IndexOf('if(filled==0)'))
{
 throw 'R242 blank destination approval still precedes input validation'
}
if(!$test.Contains('TestExcelFailClosedTypesV242();'))
{
 throw 'R242 missing native pure Excel gate test'
}
Write-Host 'R242 STATIC PASS: unknown, null, inconsistent input and source metadata blocked before destination status.'
