$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
if(!(Test-Path -LiteralPath $palette)){throw 'R328 shipping command palette source missing'}
Copy-Item 'r328/DeskSpaceTextCompareV328.cs' (Join-Path $root 'DeskSpaceTextCompareV328.cs') -Force
$crlf=[string][char]13+[string][char]10
$lf=[string][char]10
$s=[IO.File]::ReadAllText($palette).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw ("R328 "+$why+" anchor count "+$hits)}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        new("clipboard","Clipboard conversions","Convert selected text with a preview","Text"),
'@ @'
        new("clipboard","Clipboard conversions","Convert selected text with a preview","Text"),
        new("textcompare","Compare two text blocks",
            "Compare original and updated pasted lines locally, with no history","Text"),
'@ 'register one Action Palette command'
One @'
            case "clipboard":
                DeskSpaceClipboardTransformsV177.Show(owner);
                break;
'@ @'
            case "clipboard":
                DeskSpaceClipboardTransformsV177.Show(owner);
                break;
            case "textcompare":
                DeskSpaceTextCompareV328.Show(owner);
                break;
'@ 'manual Action Palette execution'
if(!$s.Contains('DeskSpaceTextCompareV328.Show(owner);') -or
   !$s.Contains('new("textcompare"')){throw 'R328 command not routed to existing palette'}
[IO.File]::WriteAllText($palette,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R328 existing Action Palette now opens manual local text comparison, no new widget or auto clipboard history.'
