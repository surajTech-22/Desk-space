$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r202/DeskSpaceVaultContextV202.cs' -Destination "$root/DeskSpaceVaultContextV202.cs" -Force
Copy-Item -LiteralPath 'r202/DeskSpaceVaultContextWindowV202.cs' -Destination "$root/DeskSpaceVaultContextWindowV202.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Patch([string]$path,[string]$before,[string]$after,[string]$why)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R202 "+$why+" expected 1 anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),
     [Text.UTF8Encoding]::new($false))
}
Patch "$root/DeskSpaceFeatureHubV199.cs" @'
        Section("Health and recovery");
'@ @'
        Section("Information and privacy");
        Add("Find vault site labels",
            "After manually unlocking, match an HTTPS site to encrypted vault labels without revealing passwords.",
            ()=>DeskSpaceVaultContextWindowV202.Show(this));

        Section("Health and recovery");
'@ 'manual privacy-preserving vault entry in the visible Workspace feature hub'
Patch "$root/DeskSpaceActionPaletteV197.cs" @'
        new("features","DeskSpace Features"
'@ @'
        new("vaultlookup","Vault website labels","Find exact HTTPS website labels after manually unlocking","Privacy"),
        new("features","DeskSpace Features"
'@ 'discoverable local vault action'
Patch "$root/DeskSpaceActionPaletteV197.cs" @'
        switch(command.Id)
        {
            case "features":
'@ @'
        switch(command.Id)
        {
            case "vaultlookup":
                DeskSpaceVaultContextWindowV202.Show(owner);
                break;
            case "features":
'@ 'explicit manual vault lookup dispatch'
Write-Host 'R202: metadata-only vault context lookup, HTTPS exact-host and manual unlock; no background or secret copy.'
