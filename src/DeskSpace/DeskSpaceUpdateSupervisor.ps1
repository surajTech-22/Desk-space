param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$StatusLog,
    [Parameter(Mandatory=$true)][int]$OldPid
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R185 full-install snapshot helpers. The backup always lives outside the
# installed directory and never touches per-user DeskSpace data.
function Assert-R185InstallScope([string]$folder) {
    $full=[IO.Path]::GetFullPath($folder).TrimEnd('\','/')
    if([IO.Path]::GetFileName($full) -ne 'DeskSpace'){
        throw 'R185 refuses a folder not named DeskSpace'
    }
    $local=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DeskSpace')).TrimEnd('\','/')
    if($full.Equals($local,[StringComparison]::OrdinalIgnoreCase) -or
       $full.StartsWith($local+[IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)){
        throw 'R185 refuses the local DeskSpace data directory'
    }
    if(!(Test-Path -LiteralPath $full -PathType Container)){
        throw 'R185 original installation is missing'
    }
    $rootItem=Get-Item -LiteralPath $full -Force
    if(($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
        throw 'R185 refuses reparse-point installation roots'
    }
    return $full
}
function Get-R185Inventory([string]$folder) {
    $full=Assert-R185InstallScope $folder
    $root=$full+[IO.Path]::DirectorySeparatorChar
    $all=@(Get-ChildItem -LiteralPath $full -Force -Recurse -ErrorAction Stop)
    if($all.Count -gt 25000){throw 'R185 install file count exceeds safety limit'}
    $files=@()
    [long]$total=0
    foreach($entry in $all){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            throw 'R185 refuses symlink/junction entries'
        }
        if($entry.PSIsContainer){continue}
        $relative=$entry.FullName.Substring($root.Length)
        if($relative.StartsWith('..') -or
           [IO.Path]::IsPathRooted($relative)){
            throw 'R185 inventory escaped installation root'
        }
        $total+=$entry.Length
        if($total -gt 2GB){throw 'R185 backup exceeds 2 GiB limit'}
        $files+=New-Object PSObject -Property @{
            Path=$relative;Size=[long]$entry.Length;
            Sha=(Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash
        }
    }
    if(-not (Test-Path -LiteralPath (Join-Path $full 'DeskSpace.exe') -PathType Leaf)){
        throw 'R185 missing original DeskSpace executable'
    }
    return $files
}
function Save-R185Snapshot([string]$install,[string]$snapshot) {
    $full=Assert-R185InstallScope $install
    if(Test-Path -LiteralPath $snapshot){throw 'R185 refuses existing backup destination'}
    $inventory=@(Get-R185Inventory $full)
    if($inventory.Count -lt 1){throw 'R185 cannot snapshot an empty installation'}
    $parent=Split-Path -Parent $snapshot
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $snapshot -Recurse -Force -ErrorAction Stop
    foreach($entry in $inventory){
        $target=Join-Path $snapshot $entry.Path
        if(!(Test-Path -LiteralPath $target -PathType Leaf) -or
            (Get-Item -LiteralPath $target).Length -ne $entry.Size -or
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Sha){
            throw ('R185 snapshot mismatch at '+$entry.Path)
        }
    }
    $count=@(Get-ChildItem -LiteralPath $snapshot -Force -Recurse -File).Count
    if($count -ne $inventory.Count){throw 'R185 snapshot file count differs'}
    $manifest=Join-Path (Split-Path -Parent $snapshot) 'verified-manifest.json'
    $record=@($inventory|Select-Object Path,Size,Sha)|ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($manifest,$record,[Text.UTF8Encoding]::new($false))

    return $inventory.Count
}
function Restore-R185Snapshot([string]$install,[string]$snapshot,[string]$quarantine) {
    # The current new install is QUARANTINED, never deleted. The prior entire
    # folder is restored, not just DeskSpace.exe or a single DLL.
    if(!(Test-Path -LiteralPath (Join-Path $snapshot 'DeskSpace.exe') -PathType Leaf)){
        throw 'R185 validated prior installation snapshot is missing'
    }
    $manifest=Join-Path (Split-Path -Parent $snapshot) 'verified-manifest.json'
    if(!(Test-Path -LiteralPath $manifest -PathType Leaf)){
        throw 'R185 missing previous snapshot SHA-256 manifest'
    }
    $reparse=@(Get-ChildItem -LiteralPath $snapshot -Recurse -Force | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0})
    if($reparse.Count -gt 0){throw 'R185 snapshot contains a symlink or junction'}
    $prior=@(Get-ChildItem -LiteralPath $snapshot -Recurse -Force -File)
    $snapshotRoot=$snapshot.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if($prior.Count -eq 0){throw 'R185 empty rollback snapshot'}
    $records=@(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
    if($prior.Count -ne $records.Count){throw 'R185 snapshot file count differs from verified manifest'}
    $oldHash=@{}
    foreach($item in $prior){
        if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            throw 'R185 rollback contains a reparse point'
        }
        $relative=$item.FullName.Substring($snapshotRoot.Length)
        $oldHash[$relative]=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    }
    foreach($record in $records){
        $relative=[string]$record.Path
        if([string]::IsNullOrWhiteSpace($relative) -or
           [IO.Path]::IsPathRooted($relative) -or
           ($relative -split '[\\/]') -contains '..' -or
           !$oldHash.ContainsKey($relative) -or
           $oldHash[$relative] -ne [string]$record.Sha){
            throw 'R185 backup was changed after verification; refusing rollback'
        }
    }
    if(Test-Path -LiteralPath $install){
        [void](Assert-R185InstallScope $install)
        if(Test-Path -LiteralPath $quarantine){
            throw 'R185 failed-install quarantine already exists'
        }
        Move-Item -LiteralPath $install -Destination $quarantine -ErrorAction Stop
    }
    Copy-Item -LiteralPath $snapshot -Destination $install -Recurse -Force -ErrorAction Stop
    foreach($relative in $oldHash.Keys){
        $restored=Join-Path $install $relative
        if(!(Test-Path -LiteralPath $restored -PathType Leaf) -or
            (Get-FileHash -LiteralPath $restored -Algorithm SHA256).Hash -ne $oldHash[$relative]){
            throw ('R185 restored installation hash mismatch at '+$relative)
        }
    }
    return $oldHash.Count
}

function Report([string]$message) {
    try {
        $parent=Split-Path -Parent $StatusLog
        if(!(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
        Add-Content -LiteralPath $StatusLog -Value ([DateTime]::UtcNow.ToString('O')+' '+$message)
    } catch {}
}
function Launch-R185Host {
    $running=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -like '*--widget-host*' -and
            ($null -eq $_.ExecutablePath -or
             $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase))
        })
    if($running.Count -eq 0 -and (Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        Start-Process -FilePath $TargetExe -WorkingDirectory (Split-Path -Parent $TargetExe) -ArgumentList '--widget-host' -ErrorAction Stop | Out-Null
        Report 'R185 primary widget host relaunch requested.'
    }
}
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
    # Windows PowerShell 5.1 runs on .NET Framework: HashData() and
    # Convert.ToHexString() do not exist there. Match the .NET 8 WPF hash
    # using framework-compatible APIs.
    $sha256=[Security.Cryptography.SHA256]::Create()
    try{$sha=$sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($full))}
    finally{$sha256.Dispose()}
    $hex=[BitConverter]::ToString($sha).Replace('-','')
    $eventName='Local\DeskSpace-UpdateExit-V230-'+$hex.Substring(0,24)
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
using System.Text;
namespace DeskSpaceR230 {
  public static class NativeClose {
    private delegate bool Callback(IntPtr h,IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback c,IntPtr d);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr h,StringBuilder name,int max);
    public static int Request(int[] pids) {
      var ids=new HashSet<uint>();
      foreach(var value in pids)ids.Add((uint)value);
      int count=0;
      Callback callback=(h,d)=>{
        uint pid=0;
        GetWindowThreadProcessId(h,out pid);
        if(!ids.Contains(pid))return true;
        // Never send WM_CLOSE to a PowerShell console, native dialog or
        // unrelated UI in the same process: those could bypass WPF Closing
        // and discard unsaved work. Target only DeskSpace's own WPF HWNDs.
        var classname=new StringBuilder(260);
        if(GetClassName(h,classname,classname.Capacity)<=0)return true;
        var type=classname.ToString();
        if(!type.StartsWith("HwndWrapper[",StringComparison.Ordinal) &&
           !type.StartsWith("WindowsForms10.Window.",StringComparison.Ordinal))
           return true;
        if(PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero))count++;
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
$original=[IO.Path]::GetFullPath($TargetExe)
$install=Split-Path -Parent $original
$root=Join-Path $env:LOCALAPPDATA 'DeskSpace\Updates'
$txn=[guid]::NewGuid().ToString('N')
$rollback=Join-Path $root ('Rollback\'+$txn)
$snapshot=Join-Path $rollback 'snapshot'
$quarantine=Join-Path $rollback 'failed-install'
$post=Join-Path $root 'PostInstall'
$pending=Join-Path $post 'pending.json'
$ready=Join-Path $post 'ready.json'
$hadSnapshot=$false
$installerStarted=$false
$complete=$false
$ownsMutex=$false
$mutex=$null
$restoreMainWindow=$false
$exitEvent=$null
$exitBlocked=$false
try {
    $mutex=New-Object System.Threading.Mutex($false,'Local\DeskSpace-UpdateSupervisor-V185')
    $ownsMutex=$mutex.WaitOne(0)
    if(!$ownsMutex){
        Report 'R227 active supervisor owns install lock; duplicate exits without relaunch.'
        return
    }
    $tag=Split-Path -Leaf (Split-Path -Parent $Installer)
    if($tag -notmatch '^(?:tester-)?v(\d+\.\d+\.\d+)$'){
        throw 'R185 unrecognized verified release staging tag'
    }
    $version=$Matches[1]
    if(!(Test-Path -LiteralPath $Installer -PathType Leaf)){
        throw 'R185 verified installer missing before update'
    }
    if(!(Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        throw 'R185 current DeskSpace executable missing'
    }
    [void](Assert-R185InstallScope $install)
    Report ('R185 update transaction '+$txn+' version '+$version+' started.')
    $others=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.ProcessId -ne $OldPid -and $_.CommandLine -notlike '*--widget-host*'})
    $restoreMainWindow=$others.Count -gt 0
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
    $count=Save-R185Snapshot $install $snapshot
    $hadSnapshot=$true
    Report ('R185 verified full-install snapshot, files='+$count)
    New-Item -ItemType Directory -Force -Path $post|Out-Null
    if(Test-Path -LiteralPath $ready){Remove-Item -LiteralPath $ready -Force}
    $json=@{Transaction=$txn;Version=$version}|ConvertTo-Json -Compress
    [IO.File]::WriteAllText($pending,$json,[Text.UTF8Encoding]::new($false))
    $setupLog=Join-Path (Split-Path -Parent $Installer) 'inno-setup.log'
    $arguments='/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /DIR="'+$install+'" /LOG="'+$setupLog+'"'
    Report 'R185 launching verified installer over original directory.'
    $installerStarted=$true
    $setup=Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru -ErrorAction Stop
    if($setup.ExitCode -ne 0){throw ('R185 installer exited '+$setup.ExitCode)}
    if(!(Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        throw 'R185 installer succeeded but new executable is absent'
    }
    Start-Sleep -Seconds 2
    Launch-R185Host
    $deadline=[DateTime]::UtcNow.AddSeconds(50)
    $ack=$false
    while([DateTime]::UtcNow -lt $deadline) {
        if(Test-Path -LiteralPath $ready -PathType Leaf){
            try{
                if((Get-Item -LiteralPath $ready).Length -le 4096){
                    $receipt=Get-Content -LiteralPath $ready -Raw|ConvertFrom-Json
                    if($receipt.Transaction -eq $txn -and
                       $receipt.Version -eq $version -and [int]$receipt.Pid -gt 0){
                        $process=Get-Process -Id ([int]$receipt.Pid) -ErrorAction SilentlyContinue
                        if($process -and !$process.HasExited -and $process.Path -and
                           $process.Path.Equals($original,[StringComparison]::OrdinalIgnoreCase)){
                            $ack=$true;break
                        }
                    }
                }
            }catch{}
        }
        Start-Sleep -Seconds 1
    }
    if(!$ack){
        Report ('R227 installed widget did not produce matching receipt for '+$version+
            '; snapshot retained and live app will not be force-terminated.')
        throw 'R185 installed widget did not acknowledge primary WPF Loaded'
    }
    Report ('R185 verified post-install ready receipt for '+$version)
    if($restoreMainWindow){
        $openMain=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
            Where-Object {$_.CommandLine -notlike '*--widget-host*'})
        if($openMain.Count -eq 0){
            Start-Process -FilePath $TargetExe -WorkingDirectory $install -ErrorAction Stop|Out-Null
            Report 'R185 previous full Workspace window reopened.'
        }
    }
    $complete=$true
    if(Test-Path -LiteralPath $pending){Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue}
    Report 'R185 SUCCESS: complete prior-install snapshot retained for recovery.'
}
catch {
    Report ('R185 FAILED: '+$_.Exception.GetType().Name+': '+$_.Exception.Message)
    if($hadSnapshot -and $installerStarted){
        try{
            $live=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
                Where-Object {$_.ExecutablePath -and $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
            if($live.Count -gt 0){
                throw 'New app still running: do not force-terminate unsaved work; snapshot retained'
            }
            $restored=Restore-R185Snapshot $install $snapshot $quarantine
            Report ('R185 restored complete prior installation, files='+$restored)
        }catch {
            Report ('R185 manual recovery required, snapshot retained: '+$_.Exception.GetType().Name)
        }
    }
    if($ownsMutex -and !$exitBlocked){
        try{
            $visible=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
                Where-Object {$_.ExecutablePath -and
                  $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
            if($visible.Count -eq 0){Launch-R185Host}
            else{Report 'R227 failure left a live DeskSpace process; preserving it without relaunch.'}
        }catch{Report ('R227 fallback host unavailable: '+$_.Exception.GetType().Name)}
    }
    exit 1
}
finally {
    # A new install must never inherit the prior update exit request.
    # Clear even when a user cancels or the process fails unexpectedly.
    if($exitEvent){
        try{[void]$exitEvent.Reset();$exitEvent.Dispose()}catch{}
    }
    if(!$complete -and (Test-Path -LiteralPath $pending)){
        try{Remove-Item -LiteralPath $pending -Force}catch{}
    }
    if($ownsMutex -and $mutex){$mutex.ReleaseMutex()}
    if($mutex){$mutex.Dispose()}
}
