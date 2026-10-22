$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r42/apply-r42-updater.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$wheel=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelWheelV42.cs'))
$updater=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceUpdateCheckV42.cs'))
$oldUpdater=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceUpdater.cs'))
foreach($v in @(
    'canvas.Width=Math.Max(100,extent.Width+2*margin)',
    'canvas.LayoutTransform=new ScaleTransform(scale,scale)',
    'localOriginX=extent.Left',
    'geometry.Transform=translation',
    'drillTimer.Start();',
    'item.Command=="information"||item.Command=="group"',
    'if(item.Command!="mapping" || !item.Id.HasValue)return;',
    'DeskSpaceExcelClipboardV39.ShowActive();'
)){
    if(!$wheel.Contains($v)){throw "R42 hover wheel geometry/logic missing: $v"}
}
if($wheel.Contains('canvas=new Canvas{Width=650,Height=650') -or
   $wheel.Contains('Status("Excel Information","Saved mappings')){
    throw 'R42 old detached popup/click-to-open Information behavior remains'
}
if(!$wheel.Contains('CX=325,CY=325,Outer=252,Inner=196')){
    throw 'R42 child-arc geometry is not directly connected to main outer ring'
}
foreach($v in @('FindCli()','GitHubCLI','auth","status"',
    'release","list"','isPrerelease','DeskSpaceUpdater.CurrentVersion',
    'DeskSpaceUpdater.CheckAsync(owner)','Uri.EscapeDataString(newerTag)')){
    if(!$updater.Contains($v)){throw "R42 update checking disabled: $v"}
}
if(!$oldUpdater.Contains('public static string CurrentVersion')){
    throw 'R42 original version stamping was removed'
}
if(!$oldUpdater.Contains('DeskSpace_Setup.sha256.txt')){
    throw 'R42 original stable updater SHA-256 safety gate missing'
}
$count=0
foreach($file in @(Get-ChildItem -LiteralPath $root -Filter '*.cs' -File)){
    if($file.Name -eq 'DeskSpaceUpdater.cs' -or
       $file.Name -eq 'DeskSpaceUpdateCheckV42.cs'){continue}
    $s=[IO.File]::ReadAllText($file.FullName)
    $count+=([regex]::Matches($s,'DeskSpaceUpdateCheckV42[.]CheckAsync[(]')).Count
    if($s.Contains('DeskSpaceUpdater.CheckAsync(')){
        throw ('R42 manual update UI still uses old non-tester path: '+$file.Name)
    }
}
if($count -lt 1){throw 'R42 no update-check buttons wired'}
Write-Host "R42 UI + update checks PASS: hover drill-down, tight outer-ring popup, guarded paste, $count manual update actions, private tester/stable update paths."
