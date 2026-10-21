$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r31/DeskSpaceInformationV31.cs' "$root/DeskSpaceInformationV31.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$old,[string]$new) {
    if(!$script:s.Contains($old)){throw "R31 source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
Must 'DeskSpaceInformationV30? informationOverlay' 'DeskSpaceInformationV31? informationOverlay'
Must 'new DeskSpaceInformationV30(this,' 'new DeskSpaceInformationV31(this,'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R31: true partial sub-donut aligned with Information, zero-record centred + New, hit-test through WPF Canvas, no empty arrows; preserving original information and vault.'
