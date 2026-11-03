$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$app=Join-Path $root 'App.xaml.cs'
$close=Join-Path $root 'DeskSpaceGracefulExitV230.cs'
foreach($file in @($app,$close)){if(!(Test-Path -LiteralPath $file)){throw ('R355 shipping source missing: '+$file)}}
function One([string]$path,[string]$old,[string]$new,[string]$name) {
    $s=[IO.File]::ReadAllText($path)
    $old=$old.Replace(([string][char]13+[string][char]10),[string][char]10)
    $new=$new.Replace(([string][char]13+[string][char]10),[string][char]10)
    $s=$s.Replace(([string][char]13+[string][char]10),[string][char]10)
    $found=([regex]::Matches($s,[regex]::Escape($old))).Count
    if($found -ne 1){throw "R355 $name expected one anchor, got $found"}
    [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
# A second "--widget-host" can receive a named-pipe Show request without
# owning any user files, windows or tray. Under earlier releases Shutdown()
# could leave thread-pool/COM native threads alive without a visible widget.
# There is nothing to save because the widget constructor was never called.
One $app @'
                Shutdown(); return;
'@ @'
                // We never owned the singleton widget or opened any user
                // windows/data. A second widget-host MUST not remain as an
                // invisible process blocking the independent installer.
                Shutdown(0);
                Environment.Exit(0);
                return;
'@ 'orphan/duplicate widget host exit'
One $close @'
    private static DispatcherTimer? listener;
'@ @'
    private static DispatcherTimer? listener;
    // The updater is allowed to finish a process only after all WPF
    // Closing handlers have completed without a veto. Never use this
    // flag for ordinary tray exits, shutdown errors or user dialogs.
    private static volatile bool updateExitAuthorized;
    internal static bool UpdateExitAuthorized=>updateExitAuthorized;
    internal static void LogR355(string message)
    {
        try
        {
            var root=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root,"update-status.log"),
                DateTime.UtcNow.ToString("O")+" R355 pid="+Environment.ProcessId+
                " "+message+Environment.NewLine);
        }
        catch(IOException){} catch(UnauthorizedAccessException){}
    }
'@ 'scoped update exit authorization and safe diagnostics'
One $close @'
            if(TryCloseWindows(Application.Current.Windows.Cast<Window>().ToArray()))
            {
                timer.Stop();
                Application.Current.Shutdown();
            }
            // If any window refused, keep the app and saved state intact.
'@ @'
            var windows=Application.Current.Windows.Cast<Window>().ToArray();
            LogR355("update exit signal observed; windows="+windows.Length);
            updateExitAuthorized=true;
            if(TryCloseWindows(windows))
            {
                LogR355("all WPF windows accepted graceful Close; requesting App.OnExit");
                timer.Stop();
                Application.Current.Shutdown();
            }
            else
            {
                updateExitAuthorized=false;
                LogR355("one WPF window refused graceful Close; preserved open state");
            }
            // If any window refused, keep the app and saved state intact.
'@ 'cooperative window close consent, safe audit and opt-in exit'
One $app @'
        widgetMutex?.Dispose();base.OnExit(e);
'@ @'
        widgetMutex?.Dispose();
        base.OnExit(e);
        if(DeskSpaceGracefulExitV230.UpdateExitAuthorized)
        {
            // All windows already ran normal WPF Closing/Closed, including
            // unsaved-work vetoes. OnExit has completed widget cleanup and
            // released the singleton. On some PCs a foreground COM/WinForms
            // helper thread can otherwise keep an invisible DeskSpace.exe
            // alive for >70s, causing R230 to abort before installing.
            DeskSpaceGracefulExitV230.LogR355(
                "App.OnExit finished; completing authorized update shutdown");
            Environment.Exit(0);
        }
'@ 'final cooperative process exit after normal App.OnExit'
# Reject executable Process.Kill() invocations, not safety comments.
if(([regex]::Matches([IO.File]::ReadAllText($app),'(?m)^\\s*(?!//)[^\\r\\n]*\\bProcess\\.Kill\\s*\\(')).Count -gt 0 -or
   ([regex]::Matches([IO.File]::ReadAllText($close),'(?m)^\\s*(?!//)[^\\r\\n]*\\bProcess\\.Kill\\s*\\(')).Count -gt 0){
    throw 'R355 forbids arbitrary process termination'
}
Write-Host 'R355: close consent and App.OnExit complete before any process exit; duplicate widget-host exits without user state.'
