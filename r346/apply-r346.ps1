$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
if(!(Test-Path -LiteralPath $palette)){
 throw 'R346 shipping Action Palette framework missing'
}
Copy-Item 'r346/DeskSpaceSearchDiskHealthV346.cs' (Join-Path $root 'DeskSpaceSearchDiskHealthV346.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($palette).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R346 $why expected one matching source, got $hits"}
 $script:s=$script:s.Replace($old,$new)
}
# In the canonical shipping source the optional R260 Index Health panel
# is not present; integrate disk health through the already verified Action
# Palette rather than introducing a duplicate source reconstruction.
One @'
        new("filehash","Verify SHA-256 of file",
'@ @'
        new("diskhealth","Search disk cache health",
            "Inspect compact local search cache size and old oversized cache (read only)","System"),
        new("filehash","Verify SHA-256 of file",
'@ 'discoverable disk cache health action'
One @'
            case "filehash":
                DeskSpaceFileVerifyV339.Show(owner);
'@ @'
            case "diskhealth":
                DeskSpaceSearchDiskHealthV346.Show(owner);
                break;
            case "filehash":
                DeskSpaceFileVerifyV339.Show(owner);
'@ 'open read-only cache metadata on demand'
[IO.File]::WriteAllText($palette,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('DeskSpaceSearchDiskHealthV346.Show(owner)')){
 throw 'R346 disk health not accessible from existing Action Palette'
}
Write-Host 'R346: persisted search cache footprint/old-cache warning visible in existing Actions, no index contents read or modified.'
