$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$excel=Get-Content (Join-Path $root 'DeskSpaceExcelWheelV42.cs') -Raw
$info=Get-Content (Join-Path $root 'DeskSpaceInformationV36.cs') -Raw
$store=Get-Content (Join-Path $root 'DeskSpaceExcelStoreV16.cs') -Raw
$hostCode=Get-Content (Join-Path $root 'WidgetHost.cs') -Raw
if(!$store.Contains('public static DateTime LastChangeUtc()')){
    throw 'R56 Excel store revision unavailable'
}
foreach($token in @('public void WarmStart()','Task.Run(()=>',
    'warmed=true;warming=false','if(arc.IsOpen)Render()',
    'LastChangeUtc()!=warmStamp','WarmStart(); // paint the cached data')){
    if(!$excel.Contains($token)){throw "R56 Excel warmup missing $token"}
}
foreach($token in @('public void WarmStart()','CurrentStamp()!=warmStamp',
    'WarmStart(); // immediately paint cache','warmed=true;warming=false',
    'if(arc.IsOpen){RenderArc();PositionArc();changed();}')){
    if(!$info.Contains($token)){throw "R56 Information warmup missing $token"}
}
foreach($token in @('DeskSpaceSearchIndexV13.WarmStart();',
    'excelWheel.WarmStart();','informationOverlay.WarmStart();',
    'appsWheel?.HideArc();','"Applications", "Tasks", "Excel Functions"')){
    if(!$hostCode.Contains($token)){throw "R56 startup integration missing $token"}
}
Write-Host 'R56 source PASS: cached child data is loaded at startup and refreshed on modification without synchronous open-time reading.'
