$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
if(!(Test-Path $palette)){throw 'R344 Action Palette shipping file missing'}
Copy-Item 'r344/DeskSpaceScreenTextV344.cs' (Join-Path $root 'DeskSpaceScreenTextV344.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($palette).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R344 $reason expected one anchor, found $count"}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        new("imageconvert","Safe batch image conversion",
'@ @'
        new("screentext","Extract screen text (offline)",
            "Choose a monitor and rectangle, review Windows OCR, manually copy result","Text"),
        new("imageconvert","Safe batch image conversion",
'@ 'add on-demand OCR command'
One @'
            case "imageconvert":
                DeskSpaceImageConvertV341.Show(owner);
'@ @'
            case "screentext":
                DeskSpaceScreenTextV344.Show(owner);
                break;
            case "imageconvert":
                DeskSpaceImageConvertV341.Show(owner);
'@ 'route explicit screen OCR'
[IO.File]::WriteAllText($palette,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('DeskSpaceScreenTextV344.Show(owner)')){
 throw 'R344 OCR is not accessible from existing Action Palette'
}
Write-Host 'R344: local Windows OCR screen text extractor, explicit selection, no passive capture.'
