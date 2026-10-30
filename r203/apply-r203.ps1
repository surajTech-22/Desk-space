$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r203/DeskSpaceWorkflowDryRunV203.cs' -Destination "$root/DeskSpaceWorkflowDryRunV203.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Patch([string]$path,[string]$old,[string]$new,[string]$why)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R203 "+$why+" expected 1 anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
}
Patch "$root/DeskSpaceFeatureHubV199.cs" @'
        Add("Reminders","Open the existing reminders tab.",
            ()=>SelectExistingFeatureTabV199("Reminders"));
'@ @'
        Add("Reminders","Open the existing reminders tab.",
            ()=>SelectExistingFeatureTabV199("Reminders"));
        Add("Preview saved workflows",
            "Review steps and conditions without launching any app, URL or file.",
            ()=>DeskSpaceWorkflowDryRunV203.Show(this));
'@ 'visible no-execution workflow review'
Patch "$root/DeskSpaceActionPaletteV197.cs" @'
        new("vaultlookup","Vault website labels"
'@ @'
        new("workflowreview","Preview workflows","Review saved reminder actions without running them","Automation"),
        new("vaultlookup","Vault website labels"
'@ 'Action Palette workflow preview discovery'
Patch "$root/DeskSpaceActionPaletteV197.cs" @'
        switch(command.Id)
        {
            case "vaultlookup":
'@ @'
        switch(command.Id)
        {
            case "workflowreview":
                DeskSpaceWorkflowDryRunV203.Show(owner);
                break;
            case "vaultlookup":
'@ 'manual workflow preview action'
Write-Host 'R203: non-executing workflow preview available in Workspace and Action Palette.'
