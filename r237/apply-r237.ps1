$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r237/DeskSpaceContextSuggestionsV237.cs' -Destination "$root/DeskSpaceContextSuggestionsV237.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        Add("Clipboard conversions",
'@.Replace($crlf,$lf)
$new=@'
        Add("Context suggestions",
            "Opt in for this window only: suggest existing actions from the app category.",
            ()=>DeskSpaceContextSuggestionsV237.Show(this));
        Add("Clipboard conversions",
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R237 Feature Hub insertion requires one anchor, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R237 context suggestions available inside existing DeskSpace Features hub, opt-in only.'
