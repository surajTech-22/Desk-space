$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r93/DeskSpaceStableHoverV93.cs' "$r/DeskSpaceStableHoverV93.cs" -Force
$p="$r/DeskSpaceShelfGroupsV88.cs"
$s=[IO.File]::ReadAllText($p)
$s=$s.Replace([string][char]13+[char]10,[string][char]10)
$start=$s.IndexOf('    internal static void AddHoverGroup(StackPanel root,',[StringComparison]::Ordinal)
$end=$s.LastIndexOf(([string][char]10 + '}'),[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){
 throw 'R93 stable-hover integration anchor not found'
}
if(([regex]::Matches($s,[regex]::Escape('internal static void AddHoverGroup('))).Count -ne 1){
 throw 'R93 unexpected duplicate hover methods'
}
$replacement=@'
    internal static void AddHoverGroup(StackPanel root,
        IReadOnlyList<Entry> apps,IReadOnlyList<Entry> windows)
    {
        DeskSpaceStableHoverV93.Add(root,apps,windows);
    }
'@.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$start)+$replacement+$s.Substring($end)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R93 external anchored app and window flyouts; deck measurement stable.'
