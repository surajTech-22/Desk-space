$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
if(!(Test-Path -LiteralPath $palette)){throw 'R340 shipping action palette missing'}
Copy-Item 'r340/DeskSpaceSnippetsV340.cs' (Join-Path $root 'DeskSpaceSnippetsV340.cs') -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($palette).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R340 $label expected one match, found $hits"}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        new("filehash","Verify SHA-256 of file",
'@ @'
        new("snippets","Saved text snippets",
            "Manually save, search, pin and copy local snippets without clipboard monitoring","Text"),
        new("filehash","Verify SHA-256 of file",
'@ 'manual saved snippets command'
One @'
            case "filehash":
                DeskSpaceFileVerifyV339.Show(owner);
'@ @'
            case "snippets":
                DeskSpaceSnippetsV340.Show(owner);
                break;
            case "filehash":
                DeskSpaceFileVerifyV339.Show(owner);
'@ 'reuse Action Palette switch'
[IO.File]::WriteAllText($palette,$s,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('DeskSpaceSnippetsV340.Show(owner)')){
 throw 'R340 snippets cannot be invoked from shipping Action Palette'
}
Write-Host 'R340: explicit local saved snippets added to existing DeskSpace Action Palette.'
