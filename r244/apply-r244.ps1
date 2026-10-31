$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r244/DeskSpaceFileDuplicatesV244.cs' -Destination "$root/DeskSpaceFileDuplicatesV244.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        Section("Windows and workspace");
'@.Replace($crlf,$lf)
$new=@'
        Section("Files");
        Add("Duplicate-file inspector",
            "Select one folder to find matching SHA-256 files, without deleting or moving anything.",
            ()=>DeskSpaceFileDuplicatesV244.Show(this));

        Section("Windows and workspace");
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R244 Features integration expected exactly one anchor; got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R244: duplicate inspector available from Features, on-demand only.'
