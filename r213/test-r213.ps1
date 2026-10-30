$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R213: exercise the REAL R185 backup/restore functions against a fake
# installation in this runner's temporary directory. Never execute the
# supervisor's installer/process-control statements or use user workspace data.
$supervisor='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path -LiteralPath $supervisor -PathType Leaf)){
    throw 'R213: verified original update supervisor is unavailable'
}
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path $supervisor).Path,[ref]$tokens,[ref]$errors)
if($errors.Count -ne 0){throw 'R213: supervisor cannot be parsed safely'}
$allowed=@('Assert-R185InstallScope','Get-R185Inventory',
           'Save-R185Snapshot','Restore-R185Snapshot')
foreach($name in $allowed){
    $matches=@($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq $name
    },$false))
    if($matches.Count -ne 1){
        throw ("R213: R185 rollback function missing/ambiguous: "+$name)
    }
    # Import only the named declarations. The installer main body is
    # deliberately never run, and cannot access a real DeskSpace process.
    . ([scriptblock]::Create($matches[0].Extent.Text))
}
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r213-'+
    [guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'DeskSpace'
$original=Join-Path $install 'DeskSpace.exe'
$plugin=Join-Path $install 'plugins\display.dll'
$config=Join-Path $install 'appsettings.json'
$snapshot=Join-Path $root 'rollback1\snapshot'
$quarantine=Join-Path $root 'rollback1\failed-install'
$snapshot2=Join-Path $root 'rollback2\snapshot'
$quarantine2=Join-Path $root 'rollback2\failed-install'
try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $plugin) -Force|
        Out-Null
    [IO.File]::WriteAllText($original,'synthetic version OLD 1.5.0')
    [IO.File]::WriteAllText($plugin,'synthetic old plugin')
    [IO.File]::WriteAllText($config,'{"mode":"old"}')
    $count=Save-R185Snapshot $install $snapshot
    if($count -ne 3){throw 'R213: snapshot must contain the full installation'}
    $manifest=Join-Path (Split-Path -Parent $snapshot) 'verified-manifest.json'
    if(!(Test-Path -LiteralPath $manifest -PathType Leaf)){
        throw 'R213: verified inventory manifest was not created'
    }
    [IO.File]::WriteAllText($original,'synthetic version NEW 1.5.9')
    [IO.File]::WriteAllText($plugin,'synthetic new plugin')
    [IO.File]::WriteAllText((Join-Path $install 'additional.dll'),
        'synthetic newly installed file')
    $restored=Restore-R185Snapshot $install $snapshot $quarantine
    if($restored -ne 3 -or
       [IO.File]::ReadAllText($original) -ne 'synthetic version OLD 1.5.0' -or
       [IO.File]::ReadAllText($plugin) -ne 'synthetic old plugin' -or
       [IO.File]::ReadAllText($config) -ne '{"mode":"old"}' -or
       (Test-Path -LiteralPath (Join-Path $install 'additional.dll'))){
        throw 'R213: full-install restoration did not recover exact previous bytes'
    }
    if(!(Test-Path -LiteralPath (Join-Path $quarantine 'additional.dll')) -or
       [IO.File]::ReadAllText((Join-Path $quarantine 'DeskSpace.exe')) -ne
       'synthetic version NEW 1.5.9'){
        throw 'R213: failed/new install must be quarantined, not deleted'
    }
    Write-Host 'R213 PASS: full old EXE/plugin/config restored; failed install quarantined.'
    # A changed snapshot must fail before moving any current installation.
    [void](Save-R185Snapshot $install $snapshot2)
    [IO.File]::WriteAllText((Join-Path $snapshot2 'plugins\display.dll'),
        'maliciously altered backup')
    [IO.File]::WriteAllText($original,'current install must survive failed restore')
    $blocked=$false
    try { [void](Restore-R185Snapshot $install $snapshot2 $quarantine2) }
    catch {
        if($_.Exception.Message -like '*backup was changed*'){
            $blocked=$true
        }else{throw}
    }
    if(!$blocked -or (Test-Path -LiteralPath $quarantine2) -or
       [IO.File]::ReadAllText($original) -ne
       'current install must survive failed restore'){
        throw 'R213: tampered rollback changed the current installation'
    }
    Write-Host 'R213 PASS: snapshot tampering blocks restore without altering current files.'
    $unsafe=$false
    try { [void](Assert-R185InstallScope (
        Join-Path $env:LOCALAPPDATA 'DeskSpace')) }
    catch {
        if($_.Exception.Message -like '*local DeskSpace data directory*'){
            $unsafe=$true
        }else{throw}
    }
    if(!$unsafe){throw 'R213: local app data must never be an installation target'}
    Write-Host 'R213 PASS: user workspace storage excluded from backup/restore operations.'
}
finally {
    # Deletes only the GUID-owned synthetic test root. The runner holds
    # no access to a user's installed DeskSpace or personal files.
    if(Test-Path -LiteralPath $root){
        Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction Stop
    }
}
