$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
if(!(Test-Path -LiteralPath $palette)){throw 'R341 shipping palette missing'}
Copy-Item 'r341/DeskSpaceImageConvertV341.cs' (Join-Path $root 'DeskSpaceImageConvertV341.cs') -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($palette).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R341 $label expected 1 anchor; got $hits"}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        new("duplicatefiles","Inspect duplicate files",
'@ @'
        new("imageconvert","Safe batch image conversion",
            "Preview PNG/JPG outputs, no overwrite or modification of originals","Files"),
        new("duplicatefiles","Inspect duplicate files",
'@ 'add image conversion to existing action commands'
One @'
            case "duplicatefiles":
                DeskSpaceDuplicateInspectorV339.Show(owner);
'@ @'
            case "imageconvert":
                DeskSpaceImageConvertV341.Show(owner);
                break;
            case "duplicatefiles":
                DeskSpaceDuplicateInspectorV339.Show(owner);
'@ 'route manual conversion command'
[IO.File]::WriteAllText($palette,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('DeskSpaceImageConvertV341.Show(owner)')){
 throw 'R341 no shipping UI route for image converter'
}
Write-Host 'R341: previewed no-overwrite bounded image conversion in existing Action Palette.'
