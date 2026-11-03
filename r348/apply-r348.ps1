$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$file=Join-Path $root 'DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path -LiteralPath $file)){throw 'R348 supervisor not yet assembled'}
$source=[IO.File]::ReadAllText('r348/DeskSpaceSafeStorageCleanupV348.ps1')
$s=[IO.File]::ReadAllText($file)
$anchor='function Report([string]$message) {'
$success="    Report 'R185 SUCCESS: complete prior-install snapshot retained for recovery.'"
if(($s.Split($anchor).Count-1) -ne 1){throw 'R348 expected exactly one supervisor function marker'}
if(($s.Split($success).Count-1) -ne 1){throw 'R348 expected exactly one post-install success marker'}
if($s.Contains('Invoke-R348StorageCleanup')){throw 'R348 already applied; refusing duplicate hook'}
$newline=[Environment]::NewLine
$s=$s.Replace($anchor,$source+$newline+$anchor)
$hook=@'
    # Only after the verified new WPF host has acknowledged this transaction,
    # prune proven-obsolete DeskSpace storage. This maintenance MUST NOT turn
    # a successful update into a failure or trigger application rollback.
    try {
        $storage=Invoke-R348StorageCleanup -UpdatesRoot $root -CurrentRollback $rollback -SearchRoot (Join-Path $env:LOCALAPPDATA 'DeskSpace\Search') -TargetExe $TargetExe
        foreach($message in $storage.Messages){Report ('R348 '+$message)}
    } catch {
        Report ('R348 storage maintenance safely deferred: '+$_.Exception.Message)
    }
'@
$s=$s.Replace($success,$success+$newline+$hook.TrimEnd())
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
$tokens=$null;$errors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile($file,
    [ref]$tokens,[ref]$errors)
if($errors.Count -gt 0){
    throw ('R348 supervisor PowerShell syntax error: '+($errors|Out-String))
}
Write-Host 'R348: guarded post-receipt DeskSpace cache/rollback retention integrated.'
