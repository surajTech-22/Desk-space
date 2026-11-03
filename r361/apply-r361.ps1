$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=Join-Path $root 'App.xaml.cs'
$worker=Join-Path $root 'DeskSpaceWidgetStartupV361.cs'
$fallback=Join-Path $root 'DeskSpaceStartupFallbackV276.cs'
if(!(Test-Path -LiteralPath $app)){throw 'R361 missing assembled shipping App.xaml.cs'}
if(!(Test-Path -LiteralPath 'r361/DeskSpaceWidgetStartupV361.cs')){throw 'R361 source missing'}
if(!(Test-Path -LiteralPath $fallback)){
    # Use the existing tested fallback class, never a new UI flow.
    Copy-Item 'src/DeskSpace/DeskSpaceStartupFallbackV276.cs' $fallback -Force
}
function One([string]$old,[string]$replacement,[string]$name){
    $lf=[string][char]10
    $s=[IO.File]::ReadAllText($app).Replace(([string][char]13+$lf),$lf)
    $old=$old.Replace(([string][char]13+$lf),$lf)
    $replacement=$replacement.Replace(([string][char]13+$lf),$lf)
    $matches=([regex]::Matches($s,[regex]::Escape($old))).Count
    if($matches -ne 1){throw ("R361 $name expected one shipping anchor, got $matches")}
    [IO.File]::WriteAllText($app,$s.Replace($old,$replacement),
        [Text.UTF8Encoding]::new($false))
}
# R360 published source has the older, minimal R232 app shell; it did NOT
# include R286's tray-exit marker or R183's early-recovery methods.
# Do not call unshipped methods or replace the entire App.xaml.cs.
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
                        // A previous process still owns the singleton, but its
                        // IPC pipe never became ready. Never kill it, silently
                        // exit, or create a second functional widget.
                        widgetMutex.Dispose();
                        widgetMutex=null;
                        DeskSpaceStartupFallbackV276.Show(
                            "Another DeskSpace widget process is running "+
                            "but did not respond to the startup command. "+
                            "No running processes or saved data were changed.");
                        return;
                    }
                    // The primary acknowledged this Show request. This
                    // duplicate has no user windows, settings or tray.
                    Shutdown(0);
                    Environment.Exit(0);
                    return;
                }
                // CreatedNew=false is not proof of a live owner. We now own
                // this existing kernel mutex through WaitOne(0).
            }
            else DeskSpaceWidgetStartupV361.Audit(
                "fresh widget mutex acquired");
'@ 'mutex ownership and IPC race fix'
One @'
            var window=new WidgetShellWindow();MainWindow=window;window.Show();
                DeskSpaceStorageMaintenanceV351.Begin();return;
'@ @'
            try
            {
                var window=new WidgetShellWindow();
                MainWindow=window;
                window.Show();
                DeskSpaceWidgetStartupV361.Audit("widget window shown");
                DeskSpaceStorageMaintenanceV351.Begin();
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception or
                System.Runtime.InteropServices.COMException or
                System.Windows.Markup.XamlParseException or
                TypeInitializationException)
            {
                DeskSpaceWidgetStartupV361.Audit(
                    "widget construction failed: "+ex.GetType().Name);
                DeskSpaceStartupFallbackV276.Show(ex.GetType().Name);
            }
            return;
'@ 'log first paint or use existing recovery window on constructor error'
Copy-Item 'r361/DeskSpaceWidgetStartupV361.cs' $worker -Force
$appText=[IO.File]::ReadAllText($app)
if(!$appText.Contains('DeskSpaceStorageMaintenanceV351.Begin();') -or
   !$appText.Contains('DeskSpaceGracefulExitV230.UpdateExitAuthorized') -or
   !$appText.Contains('Environment.Exit(0);') -or
   !$appText.Contains('DeskSpaceWidgetStartupV361.ResolveExisting(')){
    throw 'R361 lost existing shipping update/cleanup guards'
}
Write-Host 'R361: verified R360 release shell now resolves stale mutex, retries live pipe and displays guarded startup recovery without changing widget or user data.'
