$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r241/DeskSpaceDocxPreviewV241.cs' -Destination "$root/DeskSpaceDocxPreviewV241.cs" -Force
$p="$root/DeskSpaceFilePreviewV85.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
            if(DeskSpaceTextPreviewV240.Supports(extension))
            {
'@.Replace($crlf,$lf)
$new=@'
            if(DeskSpaceDocxPreviewV241.Supports(extension))
            {
                // Read Word XML only: macros, references, attachments are ignored.
                panel.Children.Add(DeskSpaceDocxPreviewV241.Create(path));
            }
            else if(DeskSpaceTextPreviewV240.Supports(extension))
            {
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R241 Word preview integration requires one existing text branch, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R241: bounded DOCX text preview integrated before R240 plaintext and R217 PDF.'
