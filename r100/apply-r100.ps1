$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r100/DeskSpaceWindowPreviewV100.cs' "$r/DeskSpaceWindowPreviewV100.cs" -Force
# R93 and R99 sources were copied by their earlier stages and are the
# current source-of-truth for app and file preview integration.
$p="$r/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$before='    private bool previewOnHover;'
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw "R100 preview-default anchor expected once, found $n"}
$s=$s.Replace($before,'    private bool previewOnHover=true;')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R100: stationary 650ms hover for file and running app previews, real app window bitmap when available, no overlays during cursor travel.'
