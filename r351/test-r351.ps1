$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot/../r348/DeskSpaceSafeStorageCleanupV348.ps1"
. "$PSScriptRoot/DeskSpaceStagedInstallerRetentionV351.ps1"
function Assert-R351([bool]$success,[string]$why){
    if(!$success){throw ('R351 TEST FAILED: '+$why)}
}
$before=$env:LOCALAPPDATA
$temp=Join-Path $env:TEMP ('r351-storage-fixture-'+[guid]::NewGuid().ToString('N'))
try {
    $env:LOCALAPPDATA=$temp
    $updates=Join-Path $temp 'DeskSpace\Updates'
    $exe=Join-Path $temp 'Programs\DeskSpace\DeskSpace.exe'
    New-Item -ItemType Directory -Force $updates,(Split-Path -Parent $exe)|Out-Null
    [IO.File]::WriteAllText($exe,'fixture exe')
    $script:otherProcess=$false
    function Get-CimInstance {
        [CmdletBinding()]
        param([string]$ClassName,[string]$Filter)
        if($script:otherProcess){
            return @([pscustomobject]@{ExecutablePath='C:\Unknown\DeskSpace.exe';ProcessId=110})
        }
        return @([pscustomobject]@{ExecutablePath=$script:fixtureExe;ProcessId=111})
    }
    $script:fixtureExe=$exe
    $names=@('tester-v1.5.1125','tester-v1.5.1124','v1.5.1124',
        'tester-v1.5.1119','v1.5.1117','tester-v1.5.1110')
    foreach($name in $names){
        $dir=Join-Path $updates $name
        New-Item -ItemType Directory -Force $dir|Out-Null
        [IO.File]::WriteAllBytes((Join-Path $dir 'DeskSpace_Setup.exe'),
            (New-Object byte[] 1024))
        $item=Get-Item -LiteralPath $dir -Force
        $item.LastWriteTimeUtc=[DateTime]::UtcNow.AddDays(-12)
    }
    $partial=Join-Path $updates 'tester-v1.5.1110\DeskSpace_Setup.exe.11111111111111111111111111111111.partial'
    [IO.File]::WriteAllBytes($partial,(New-Object byte[] 128))
    (Get-Item (Join-Path $updates 'tester-v1.5.1110')).LastWriteTimeUtc=
        [DateTime]::UtcNow.AddDays(-12)
    $custom=Join-Path $updates 'tester-v1.5.1101'
    New-Item -ItemType Directory -Force $custom|Out-Null
    [IO.File]::WriteAllText((Join-Path $custom 'custom-data.txt'),'do not touch')
    (Get-Item $custom).LastWriteTimeUtc=[DateTime]::UtcNow.AddDays(-20)
    $manual=Join-Path $temp 'DeskSpace\ManualBuilds\important-source'
    New-Item -ItemType Directory -Force $manual|Out-Null
    [IO.File]::WriteAllText((Join-Path $manual 'source.cs'),'source')
    $recovery=Join-Path $updates 'recovery-v1.5.734'
    New-Item -ItemType Directory -Force $recovery|Out-Null
    [IO.File]::WriteAllText((Join-Path $recovery 'DeskSpace_Setup.exe'),'recovery')

    $args=@{UpdatesRoot=$updates;CurrentExe=$exe}
    $post=Join-Path $updates 'PostInstall'
    New-Item -ItemType Directory -Force $post|Out-Null
    [IO.File]::WriteAllText((Join-Path $post 'pending.json'),'{}')
    $blocked=Invoke-R351StagedInstallerRetention @args
    Assert-R351 ($blocked.Bytes -eq 0) 'active update allowed deletion'
    Remove-Item (Join-Path $post 'pending.json') -Force

    $preview=Invoke-R351StagedInstallerRetention @args -Preview
    Assert-R351 ($preview.Bytes -eq 0) 'preview mutates cache'
    Assert-R351 (Test-Path (Join-Path $updates 'v1.5.1117')) 'preview deleted prior stage'

    $script:otherProcess=$true
    $foreign=Invoke-R351StagedInstallerRetention @args
    Assert-R351 ($foreign.Bytes -eq 0 -and (Test-Path (Join-Path $updates 'v1.5.1117'))) 'foreign running DeskSpace not protected'
    $script:otherProcess=$false

    $result=Invoke-R351StagedInstallerRetention @args
    Assert-R351 ($result.Removed -eq 3) 'not exactly three old staged installer folders'
    Assert-R351 ($result.Bytes -eq (3*1024+128)) 'reclaimed staged bytes incorrect'
    Assert-R351 (!(Test-Path (Join-Path $updates 'tester-v1.5.1110'))) 'old partial remnants not cleaned'
    foreach($name in @('tester-v1.5.1125','tester-v1.5.1124','v1.5.1124')){
        Assert-R351 (Test-Path (Join-Path $updates $name)) ('protected rollback installer '+$name)
    }
    Assert-R351 (Test-Path $custom) 'custom notes under updater staging deleted'
    Assert-R351 (Test-Path $recovery) 'manual recovery installer deleted'
    Assert-R351 (Test-Path $manual) 'manual build user source deleted'
    $again=Invoke-R351StagedInstallerRetention @args
    Assert-R351 ($again.Bytes -eq 0 -and $again.Removed -eq 0) 'cleanup not idempotent'
    $wrong=$false
    try {
        [void](Invoke-R351StagedInstallerRetention -UpdatesRoot
            (Join-Path $temp 'Other\Updates') -CurrentExe $exe)
    }catch{$wrong=$true}
    Assert-R351 $wrong 'wrong root accepted'
    Write-Host 'R351 PASS: pending update, foreign process, preview, old installers, partial downloads, custom/recovery/manual protections, scope and idempotence.'
}finally {
    $env:LOCALAPPDATA=$before
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
