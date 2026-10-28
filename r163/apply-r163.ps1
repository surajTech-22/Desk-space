$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r163/DeskSpaceArchiveSearchV163.cs' "$root/DeskSpaceArchiveSearchV163.cs" -Force
$p="$root/DeskSpaceFilePreviewV85.cs"
$s=[IO.File]::ReadAllText($p)
$old='panel.Children.Add(DeskSpaceArchivePreviewV161.Create(path));'
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){throw 'R163 ZIP preview anchor missing or duplicated'}
$s=$s.Replace($old,'panel.Children.Add(DeskSpaceArchiveSearchV163.Create(path));')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R163: bounded searchable ZIP preview integrated with existing Deck.'
