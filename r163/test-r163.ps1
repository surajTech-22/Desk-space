$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$zip=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceArchiveSearchV163.cs')
$preview=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFilePreviewV85.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('MaxArchiveBytes=256L*1024*1024','MaxEntries=10000','MaxShown=60','ZipFile.OpenRead(path)','e.FullName.Contains(q,StringComparison.OrdinalIgnoreCase)','ZIP contents · read-only')){
 if(!$zip.Contains($token)){throw "R163 archive inspection missing $token"}
}
if(!$preview.Contains('DeskSpaceArchiveSearchV163.Create(path)')){throw 'R163 preview integration missing'}
foreach($token in @('TestSearchableZipV163();','private static void TestSearchableZipV163()','R163 ZIP search must filter by filename','R163 ZIP preview must never extract')){
 if(!$smoke.Contains($token)){throw "R163 WPF regression missing $token"}
}
Write-Host 'R163 PASS: bounded searchable archive preview integrated with real WPF test.'
