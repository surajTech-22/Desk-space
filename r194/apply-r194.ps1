$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r194/DeskSpaceHealthSignalsV194.cs' -Destination "$root/DeskSpaceHealthSignalsV194.cs" -Force
$p="$root/DeskSpaceComponentHealthV175.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='        result.Add(new("File search","Index health not yet independently tested"));'
$new=@'
        // The index already exposes its volume progress. Read that status
        // only on demand; never enumerate files or start new scans here.
        result.Add(new("File search",DeskSpaceHealthSignalsV194.Search(
            DeskSpaceSearchIndexV13.IsIndexing,
            DeskSpaceSearchIndexV13.Coverage)));
        result.Add(new("Process resources",
            DeskSpaceHealthSignalsV194.CurrentMemory()));
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R194 existing component health anchor expected 1, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R194: real search index state and process working memory, on-demand with no data collection.'
