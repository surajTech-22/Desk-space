$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=Join-Path $root 'App.xaml.cs'
$worker=Join-Path $root 'DeskSpaceWidgetStartupV361.cs'
if(!(Test-Path -LiteralPath $app)){throw 'R361 actual shipping App.xaml.cs is missing'}
if(!(Test-Path -LiteralPath 'r361/DeskSpaceWidgetStartupV361.cs')){
    throw 'R361 startup election source missing'
}
# Diagnostics print only shipping startup CODE; no user data or filesystem scans.
$prior=[IO.File]::ReadAllText($app)
$point=$prior.IndexOf('if(e.Args.Contains("--widget-host"')
if($point -ge 0){
    Write-Host "R361 ACTUAL SHIPPING APP STARTUP:"
    Write-Host $prior.Substring($point,[Math]::Min(3400,$prior.Length-$point))
}
function One([string]$old,[string]$new,[string]$name){
    $current=[IO.File]::ReadAllText($app)
    $lf=[string][char]10
    $current=$current.Replace(([string][char]13+$lf),$lf)
    $old=$old.Replace(([string][char]13+$lf),$lf)
    $new=$new.Replace(([string][char]13+$lf),$lf)
    $matches=([regex]::Matches($current,[regex]::Escape($old))).Count
    if($matches -ne 1){throw ('R361 '+$name+' requires one exact shipping anchor; found '+$matches)}
    [IO.File]::WriteAllText($app,$current.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
One @'
            if(DeskSpaceWidgetExitV286.Dismissed)
            {
                Shutdown(0);
                return;
            }
'@ @'
            if(DeskSpaceWidgetExitV286.Dismissed)
            {
                // An explicit tray Exit is still sovereign over automatic
                // Run-entry and update relaunch. This is not a startup crash.
                DeskSpaceWidgetStartupV361.Audit(
                    "startup skipped: user previously selected Exit Widget");
                Shutdown(0);
                return;
            }
'@ 'respect tray Exit'
One @'
            widgetMutex=new Mutex(true,@"Local\DeskSpace.WidgetHost.v3",out var first);
            if(!first)
            {
                try { using var client = new System.IO.Pipes.NamedPipeClientStream(".", "DeskSpace.Widget.V3", System.IO.Pipes.PipeDirection.Out); client.Connect(400); client.WriteByte(1); client.Flush(); } catch { }
                // We never owned the singleton widget or opened any user
                // windows/data. A second widget-host MUST not remain as an
                // invisible process blocking the independent installer.
                Shutdown(0);
                Environment.Exit(0);
                return;
            }
'@ @'
            widgetMutex=new Mutex(true,@"Local\DeskSpace.WidgetHost.v3",out var first);
            if(!first)
            {
                var outcome=DeskSpaceWidgetStartupV361.ResolveExisting(
                    widgetMutex,"DeskSpace.Widget.V3");
                if(outcome!=DeskSpaceWidgetStartupV361.Outcome.BecameOwner)
                {
                    if(outcome==DeskSpaceWidgetStartupV361.Outcome.UnresponsiveExisting)
                    {
                        // A process still holds the singleton but has no
                        // responsive command pipe. Never kill it, open a
                        // duplicate widget, or silently drop this startup.
                        widgetMutex.Dispose();
                        widgetMutex=null;
                        DeskSpaceStartupFallbackV276.Show(
                            "A DeskSpace widget process is still running "+
                            "but is not responding to the startup command. "+
                            "No data or running processes were changed.");
                        return;
                    }
                    // Existing host was notified successfully. We never
                    // created a user window or tray; terminate this harmless
                    // duplicate cleanly as in the tested R355 handoff.
                    Shutdown(0);
                    Environment.Exit(0);
                    return;
                }
                // If the named mutex object exists but is unowned (or its
                // owner exits during the retry), WaitOne owns it here and
                // this process legitimately becomes the primary widget.
            }
            else DeskSpaceWidgetStartupV361.Audit("acquired fresh primary mutex");
'@ 'stale mutex ownership and slow named pipe startup'
One @'
                window.Show();
                DeskSpaceStorageMaintenanceV351.Begin();
                widgetStarted=true;
'@ @'
                window.Show();
                DeskSpaceWidgetStartupV361.Audit("primary widget window shown");
                DeskSpaceStorageMaintenanceV351.Begin();
                widgetStarted=true;
'@ 'widget startup completion log'
One @'
                Debug.WriteLine("DeskSpace widget early startup failed: "+
                    ex.GetType().Name);
'@ @'
                Debug.WriteLine("DeskSpace widget early startup failed: "+
                    ex.GetType().Name);
                DeskSpaceWidgetStartupV361.Audit(
                    "widget construction failed: "+ex.GetType().Name);
'@ 'startup fallback error audit'
Copy-Item 'r361/DeskSpaceWidgetStartupV361.cs' $worker -Force
$src=[IO.File]::ReadAllText($app)
if(!$src.Contains('DeskSpaceStartupRecoveryV183.BeginEarly();') -or
   !$src.Contains('DeskSpaceGracefulExitV230.UpdateExitAuthorized') -or
   !$src.Contains('DeskSpaceStorageMaintenanceV351.Begin();') -or
   !$src.Contains('Environment.Exit(0);')){
    throw 'R361 must preserve crash-loop, update handoff, and guarded cleanup'
}
Write-Host 'R361: singleton handoff waits for slow pipe, recovers stale unowned mutex, logs startup and retains explicit tray Exit.'
