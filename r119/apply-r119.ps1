$ErrorActionPreference='Stop'
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs'
$s=[IO.File]::ReadAllText($path)
$old=@'
                var selected=SelectedOriginV116(app,entry);
                if(selected.HasValue)
'@
$new=@'
                // Force static call and nullable tuple type. Calling with
                // dynamic Excel COM otherwise boxes the nullable tuple into
                // ValueTuple<int,int>; that does not have HasValue.
                (int Row,int Column)? selected=
                    SelectedOriginV116((object)app,entry);
                if(selected.HasValue)
'@
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=$s.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old.Replace($crlf,$lf)))).Count
if($n -ne 1){throw "R119 expected one selection call, got $n"}
[IO.File]::WriteAllText($path,$s.Replace($old.Replace($crlf,$lf),$new.Replace($crlf,$lf)),[Text.UTF8Encoding]::new($false))
Write-Host 'R119: Excel selected-origin HasValue now uses statically bound nullable tuple.'
