$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = 'DeskSpace_Windows_R1/DeskSpace'
$updater = Join-Path $root 'DeskSpaceUpdater.cs'
if (!(Test-Path -LiteralPath $updater)) { throw 'R7 updater source missing' }
$code = [IO.File]::ReadAllText($updater)
$oldCadence = 'if (timer != null) timer.Interval = TimeSpan.FromHours(2);'
if (!$code.Contains($oldCadence)) { throw 'R7 two-hour steady-state updater interval not found' }
$code = $code.Replace($oldCadence, 'if (timer != null) timer.Interval = TimeSpan.FromMinutes(5);')
$from = '            var process = new ProcessStartInfo(installer)'
$to = '            Application.Current.Shutdown();'
$first = $code.IndexOf($from, [StringComparison]::Ordinal)
if ($first -lt 0) { throw 'R7 installer launch anchor missing' }
$last = $code.IndexOf($to, $first, [StringComparison]::Ordinal)
if ($last -lt 0) { throw 'R7 shutdown anchor missing' }
$last += $to.Length
$replacement = @'
            // The installer cannot reliably replace a still-running WPF executable.
            // An independent PowerShell process waits for this host to exit,
            // installs the already verified EXE, and guarantees host relaunch.
            var supervisor = Path.Combine(targetDir, "CompleteDeskSpaceUpdate.ps1");
            await using (var embedded = typeof(DeskSpaceUpdater).Assembly
                .GetManifestResourceStream("DeskSpace.DeskSpaceUpdateSupervisor.ps1")
                ?? throw new IOException("DeskSpace independent update supervisor is missing."))
            await using (var outputFile = new FileStream(supervisor, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await embedded.CopyToAsync(outputFile);
            }

            var helper = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in new[]
            {
                "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden",
                "-ExecutionPolicy", "Bypass", "-File", supervisor,
                "-Installer", installer,
                "-TargetExe", Path.Combine(AppContext.BaseDirectory, "DeskSpace.exe"),
                "-StatusLog", Path.Combine(WorkspaceStore.Root, "update-status.log"),
                "-OldPid", Environment.ProcessId.ToString(),
            }) helper.ArgumentList.Add(argument);
            if (Process.Start(helper) is null)
                throw new IOException("Windows could not start the independent update supervisor.");
            Record("Verified installer handed to independent supervisor for " + tag + "; closing old widget.");
            Application.Current.Shutdown();
'@
$code = $code.Substring(0,$first) + $replacement + $code.Substring($last)
[IO.File]::WriteAllText($updater, $code, [Text.UTF8Encoding]::new($false))

$resource = Join-Path $root 'DeskSpaceUpdateSupervisor.ps1'
Copy-Item -LiteralPath 'r7/DeskSpaceUpdateSupervisor.ps1' -Destination $resource -Force
$project = Join-Path $root 'DeskSpace.csproj'
$xml = [IO.File]::ReadAllText($project)
if ($xml.Contains('DeskSpaceUpdateSupervisor.ps1')) { throw 'R7 supervisor resource was already added' }
if (!$xml.Contains('</Project>')) { throw 'R7 .csproj document is malformed' }
$xml = $xml.Replace('</Project>', @'
  <ItemGroup>
    <EmbeddedResource Include="DeskSpaceUpdateSupervisor.ps1" />
  </ItemGroup>
</Project>
'@)
[IO.File]::WriteAllText($project, $xml, [Text.UTF8Encoding]::new($false))
$inno='installer/DeskSpace.iss'
$setup=[IO.File]::ReadAllText($inno)
if (!$setup.Contains('Check: WizardSilent') -or !$setup.Contains('Parameters: "--widget-host"')) {
    throw 'R7 silent installer does not relaunch the background widget.'
}
if (!$setup.Contains('CloseApplications=yes')) { throw 'R7 installer cannot safely replace running files' }
$setup = $setup.Replace('AppVerName=DeskSpace R6','AppVerName=DeskSpace R7')
[IO.File]::WriteAllText($inno,$setup,[Text.UTF8Encoding]::new($false))
Write-Host 'R7 prepared: five-minute live checks, independent installer supervision, and verified widget relaunch.'
