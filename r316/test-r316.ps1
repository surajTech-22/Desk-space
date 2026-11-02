$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$u=[IO.File]::ReadAllText((Join-Path $root 'DeskSpacePublicUpdaterV44.cs'))
$p=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceUpdateSupervisor.ps1'))
foreach($check in @(
    'handoffNonceV316=Guid.NewGuid().ToString("N")',
    '"-AcceptPath",handoffAckV316',
    '"-AcceptNonce",handoffNonceV316',
    'if(File.Exists(handoffAckV316)',
    'if(supervisorV316.HasExited)break;',
    'if(!acceptedV316)',
    'throw new SupervisorBusyV310Exception();',
    'DeskSpaceVerifiedUpdateQueueV191.Clear(pending);'
)){
    if(!$u.Contains($check)){throw "R316 missing C# source guard: $check"}
}
if($u.Contains('DeskSpace update in progress",MessageBoxButton')){
    throw 'R316 repeated busy modal dialog was not removed'
}
if($u.IndexOf('if(!acceptedV316)') -gt $u.IndexOf('Log("R316 "+tag+" accepted')){
    throw 'R316 marks install accepted before checking acknowledgement'
}
$lock=$p.IndexOf('$ownsMutex=$mutex.WaitOne(0)')
$reject=$p.IndexOf('if(!$ownsMutex){')
$ack=$p.IndexOf('WriteAllText($expected,$AcceptNonce')
$exit=$p.IndexOf('$remaining=@(Get-R230InstalledPeers $original)')
if($lock -lt 0 -or $reject -lt $lock -or $ack -lt $reject -or $exit -lt $ack){
    throw 'R316 acknowledgement could be written without exclusive mutex or after app shutdown'
}
if(!$p.Contains('^[0-9a-f]{32}$') -or !$p.Contains('OrdinalIgnoreCase')){
    throw 'R316 nonced, directory-scoped acknowledgement validation absent'
}
$tokens=$null
$syntaxErrors=$null
[void][System.Management.Automation.Language.Parser]::ParseInput($p,[ref]$tokens,[ref]$syntaxErrors)
if(@($syntaxErrors).Count -gt 0){
    throw ('R316 supervisor PowerShell syntax error: '+($syntaxErrors|Out-String))
}
$tokens=$null;$syntaxErrors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $root 'DeskSpaceUpdateSupervisor.ps1'),[ref]$tokens,[ref]$syntaxErrors)
if(@($syntaxErrors).Count -gt 0){throw 'R316 shipping supervisor parse failed'}
Write-Host 'R316 PASS: supervisor ownership acknowledgement is scoped, verified before handoff, and non-blocking.'