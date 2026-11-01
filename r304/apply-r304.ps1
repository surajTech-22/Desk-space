$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source="$root/DeskSpaceDropShelfV15.cs"
if(!(Test-Path -LiteralPath $source)){throw 'Original Stable Deck source missing'}
if(!(Test-Path "$root/DeskSpaceMonitorDockV190.cs")){throw 'R301 below-only Deck implementation missing'}
Copy-Item -LiteralPath 'r304/DeskSpaceDeckVisibilityV304.cs' -Destination "$root/DeskSpaceDeckVisibilityV304.cs" -Force
$s=[IO.File]::ReadAllText($source)
$old=@'
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup,
            (proposed,work)=>DeskSpaceMonitorDockV190.ConstrainBeforePaintV298(
                host,proposed,work));
'@
$lf=[string][char]10
$s=$s.Replace(([string][char]13+$lf),$lf)
$old=$old.Replace(([string][char]13+$lf),$lf)
if(($s.Split([string[]]@($old),[StringSplitOptions]::None).Length-1) -ne 1){
 throw 'R304 expected one real Running Apps Popup/native fence hook'
}
$s=$s.Replace($old,$old+$lf+'        DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);')
$oldTimer='Interval=TimeSpan.FromSeconds(2)'
if(($s.Split([string[]]@($oldTimer),[StringSplitOptions]::None).Length-1) -ne 1){
 throw 'R304 expected one existing running-apps timer interval'
}
$s=$s.Replace($oldTimer,'Interval=TimeSpan.FromSeconds(5)')
[IO.File]::WriteAllText($source,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);') -or
   !$s.Contains('Interval=TimeSpan.FromSeconds(5)')){
 throw 'R304 running apps popup fronting/performance integration failed'
}
Write-Host 'R304: native Deck stays in topmost band; dark scroll without white arrow rail; 5-second running-app scan.'
