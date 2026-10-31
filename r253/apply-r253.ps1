$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r253/DeskSpaceResourceBudgetV253.cs' -Destination "$root/DeskSpaceResourceBudgetV253.cs" -Force
$p="$root/DeskSpaceComponentHealthV175.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a=@'
        result.Add(new("Process CPU and handles",
            DeskSpaceResourceSnapshotV205.Current()));
'@.Replace($crlf,$lf)
$b=@'
        result.Add(new("Process CPU and handles",
            DeskSpaceResourceSnapshotV205.Current()));
        result.Add(new("Runtime advisory budget",
            DeskSpaceResourceBudgetV253.Current()));
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R253 health status anchor expected one, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
Write-Host 'R253: on-demand runtime memory/handle/thread budget check in existing Component Status.'
