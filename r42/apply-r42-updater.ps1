$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source='r42/DeskSpaceUpdateCheckV42.cs'
$target=Join-Path $root 'DeskSpaceUpdateCheckV42.cs'
if(!(Test-Path $source)){throw 'R42 update diagnostics source missing'}
Copy-Item -LiteralPath $source -Destination $target -Force
# Preserve original automatic release logic and update-version build stamp.
# Intercept only USER-CALLED CheckAsync in host and Workspace UI sources.
$count=0
foreach($file in @('MainWindow.xaml.cs','WidgetHost.cs','DeskSpaceModuleV10.cs')){
    $path=Join-Path $root $file
    if(!(Test-Path $path)){continue}
    $s=[IO.File]::ReadAllText($path)
    $key='DeskSpaceUpdater.CheckAsync('
    $found=([regex]::Matches($s,[regex]::Escape($key))).Count
    if($found -eq 0){continue}
    $s=$s.Replace($key,'DeskSpaceUpdateCheckV42.CheckAsync(')
    [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
    $count+=$found
}
if($count -lt 1){
    throw 'R42 no in-app Check for Updates buttons were connected'
}
$helper=[IO.File]::ReadAllText($target)
foreach($guard in @('DeskSpaceUpdater.CurrentVersion',
    'BuildTools","GitHubCLI"','auth","status"','isPrerelease',
    'ProcessStartInfo(Releases)','DeskSpaceUpdater.CheckAsync(owner)')){
    if(!$helper.Contains($guard)){
        throw "R42 new cross-PC update check missing: $guard"
    }
}
Write-Host "R42 update checker linked to $count in-app update buttons; preserved original SHA-256 updater."
