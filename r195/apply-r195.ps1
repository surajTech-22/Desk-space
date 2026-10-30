$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r195/DeskSpaceDpiBoundsV195.cs' -Destination "$root/DeskSpaceDpiBoundsV195.cs" -Force
$p="$root/DeskSpaceMonitorDockV190.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
            if(!double.IsFinite(sx)||sx<=0)sx=1;
            if(!double.IsFinite(sy)||sy<=0)sy=1;
'@.Replace($crlf,$lf)
$new=@'
            sx=DeskSpaceDpiBoundsV195.CleanScale(sx);
            sy=DeskSpaceDpiBoundsV195.CleanScale(sy);
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 2){throw ("R195 requires exactly two dock DPI conversions; found "+$count)}
$s=$s.Replace($old,$new)
$old=@'
                (int)Math.Ceiling(popupSize.Width*sx),
                (int)Math.Ceiling(popupSize.Height*sy));
'@.Replace($crlf,$lf)
$new=@'
                DeskSpaceDpiBoundsV195.Pixels(popupSize.Width,sx,screen.Width),
                DeskSpaceDpiBoundsV195.Pixels(popupSize.Height,sy,screen.Height));
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw ("R195 dock physical pixel anchor "+$count)}
$s=$s.Replace($old,$new)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R195: both per-widget DPI readings centralized; popup size bounded before native conversion.'
