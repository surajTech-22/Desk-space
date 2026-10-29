$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source=Get-Content -LiteralPath 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs' -Raw
foreach($gate in @(
    'e.Process is {Length: >=1 and <=128}',
    'e.TitleHash is {Length: 64}',
    'e.Monitor is {Length: >=1 and <=128}',
    'return result.Where(ValidProfile).Take(12).ToList();',
    'if(!ValidProfile(profile))',
    'if(screen==null){skipped++;continue;}',
    's.DeviceName.Equals(saved.Monitor,'
)){
    if(!$source.Contains($gate)){throw "R181 regression missing: $gate"}
}
if($source.Contains('s.DeviceName==saved.Monitor)??Forms.Screen.PrimaryScreen')){
    throw 'R181 failed: absent monitor may still move an unrelated window'
}
$test=Get-Content -LiteralPath 'r36/UiSmoke/Program.cs' -Raw
if(!$test.Contains('TestMalformedLayoutsV181();')){
    throw 'R181 native WPF regression was not registered'
}
Write-Host 'R181 source gates PASS; WPF malformed-profile behavior runs in R41 suite.'
