$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r230/DeskSpaceGracefulExitV230.cs' -Destination "$root/DeskSpaceGracefulExitV230.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$reason)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R230 "+$reason+" expected one anchor, got "+$count)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
# Attach one scoped IPC listener to the main Workspace process.
Swap "$root/MainWindow.xaml.cs" @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
'@ @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
        // Register once per process, covering Workspace windows even when
        // a separate widget/tray process is active.
        DeskSpaceGracefulExitV230.Attach(this);
'@ 'Workspace graceful shutdown subscription'
# The standalone widget host has its own process, monitor replicas do not.
Swap "$root/WidgetHost.cs" @'
            DeskSpacePostInstallReceiptV185.ReadyAfterStabilityV224(this);
'@ @'
            DeskSpacePostInstallReceiptV185.ReadyAfterStabilityV224(this);
            DeskSpaceGracefulExitV230.Attach(this);
'@ 'widget/tray process graceful shutdown subscription'
$updater="$root/DeskSpacePublicUpdaterV44.cs"
# Old hard peer gates are now replaced by a post-download, out-of-process
# supervisor that gracefully closes the entire installed application set.
$s=[IO.File]::ReadAllText($updater).Replace($crlf,$lf)
$blocked='if(DeferForOpenDeskSpace(owner,automatic))return;'
$count=([regex]::Matches($s,[regex]::Escape($blocked))).Count
if($count -ne 2){throw ("R230 expected two old install-only deferrals, got "+$count)}
$s=$s.Replace($blocked,'Log("R230 verified installer ready; independent supervisor will request normal exit of all DeskSpace windows.");')
$queued=@'
            if(DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(target))
            {
                if(!automatic)MessageBox.Show(owner,
                    "DeskSpace update is verified and waiting. Save work and " +
                    "close the other DeskSpace Workspace normally. The widget " +
                    "will retry installation automatically without downloading again.",
                    "DeskSpace update waiting",MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }
'@.Replace($crlf,$lf)
if(([regex]::Matches($s,[regex]::Escape($queued))).Count -ne 1){
    throw 'R230 queued verified installer peer defer hook missing'
}
$s=$s.Replace($queued,@'
            // R230: verified update is already cached. Do not ask the user
            // to close every window manually: the independent supervisor
            // sends normal close requests to every matching process.
'@.Replace($crlf,$lf))
$old=@'
            // Two live-process checks: one here and R185 in the independent
            // supervisor. No other application process is terminated.
            if(DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(target))
                return true;
'@.Replace($crlf,$lf)
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
    throw 'R230 second queued peer defer hook missing'
}
$s=$s.Replace($old,@'
            // The external supervisor coordinates all normal exits and
            // rechecks the exact executable path before replacing files.
'@.Replace($crlf,$lf))
$direct='        Application.Current.Shutdown();'
if(([regex]::Matches($s,[regex]::Escape($direct))).Count -ne 1){
 throw 'R230 expected one current-process premature Shutdown call'
}
$s=$s.Replace($direct,@'
        // Do not forcibly shut down only the updater owner here; R230's
        // supervisor signals all Workspace/widget/tray processes together.
        // All WPF Closing handlers run and may decline unsaved-work exit.
        Log("R230 supervisor launched; awaiting coordinated normal shutdown.");
'@.Replace($crlf,$lf))
[IO.File]::WriteAllText($updater,$s,[Text.UTF8Encoding]::new($false))
$super="$root/DeskSpaceUpdateSupervisor.ps1"
$helpers=@'
# R230 installation-scoped, user-session graceful exit signal and a
# best-effort WM_CLOSE fallback for installed versions predating R230.
# Both mechanisms use ordinary WPF Close/Closing handlers. NEVER taskkill.
function Get-R230InstalledPeers([string]$executable) {
    $all=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction Stop)
    $result=@()
    foreach($process in $all) {
        if(!$process.ExecutablePath) {
            throw 'R230 cannot identify an active DeskSpace executable; no unsafe closure'
        }
        if($process.ExecutablePath.Equals($executable,[StringComparison]::OrdinalIgnoreCase)){
            $result+=$process
        }
    }
    return @($result)
}
function New-R230ExitEvent([string]$executable) {
    $full=[IO.Path]::GetFullPath($executable).TrimEnd('\','/').ToUpperInvariant()
    $sha=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($full))
    $eventName='Local\DeskSpace-UpdateExit-V230-'+[Convert]::ToHexString($sha).Substring(0,24)
    return [Threading.EventWaitHandle]::new(
        $false,[Threading.EventResetMode]::ManualReset,$eventName)
}
function Request-R230NativeClose([int[]]$processIds) {
    if($processIds.Count -eq 0){return 0}
    if(!('DeskSpaceR230.NativeClose' -as [type])){
        Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace DeskSpaceR230 {
  public static class NativeClose {
    private delegate bool Callback(IntPtr h,IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback c,IntPtr d);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    public static int Request(int[] pids) {
      var ids=new HashSet<uint>();
      foreach(var value in pids)ids.Add((uint)value);
      int count=0;
      Callback callback=(h,d)=>{
        GetWindowThreadProcessId(h,out uint pid);
        if(ids.Contains(pid) && PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero))count++;
        return true;
      };
      EnumWindows(callback,IntPtr.Zero);
      GC.KeepAlive(callback);
      return count;
    }
  }
}
"@
    }
    return [DeskSpaceR230.NativeClose]::Request($processIds)
}
'@
$inject='$original=[IO.Path]::GetFullPath($TargetExe)'
Swap $super $inject ($helpers+$lf+$inject) 'supervisor scoped graceful-exit helper'
Swap $super @'
    $previous=Get-Process -Id $OldPid -ErrorAction SilentlyContinue
    if($null -ne $previous){Wait-Process -Id $OldPid -Timeout 90 -ErrorAction Stop}
    # Keep other DeskSpace instances and any unsaved work untouched.
    $remaining=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.ExecutablePath -and $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
    if($remaining.Count -gt 0){throw 'R185 another old DeskSpace process is still running'}
'@ @'
    # R230: request normal exit for every installed DeskSpace process,
    # including all Workspace windows, widget satellites and the tray host.
    # New clients receive a scoped named event; older installed clients
    # receive WM_CLOSE on all of their top-level HWNDs (even hidden ones).
    $remaining=@(Get-R230InstalledPeers $original)
    if($remaining.Count -gt 0){
        $exitEvent=New-R230ExitEvent $original
        [void]$exitEvent.Set()
        $pids=[int[]]@($remaining|ForEach-Object {[int]$_.ProcessId})
        $requested=Request-R230NativeClose $pids
        Report ('R230 graceful close requested for '+$pids.Count+
            ' DeskSpace processes and '+$requested+' windows.')
        $deadline=[DateTime]::UtcNow.AddSeconds(70)
        while([DateTime]::UtcNow -lt $deadline){
            Start-Sleep -Milliseconds 800
            $remaining=@(Get-R230InstalledPeers $original)
            if($remaining.Count -eq 0){break}
        }
        if($remaining.Count -gt 0){
            $exitBlocked=$true
            throw ('R230 normal exit not completed by '+$remaining.Count+
                ' DeskSpace process(es). No forced termination; installer deferred.')
        }
        [void]$exitEvent.Reset()
        $exitEvent.Dispose()
        $exitEvent=$null
        Report 'R230 all installed DeskSpace processes exited normally.'
    }
'@ 'wait for every installed app exit before snapshot'
Swap $super @'
$restoreMainWindow=$false
try {
'@ @'
$restoreMainWindow=$false
$exitEvent=$null
$exitBlocked=$false
try {
'@ 'track installation-scoped exit signal'
Swap $super @'
    if($ownsMutex){
        try{
'@ @'
    if($ownsMutex -and !$exitBlocked){
        try{
'@ 'do not relaunch widget if a user declined normal close'
Swap $super @'
finally {
    if(!$complete -and (Test-Path -LiteralPath $pending)){
'@ @'
finally {
    # A new install must never inherit the prior update exit request.
    # Clear even when a user cancels or the process fails unexpectedly.
    if($exitEvent){
        try{[void]$exitEvent.Reset();$exitEvent.Dispose()}catch{}
    }
    if(!$complete -and (Test-Path -LiteralPath $pending)){
'@ 'always reset coordinated exit signal on failure'
# Once every installed DeskSpace process has exited, the installer MUST NOT
# invoke an independent force-close path that could ignore an unsaved dialog.
Swap $super @'
    $arguments='/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR="'+$install+'" /LOG="'+$setupLog+'"'
'@ @'
    $arguments='/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /DIR="'+$install+'" /LOG="'+$setupLog+'"'
'@ 'installer must not ignore cancelled close'
Write-Host 'R230: installer supervisor automatically requests normal exit of ALL installed Workspace/widget/tray windows, waits for complete process shutdown and only then snapshots/installs.'
