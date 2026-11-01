$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
if(!(Test-Path $path)){throw 'R310 original shipping updater missing'}
$s=[IO.File]::ReadAllText((Resolve-Path $path))
$nl=[string][char]10
$s=$s.Replace(([string][char]13+$nl),$nl)
function One([string]$old,[string]$new,[string]$label)
{
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R310 '+$label+' anchor not unique: '+$count)}
 $script:s=$script:s.Replace($old,$new)
}
One '    private static async Task StartSupervisor(string installer,string folder,string tag)' (
'    // A live supervisor mutex means an in-progress transaction, not a failed update.'+$nl+
'    private sealed class SupervisorBusyV310Exception : InvalidOperationException'+$nl+
'    {'+$nl+
'        internal SupervisorBusyV310Exception() :'+$nl+
'            base("Another verified DeskSpace installation is already in progress."){}'+$nl+
'    }'+$nl+$nl+
'    private static async Task StartSupervisor(string installer,string folder,string tag)'
) 'typed busy exception'
One 'throw new InvalidOperationException("R227 supervised installation already running; no second handoff.");' (
'throw new SupervisorBusyV310Exception();') 'busy classification'
One 'Log("R227 active supervisor already owns installer; retaining this widget.");' (
'Log("R310 update deferred: an existing supervisor owns the install mutex; no duplicate handoff. Verified download stays available.");') 'busy log'
$old='            catch(Exception error) when(error is InvalidOperationException or IOException)'
$insert=@'
            catch(SupervisorBusyV310Exception)
            {
                Log("R310 verified candidate retained: another supervisor is still completing the installation.");
                DeskSpaceUpdateProgress.Clear();
                if(!automatic)
                    MessageBox.Show(owner,
                        "A DeskSpace installation is already in progress. "+
                        "No second installer was launched. Your current Workspace and saved data are unchanged. "+
                        "The verified update remains cached and DeskSpace will retry when the supervisor finishes.",
                        "DeskSpace update in progress",MessageBoxButton.OK,
                        MessageBoxImage.Information);
                return true;
            }
'@
One $old ($insert+$nl+$old) 'verified queue defer'
$oldCatch=@'
        catch(Exception ex)
        {
            Log("Update failed at "+stage+": "+ex.GetType().Name+" "+ex.Message);
'@
$newCatch=@'
        catch(SupervisorBusyV310Exception)
        {
            // Benign contention: the actual supervisor owns install/rollback.
            // Never launch a duplicate, discard verified download or stop work.
            Log("R310 update deferred at "+stage+
                ": another supervisor owns the transaction. Cached verified installer retained.");
            if(!automatic)
                MessageBox.Show(owner,
                    "Another DeskSpace installation is already in progress. "+
                    "No second installation was started. The current app and saved data are unchanged. "+
                    "The verified update remains cached for automatic retry after the first supervisor exits.",
                    "DeskSpace update in progress",MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }
        catch(Exception ex)
        {
            Log("Update failed at "+stage+": "+ex.GetType().Name+" "+ex.Message);
'@
One $oldCatch ($newCatch.Replace(([string][char]13+$nl),$nl)) 'no false failed-update dialog'
foreach($token in @('catch(SupervisorBusyV310Exception)', 'throw new SupervisorBusyV310Exception();',
    'DeskSpace update in progress', 'DeskSpaceVerifiedUpdateQueueV191.Stage(')){
 if(!$s.Contains($token)){throw "R310 missing guard $token"}
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R310 safe supervisor guard: preserve installer, deferred retry and informational dialog; no duplicate installers.'
