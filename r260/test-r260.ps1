$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchIndexHealthV260.cs')
$index=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchIndexV13.cs')
$hub=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFeatureHubV199.cs')
$palette=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceActionPaletteV197.cs')
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'SafeCountsV260()',
 'foreach(var amount in VolumeCounts.Values)',
 'int finished=VolumeComplete.Count(x=>x.Value)',
 'int volumes=VolumeCounts.Count',
 'internal static string Describe(Stats stats)',
 'Content="Start indexing names"',
 'MessageBoxButton.YesNo',
 'DeskSpaceSearchIndexV13.WarmStart();',
 'DeskSpaceSearchIndexHealthV260.Show(this)',
 'DeskSpaceSearchIndexHealthV260.Show(owner)',
 'TestSearchIndexHealthV260();'
)){
 if(!$s.Contains($needle) -and !$index.Contains($needle) -and
    !$hub.Contains($needle) -and !$palette.Contains($needle) -and
    !$tests.Contains($needle))
 {throw ('R260 missing no-content index health safeguard '+$needle)}
}
foreach($forbidden in @('File.Read','File.Write','Directory.Get',
 'Search(','HttpClient','Clipboard.','Task.Run(','DispatcherTimer','Process.Start')){
 if($s.Contains($forbidden))
 {throw ('R260 health dialog must not read content or start implicit scans: '+$forbidden)}
}
Write-Host 'R260 STATIC PASS: aggregate-only search status, explicit local scan and no timer/contents.'
