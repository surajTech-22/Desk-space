$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$operations=[IO.File]::ReadAllText('r185/DeskSpaceRollbackOpsV185.ps1')
$supervisor=[IO.File]::ReadAllText('r185/DeskSpaceUpdateSupervisorV185.ps1')
if(([regex]::Matches($supervisor,'# BEGIN_R185_OPERATIONS')).Count -ne 1){
    throw 'R185 supervisor helper insertion marker missing or repeated'
}
$supervisor=$supervisor.Replace('# BEGIN_R185_OPERATIONS',$operations)
[IO.File]::WriteAllText("$root/DeskSpaceUpdateSupervisor.ps1",$supervisor,
    [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath 'r185/DeskSpacePostInstallReceiptV185.cs' -Destination "$root/DeskSpacePostInstallReceiptV185.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function One([string]$path,[string]$from,[string]$to,[string]$why)
{
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
    $count=([regex]::Matches($s,[regex]::Escape($from))).Count
    if($count -ne 1){throw "R185 "+$why+": expected one anchor, got "+$count}
    [IO.File]::WriteAllText($path,$s.Replace($from,$to),
        [Text.UTF8Encoding]::new($false))
}
One "$root/WidgetHost.cs" @'
            if(!DeskSpaceStartupRecoveryV183.SafeMode)
                DeskSpaceMultiMonitorV151.Register(this);
'@ @'
            if(!DeskSpaceStartupRecoveryV183.SafeMode)
                DeskSpaceMultiMonitorV151.Register(this);
            // The already-loaded primary acknowledges only the matching
            // local supervised update transaction/version, no content read.
            DeskSpacePostInstallReceiptV185.Ready(this);
'@ 'post-install WPF-ready handshake'
One "$root/DeskSpaceComponentHealthV175.cs" @'
        result.Add(new("Startup recovery",DeskSpaceStartupRecoveryV183.Status));
'@ @'
        result.Add(new("Startup recovery",DeskSpaceStartupRecoveryV183.Status));
        result.Add(new("Update recovery","Whole-install snapshots and WPF-ready receipts enabled; "+
            "real-PC rollback still needs owner acceptance"));
'@ 'report update safety in local diagnostics'
Write-Host 'R185: embedded transactional full-install supervisor, nonce/version WPF receipt and local status.'
