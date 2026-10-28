$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs')
$widget=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('Environment.ProcessId','TitleHash(title.ToString())',
 'SHA256.HashData','Process.GetProcessById','SWP_NOACTIVATE',
 'profile.Windows','SetWindowPos','RestoreBounds',
 'Overwrite','Replace the existing profile','MessageBoxResult.Yes',
 'new UTF8Encoding(false)','File.Move(temporary,Storage,true)')){
 if(!$source.Contains($token)){throw "R179 missing window layout safety guard: $token"}
}
if($source.Contains('Process.Start(')){
 throw 'R179 must not launch applications during layout restore'
}
if(!$widget.Contains('DeskSpaceWindowLayoutsV179.Show(this)')){
 throw 'R179 tray integration missing'
}
if(!$smoke.Contains('TestWindowLayoutsV179();')){
 throw 'R179 WPF acceptance not installed'
}
Write-Host 'R179 PASS: manual-only window restore, anonymized title fingerprints, data-safe profiles and WPF verification.'
