$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r260/DeskSpaceSearchIndexHealthV260.cs' -Destination "$root/DeskSpaceSearchIndexHealthV260.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$a,[string]$b,[string]$why)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R260 "+$why+": expected one anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($a,$b),
    [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceSearchIndexV13.cs" @'
    public static string Coverage
'@ @'
    // Aggregate-only process-index counters: never disclose which drive,
    // file, directory or user account was indexed in health diagnostics.
    internal static DeskSpaceSearchIndexHealthV260.Stats SafeCountsV260()
    {
        long names=0;
        foreach(var amount in VolumeCounts.Values)
        {
            if(amount<0)continue;
            names=amount>long.MaxValue-names?
                long.MaxValue:names+amount;
        }
        int volumes=VolumeCounts.Count;
        int finished=VolumeComplete.Count(x=>x.Value);
        return new(IsIndexing,volumes,Math.Min(finished,volumes),names);
    }
    public static string Coverage
'@ 'indexed drive count without exposing paths'
Swap "$root/DeskSpaceFeatureHubV199.cs" @'
        Add("Component status",
'@ @'
        Add("Search index health",
            "Review aggregate multi-drive indexing progress without showing paths or file contents.",
            ()=>DeskSpaceSearchIndexHealthV260.Show(this));
        Add("Component status",
'@ 'visible Search index health entry in Workspace'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        new("status","Component status",
'@ @'
        new("searchhealth","Search index health",
            "Review indexed name counts and optionally start local indexing","System"),
        new("status","Component status",
'@ 'discoverable group-filtered command'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        switch(command.Id)
        {
'@ @'
        switch(command.Id)
        {
            case "searchhealth":
                DeskSpaceSearchIndexHealthV260.Show(owner);
                break;
'@ 'explicit manual index-status command dispatch'
Write-Host 'R260: aggregate multi-volume search status and opt-in indexing in Workspace and Action Palette.'
