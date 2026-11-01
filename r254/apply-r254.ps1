$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r254/DeskSpaceBatchRenamePreviewV254.cs' -Destination "$root/DeskSpaceBatchRenamePreviewV254.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a=@'
        Section("Windows and workspace");
'@.Replace($crlf,$lf)
$b=@'
        Add("Batch rename preview",
            "Preview prefix/suffix filename changes and collision errors. Never renames files.",
            ()=>DeskSpaceBatchRenamePreviewV254.Show(this));

        Section("Windows and workspace");
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R254 feature entry anchor expected one, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
Write-Host 'R254: batch filename preview linked to existing Features file tools.'
