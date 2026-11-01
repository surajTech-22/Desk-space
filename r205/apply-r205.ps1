$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r205/DeskSpaceResourceSnapshotV205.cs' -Destination "$root/DeskSpaceResourceSnapshotV205.cs" -Force
$p="$root/DeskSpaceComponentHealthV175.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        result.Add(new("Process resources",
            DeskSpaceHealthSignalsV194.CurrentMemory()));
'@.Replace($crlf,$lf)
$new=@'
        result.Add(new("Process resources",
            DeskSpaceHealthSignalsV194.CurrentMemory()));
        result.Add(new("Process CPU and handles",
            DeskSpaceResourceSnapshotV205.Current()));
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R205 exact resource status anchor expected 1, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R205: truthful lifetime CPU/handle/managed-memory metrics available on demand only.'
