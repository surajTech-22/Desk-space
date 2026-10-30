$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r191/DeskSpaceVerifiedUpdateQueueV191.cs' "$root/DeskSpaceVerifiedUpdateQueueV191.cs" -Force
$file="$root/DeskSpacePublicUpdaterV44.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function Once([string]$before,[string]$after,[string]$why){
  $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
  $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
  if($count -ne 1){throw "R191 $why expected 1 anchor, got $count"}
  $script:s=$script:s.Replace($before,$after)
}
$queue=@'
    // R191: stage once from VERIFIED GitHub bytes; do not force-close an
    // ordinary Workspace or another user-owned widget host. A one-minute
    // local retry requires no repeat REST request and no new download.
    private static async Task<bool> ResumeVerifiedV191(Window owner,bool automatic)
    {
        var pending=DeskSpaceVerifiedUpdateQueueV191.Peek(Channel);
        if(pending==null)return false;
        if(!await Gate.WaitAsync(0))return true;
        try
        {
            var target=Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe");
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
            if(!DeskSpaceVerifiedUpdateQueueV191.IsStillVerified(pending))
            {
                DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
                Log("R191 staged update invalid or changed; a fresh official GitHub verification is required.");
                return false;
            }
            // Two live-process checks: one here and R185 in the independent
            // supervisor. No other application process is terminated.
            if(DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(target))
                return true;
            DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
            Log("R191 verified staged "+pending.Tag+" is now safe to hand off.");
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);
            return true;
        }
        catch(Exception ex)
        {
            DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
            Log("R191 queued handoff failed: "+ex.GetType().Name+" "+ex.Message);
            return true;
        }
        finally{Gate.Release();}
    }
'@
Once '    public static async Task CheckAsync(Window owner,bool automatic=false,' (
  $queue.Replace($crlf,$lf)+'    public static async Task CheckAsync(Window owner,bool automatic=false,') 'queue logic before CheckAsync'
Once @'
        if(!await Gate.WaitAsync(0))return;
        bool supervisorLaunched=false;
'@ @'
        if(await ResumeVerifiedV191(owner,automatic))return;
        if(!await Gate.WaitAsync(0))return;
        bool supervisorLaunched=false;
'@ 'inspect verified cache before rate-limit and network'
Once @'
                Verify(stableInstaller,sha,null);
                stage="Launch verified installer supervisor";
                DeskSpaceUpdateProgress.Publish("Restarting",100);
                await StartSupervisor(stableInstaller,stableFolder,stableTag);
'@ @'
                Verify(stableInstaller,sha,null);
                DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,
                    stableFolder,stableTag,sha,"stable");
                stage="Launch verified installer supervisor";
                if(DeferForOpenDeskSpace(owner,automatic))return;
                DeskSpaceUpdateProgress.Publish("Restarting",100);
                await StartSupervisor(stableInstaller,stableFolder,stableTag);
'@ 'stable safe handoff after checksum and peer check'
Once @'
            if(DeferForOpenDeskSpace(owner,automatic))return;
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ @'
            // Stage the official installer once even when Workspace is open.
            // Never attempt an install until the R188 final peer gate passes.
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ 'tester allow trusted staging while peers are open'
Once @'
            Verify(installer,checksum,root);
            stage="Start verified installation supervisor";
'@ @'
            Verify(installer,checksum,root);
            DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,
                tag,checksum,"tester");
            stage="Start verified installation supervisor";
'@ 'tester stage release only after SHA and metadata validation'
# Serialize staging between independent Workspace and widget-host processes.
# Reuse a file only after validating it AGAIN against official release hash.
Once @'
                var stableInstaller=Path.Combine(stableFolder,"DeskSpace_Setup.exe");
                DeskSpaceUpdateProgress.Publish("Downloading",0);
'@ @'
                var stableInstaller=Path.Combine(stableFolder,"DeskSpace_Setup.exe");
                using var stableLockV191=await DeskSpaceVerifiedUpdateQueueV191.AcquireStageLockAsync(
                    Path.Combine(Local,"Updates"),stableTimeout.Token);
                DeskSpaceUpdateProgress.Publish("Downloading",0);
'@ 'stable cross-process staging lock'
Once @'
                await Download(StableAssetV131(stableTag,
                    "DeskSpace_Setup.exe"),stableInstaller,stableTimeout.Token);
'@ @'
                bool stableCachedV191=false;
                if(File.Exists(stableInstaller))
                {
                    try{Verify(stableInstaller,sha,null);stableCachedV191=true;}
                    catch(InvalidDataException){}
                }
                if(!stableCachedV191)
                    await Download(StableAssetV131(stableTag,
                        "DeskSpace_Setup.exe"),stableInstaller,stableTimeout.Token);
'@ 'stable checksum-verified reuse'
Once @'
            var installer=Path.Combine(folder,"DeskSpace_Setup.exe");
            DeskSpaceUpdateProgress.Publish("Downloading",0);
'@ @'
            var installer=Path.Combine(folder,"DeskSpace_Setup.exe");
            using var testerLockV191=await DeskSpaceVerifiedUpdateQueueV191.AcquireStageLockAsync(
                Path.Combine(Local,"Updates"),timeout.Token);
            DeskSpaceUpdateProgress.Publish("Downloading",0);
'@ 'tester cross-process staging lock'
Once @'
            await Download(setup,installer,timeout.Token);
'@ @'
            bool testerCachedV191=false;
            if(File.Exists(installer))
            {
                try{Verify(installer,checksum,root);testerCachedV191=true;}
                catch(InvalidDataException){}
            }
            if(!testerCachedV191)await Download(setup,installer,timeout.Token);
'@ 'tester checksum and metadata verified reuse'
# The verified installer is now staged, so use accurate instructions instead
# of the old 'close and check again' message.
Once @'
                "Close other DeskSpace windows normally and check again. "+
                "Your current widget and unsaved work will not be closed.",
'@ @'
                "Save work and close the other Workspace normally. "+
                "The verified update will retry automatically; your saved data is retained.",
'@ 'accurate staged update guidance'
# Retain R188's final gate and R185's supervisor validation unchanged.
# Confirm no other branch replaces this exact post-download guard.
if(([regex]::Matches($s,[regex]::Escape(
    'if(DeferForOpenDeskSpace(owner,automatic))return;'))).Count -ne 2){
    throw 'R191 must preserve exactly two final safe-install guards (stable/tester)'
}
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R191: both channels stage SHA-verified installer; retry locally when peers close; no forced termination or bypass of final supervisor gate.'
