$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r261/DeskSpaceAppGroupOrderV261.cs' -Destination "$root/DeskSpaceAppGroupOrderV261.cs" -Force
$p="$root/DeskSpaceShelfGroupsV88.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why){
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R261 "+$why+" expected one anchor, found "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
    internal static IReadOnlyList<Entry> GroupVisible(IEnumerable<Entry> windows)=>
        windows.Where(x=>!string.IsNullOrWhiteSpace(x.Name)).
            GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).
            Select(g=>g.First()).
            OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).
            Take(24).ToArray();
'@ @'
    internal static IReadOnlyList<Entry> GroupVisible(IEnumerable<Entry> windows)=>
        DeskSpaceAppGroupOrderV261.Groups(windows);
'@ 'multi-window first, single-window alphabetic'
Swap @'
    internal static IReadOnlyList<Entry> WindowsFor(Entry app,IReadOnlyList<Entry> windows)=>
        windows.Where(x=>x.Name.Equals(app.Name,
            StringComparison.OrdinalIgnoreCase)).
            GroupBy(x=>x.Handle).Select(x=>x.First()).ToArray();
'@ @'
    internal static IReadOnlyList<Entry> WindowsFor(Entry app,IReadOnlyList<Entry> windows)=>
        DeskSpaceAppGroupOrderV261.Windows(app,windows);
'@ 'deterministic per-window ordering for flyouts'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R261: running apps groups ordered by window count (multi-first) and alphabetic singles.'
