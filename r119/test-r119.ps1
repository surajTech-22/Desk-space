$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 '(int Row,int Column)? selected=',
 'SelectedOriginV116((object)app,entry);',
 'if(selected.HasValue)',
 'selected.Value.Row,selected.Value.Column'
)){
 if(!$source.Contains($needle)){throw "R119 missing typed selection guard: $needle"}
}
if($source.Contains('var selected=SelectedOriginV116(app,entry);')){
 throw 'R119 dynamic tuple HasValue crash still present'
}
foreach($needle in @(
 'TestNullableSelectedOriginV119();',
 'TestNullableSelectedOriginV119()',
 'new System.Dynamic.ExpandoObject()',
 'selected is ValueTuple<int,int>',
 'wrong-size destination must reject cleanly',
 'disjoint destination must reject cleanly'
)){
 if(!$smoke.Contains($needle)){throw "R119 missing runtime WPF verification: $needle"}
}
Write-Host 'R119 static guard PASS; Windows compiled WPF test verifies real SelectedOriginV116 without Excel installation.'
