$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r240/DeskSpaceTextPreviewV240.cs' -Destination "$root/DeskSpaceTextPreviewV240.cs" -Force
$p="$root/DeskSpaceFilePreviewV85.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
            if(extension==".pdf")
            {
'@.Replace($crlf,$lf)
$new=@'
            if(DeskSpaceTextPreviewV240.Supports(extension))
            {
                // Read only a bounded excerpt after the user opens a Deck
                // preview. Code is rendered as literal text, never executed.
                panel.Children.Add(DeskSpaceTextPreviewV240.Create(path));
            }
            else if(extension==".pdf")
            {
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R240 text preview hook expected exactly one PDF branch anchor, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R240: on-demand, 2 MiB bounded text/code excerpt preview integrated before existing PDF/ZIP branches.'
