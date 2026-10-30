$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r198/DeskSpaceTextConversionsV198.cs' -Destination "$root/DeskSpaceTextConversionsV198.cs" -Force
$p="$root/DeskSpaceClipboardTransformsV177.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$reason)
{
  $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
  $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
  if($count -ne 1){throw ("R198 "+$reason+": expected 1 anchor, got "+$count)}
  $script:s=$script:s.Replace($before,$after)
}
Swap @'
        "Plain text","Markdown literal","JSON string","JSON object","Markdown link"
'@ @'
        "Plain text","Markdown literal","JSON string","JSON object","Markdown link",
        "Format JSON","CSV to Markdown table","Unique lines"
'@ 'add three conversions to existing approved selection'
Swap @'
            "Markdown link"=>MarkdownLink(text),
'@ @'
            "Markdown link"=>MarkdownLink(text),
            "Format JSON" or "CSV to Markdown table" or "Unique lines"=>
                DeskSpaceTextConversionsV198.Convert(text,mode),
'@ 'safe bounded transformations'
Swap @'
        "JSON string" or "JSON object"=>".json",
        "Markdown literal" or "Markdown link"=>".md",
'@ @'
        "JSON string" or "JSON object" or "Format JSON"=>".json",
        "Markdown literal" or "Markdown link" or "CSV to Markdown table"=>".md",
'@ 'safe explicit Save As extension'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R198: bounded CSV table to Markdown, pretty JSON, unique lines in existing explicit clipboard preview; no automatic read/paste.'
