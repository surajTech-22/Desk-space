$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('DeskSpacePublicUpdaterV129.cs','DeskSpaceUpdateRateV127.cs'))
{
    if(!(Test-Path -LiteralPath "r129/$name")){throw "R129 missing $name"}
}
Copy-Item -LiteralPath 'r129/DeskSpacePublicUpdaterV129.cs' -Destination "$root/DeskSpacePublicUpdaterV44.cs" -Force
Copy-Item -LiteralPath 'r129/DeskSpaceUpdateRateV127.cs' -Destination "$root/DeskSpaceUpdateRateV127.cs" -Force
Write-Host 'R129: persistent 30-minute automatic GitHub API spacing, 403/429 reset/backoff, stage-specific errors; original trusted official GitHub release & SHA-256 unchanged.'
