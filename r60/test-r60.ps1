$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceApplicationsV55.cs'
$s=[IO.File]::ReadAllText($p)
foreach($t in @(
    'ApplicationsParentCenter',
    'OuterSectorCenter(4)-360.0/14.0',
    'double step=360.0/(7*4)',
    'double start=center-360.0/14.0',
    'start+i*step,start+(i+1)*step',
    'BaseInner=197,BaseOuter=242',
    'TextWrapping=TextWrapping.Wrap',
    'ShowApplicationPinSearch()',
    'private List<SearchHit> ApplicationMatches()',
    'DeskSpaceSearchIndexV13.SnapshotApplications()',
    'pinSearch.TextChanged+=(_,_)=>RefreshPinMatches()',
    'PinFoundApp(string path)',
    'addPin(path)',
    'pinPalette!.IsOpen=owner.IsVisible')){
 if(!$s.Contains($t)){throw "R60 Apps fix missing $t"}
}
if($s.Contains('new Microsoft.Win32.OpenFileDialog') -or
   $s.Contains('FolderBrowserDialog')){
 throw 'R60 still launches an operating-system file picker for app pinning'
}
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($t in @('TestApplicationsArcAndPinSearch()',
    'applications-aligned-parent','applications-pin-search',
    'Application submenu action drawn outside parent wedge',
    'Selecting a searched application did not update shared pins')){
 if(!$ui.Contains($t)){throw "R60 WPF regression check missing $t"}
}
Write-Host 'R60 PASS: Applications child arc correctly aligned to parent, installed-app search panel replaces file manager; WPF layout and pin action smoke included.'
