$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot/DeskSpaceSafeStorageCleanupV348.ps1"
function Assert-R348([bool]$ok,[string]$message) {
    if(!$ok){throw ('R348 TEST FAILED: '+$message)}
}
$original=$env:LOCALAPPDATA
$testing=Join-Path $env:TEMP ('r348-test-'+[guid]::NewGuid().ToString('N'))
$script:otherProcess=$false
try {
    $env:LOCALAPPDATA=$testing
    $root=Join-Path $testing 'DeskSpace\Updates'
    $rb=Join-Path $root 'Rollback'
    $search=Join-Path $testing 'DeskSpace\Search'
    $fakeExe=Join-Path $testing 'Programs\DeskSpace\DeskSpace.exe'
    $script:fakeExe=$fakeExe
    foreach($path in @($rb,$search,(Split-Path -Parent $fakeExe))){
        New-Item -ItemType Directory -Force -Path $path|Out-Null
    }
    [IO.File]::WriteAllText($fakeExe,'test executable marker')
    $snapshots=@()
    for($i=0;$i -lt 6;$i++){
        $d=Join-Path $rb ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path (Join-Path $d 'snapshot')|Out-Null
        [IO.File]::WriteAllText((Join-Path $d 'snapshot\DeskSpace.exe'),'snapshot')
        [IO.File]::WriteAllText((Join-Path $d 'verified-manifest.json'),
            '[{"Path":"DeskSpace.exe","Sha":"test-hash","Size":8}]')
        $item=Get-Item -LiteralPath $d -Force
        $item.CreationTimeUtc=[DateTime]::UtcNow.AddDays(-15+$i)
        $snapshots+=,$d
    }
    $current=$snapshots[5]
    # Failed installations are never purged, even when old.
    $failed=Join-Path $rb ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path (Join-Path $failed 'snapshot')|Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $failed 'failed-install')|Out-Null
    [IO.File]::WriteAllText((Join-Path $failed 'snapshot\DeskSpace.exe'),'failed')
    [IO.File]::WriteAllText((Join-Path $failed 'verified-manifest.json'),
        '[{"Path":"DeskSpace.exe","Sha":"hash"}]')
    (Get-Item -LiteralPath $failed -Force).CreationTimeUtc=[DateTime]::UtcNow.AddDays(-30)
    # A non-transaction folder must remain untouched.
    $notOwned=Join-Path $rb 'CustomerBackup'
    New-Item -ItemType Directory -Force -Path $notOwned|Out-Null
    [IO.File]::WriteAllText((Join-Path $notOwned 'keep.txt'),'keep')

    $header=New-Object byte[] 8192
    $magic=[Text.Encoding]::ASCII.GetBytes("SQLite format 3")
    [Array]::Copy($magic,$header,$magic.Length)
    [IO.File]::WriteAllBytes((Join-Path $search 'filenames-v336.sqlite'),$header)
    [IO.File]::WriteAllBytes((Join-Path $search 'filenames-v330.sqlite'),
        (New-Object byte[] 32768))
    [IO.File]::WriteAllBytes((Join-Path $search 'filenames-v330.sqlite-wal'),
        (New-Object byte[] 2048))
    [IO.File]::WriteAllText((Join-Path $search 'unrelated.txt'),'preserve user data')
    function Get-CimInstance {
        [CmdletBinding()]
        param([string]$ClassName,[string]$Filter)
        $instances=@([pscustomobject]@{ExecutablePath=$script:fakeExe;ProcessId=100})
        if($script:otherProcess){
            $instances+=,[pscustomobject]@{
                ExecutablePath='C:\Other\DeskSpace.exe';ProcessId=101
            }
        }
        return $instances
    }
    $common=@{UpdatesRoot=$root;CurrentRollback=$current;SearchRoot=$search;TargetExe=$fakeExe}
    # The pending update gate must protect ALL caches and backups.
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'PostInstall')|Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'PostInstall\pending.json'),'{}')
    $r=Invoke-R348StorageCleanup @common
    Assert-R348 ($r.ReclaimedBytes -eq 0 -and $r.SnapshotsDeleted -eq 0) 'pending update mutated storage'
    Assert-R348 (Test-Path (Join-Path $search 'filenames-v330.sqlite')) 'pending update deleted v330'
    Remove-Item (Join-Path $root 'PostInstall\pending.json') -Force

    $preview=Invoke-R348StorageCleanup @common -Preview
    Assert-R348 ($preview.ReclaimedBytes -eq 0) 'preview changed bytes'
    Assert-R348 (Test-Path -LiteralPath $snapshots[0]) 'preview deleted snapshot'
    Assert-R348 (Test-Path (Join-Path $search 'filenames-v330.sqlite')) 'preview deleted index'

    # Unknown second instance blocks v330 cleanup, including sidecars.
    $script:otherProcess=$true
    $blocked=Invoke-R348StorageCleanup @common
    Assert-R348 (Test-Path (Join-Path $search 'filenames-v330.sqlite')) 'other instance cache was deleted'
    Assert-R348 (Test-Path (Join-Path $search 'filenames-v330.sqlite-wal')) 'other instance sidecar was deleted'
    $script:otherProcess=$false

    # Locked legacy files cannot be deleted by another process.
    $locked=[IO.File]::Open((Join-Path $search 'filenames-v330.sqlite'),
        [IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
    try {
        $lockedRun=Invoke-R348StorageCleanup @common
        Assert-R348 (Test-Path (Join-Path $search 'filenames-v330.sqlite')) 'open SQLite cache was deleted'
    } finally {$locked.Dispose()}

    $final=Invoke-R348StorageCleanup @common
    Assert-R348 ($final.LegacyIndexBytes -eq 34816) 'legacy size/count incorrect'
    Assert-R348 (!(Test-Path (Join-Path $search 'filenames-v330.sqlite'))) 'obsolete legacy index not cleaned'
    Assert-R348 (!(Test-Path (Join-Path $search 'filenames-v330.sqlite-wal'))) 'obsolete WAL not cleaned'
    Assert-R348 (Test-Path (Join-Path $search 'filenames-v336.sqlite')) 'current index removed'
    Assert-R348 (Test-Path (Join-Path $search 'unrelated.txt')) 'unrelated file deleted'
    Assert-R348 (Test-Path -LiteralPath $current) 'current transaction deleted'
    Assert-R348 (Test-Path -LiteralPath $failed) 'failed-install recovery deleted'
    Assert-R348 (Test-Path -LiteralPath $notOwned) 'nontransaction folder deleted'
    # Six transactions (one current) leave three verified prior backups, so
    # at least two of the five prior verified backups are pruned in total.
    $survivors=@($snapshots|Where-Object {Test-Path -LiteralPath $_})
    Assert-R348 ($survivors.Count -eq 4) 'did not preserve exactly current+3 valid rollback snapshots'
    # Second invocation is idempotent and cannot erase retained snapshots.
    $again=Invoke-R348StorageCleanup @common
    Assert-R348 ($again.ReclaimedBytes -eq 0 -and $again.SnapshotsDeleted -eq 0) 'second cleanup was not idempotent'
    $bad=@{UpdatesRoot=(Join-Path $testing 'Other');CurrentRollback=$current;SearchRoot=$search;TargetExe=$fakeExe}
    $blockedScope=$false
    try {[void](Invoke-R348StorageCleanup @bad)} catch {$blockedScope=$true}
    Assert-R348 $blockedScope 'incorrect updates root not rejected'
    Write-Host 'R348 PASS: pending, dry-run, multiple instances, locked SQLite, current v336, recovery backups, paths, idempotence.'
} finally {
    $env:LOCALAPPDATA=$original
    if(Test-Path -LiteralPath $testing){
        Remove-Item -LiteralPath $testing -Force -Recurse -ErrorAction SilentlyContinue
    }
}
