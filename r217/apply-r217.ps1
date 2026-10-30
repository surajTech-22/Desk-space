$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$p="$root/DeskSpaceFilePreviewV85.cs"
Copy-Item -LiteralPath 'r217/DeskSpacePdfPreviewV217.cs' -Destination "$root/DeskSpacePdfPreviewV217.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
            if(extension==".zip")
            {
'@.Replace($crlf,$lf)
$new=@'
            if(extension==".pdf")
            {
                // Windows native PDF rendering is strictly on demand.
                // Large/linked PDFs receive an explanatory placeholder.
                panel.Children.Add(DeskSpacePdfPreviewV217.Create(path));
            }
            else if(extension==".zip")
            {
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R217 file preview hook expected one anchor, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R217: PDF previews display bounded native Windows-rendered pages inside existing on-demand Deck preview.'
