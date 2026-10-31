using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeskSpace;

// Releases are hosted in the user's PRIVATE GitHub repository.
// GitHub CLI uses its normal Windows credential store after the user approves login once.
// DeskSpace never ships or stores any GitHub token or password.
public static class DeskSpaceUpdater
{
    private const string Repository = "surajTech-22/Desk-space";
    private static readonly Version InstalledVersion = new(1, 5, 1093);
    public static string CurrentVersion => InstalledVersion.ToString();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string Gh = Path.Combine(AppContext.BaseDirectory, "gh.exe");
    private static readonly string PreferencesFile = Path.Combine(WorkspaceStore.Root, "update-preferences.json");
    private static DispatcherTimer? timer;
    private static bool initialPromptShown;

    // Local diagnostics never contain GitHub passwords or authentication tokens.
    private static void Record(string step)
    {
        try
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            File.AppendAllText(Path.Combine(WorkspaceStore.Root, "update-status.log"),
                DateTime.UtcNow.ToString("O") + " " + step + Environment.NewLine);
        }
        catch { /* Diagnostics must not prevent normal operation. */ }
    }

    private sealed class Preferences
    {
        public bool AutomaticUpdates { get; set; } = true;
        public bool HasSeenSignInPrompt { get; set; }
    }
    private static Preferences Load()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesFile)) ?? new(); }
        catch { return new(); }
    }
    private static void Save(Preferences preferences)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var staging = PreferencesFile + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(preferences));
        File.Move(staging, PreferencesFile, true);
    }

    // Called by the independent widget host. The main DeskSpace window may be closed.
    // R39: no idle-only gate or several-minute delay at application start.
    // Check as soon as the widget loads; retry failures and watch for new
    // releases at most once per minute without blocking the UI.
    // R44: all widget auto checks use the anonymous stable update channel.
    // Legacy per-PC manual preference never blocks approved automatic updates.
    public static void StartAutomaticChecks(Window host)
    {
        DeskSpacePublicUpdaterV44.StartAutomaticChecks(host);
    }
    public static async Task CheckAsync(Window? owner, bool automatic = false)
    {
        // Legacy manual-only preference no longer prevents stable auto-updates.
        Record("Checking for updates" + (automatic ? " automatically" : " manually"));
        if (!await Gate.WaitAsync(0)) return;
        bool handedOff = false;
        try
        {
            if (!File.Exists(Gh))
            {
                if (!automatic) MessageBox.Show(owner, "The bundled GitHub updater is missing. Reinstall the latest verified DeskSpace build.", "DeskSpace Updates");
                return;
            }
            var auth = await RunGh(new[] { "auth", "status", "--hostname", "github.com" });
            if (auth.ExitCode != 0)
            {
                Record("GitHub authentication unavailable; retrying within 5 minutes");
                if (timer != null) timer.Interval = TimeSpan.FromMinutes(1);
                var state = Load();
                if (!automatic || (!state.HasSeenSignInPrompt && !initialPromptShown))
                {
                    initialPromptShown = true;
                    state.HasSeenSignInPrompt = true; Save(state);
                    var result = MessageBox.Show(owner,
                        "DeskSpace can now install future updates automatically from your private GitHub repository.\n\n" +
                        "GitHub needs you to authorize this Windows PC ONCE. Your GitHub sign-in is managed by the official GitHub CLI and stored in Windows, not in DeskSpace.\n\n" +
                        "Connect GitHub now?",
                        "Enable DeskSpace automatic updates", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes) OpenGitHubLogin();
                }
                return;
            }
            Record("GitHub authenticated");
            if (timer != null) timer.Interval = TimeSpan.FromMinutes(1);
            var release = await RunGh(new[] { "release", "view", "--repo", Repository, "--json", "tagName,assets" });
            if (release.ExitCode != 0) // No published release or access revoked.
            {
                if (!automatic) MessageBox.Show(owner, "No accessible DeskSpace release yet. " + Limit(release.Output), "DeskSpace Updates");
                return;
            }
            using var json = JsonDocument.Parse(release.Output);
            var tag = json.RootElement.GetProperty("tagName").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var available))
                throw new InvalidDataException("GitHub release has an invalid version.");
            Record("Installed " + InstalledVersion + ", latest " + tag);
            if (available <= InstalledVersion)
            {
                if (!automatic) MessageBox.Show(owner, $"DeskSpace {InstalledVersion} is current.", "DeskSpace Updates");
                return;
            }
            var assets = json.RootElement.GetProperty("assets").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToHashSet();
            if (!assets.Contains("DeskSpace_Setup.exe") || !assets.Contains("DeskSpace_Setup.sha256.txt"))
                throw new InvalidDataException("Verified GitHub release is missing the installer or checksum.");

            DeskSpaceUpdateProgress.Publish("New version " + tag);
            // Never run a downloaded installer before its SHA-256 has been checked.
            var targetDir = Path.Combine(WorkspaceStore.Root, "updates", tag);
            Directory.CreateDirectory(targetDir);
            var installer = Path.Combine(targetDir, "DeskSpace_Setup.exe");
            var checksum = Path.Combine(targetDir, "DeskSpace_Setup.sha256.txt");
            foreach (var file in new[] { installer, checksum }) if (File.Exists(file)) File.Delete(file);
            Record("Downloading verified update " + tag);
            DeskSpaceUpdateProgress.Publish("Downloading",0);
            // gh runs asynchronously. Poll at 1 Hz; no busy loop, UI blocking,
            // extra process or per-frame disk IO.
            long expectedSize=0;
            foreach(var asset in json.RootElement.GetProperty("assets").EnumerateArray())
                if(asset.GetProperty("name").GetString() == "DeskSpace_Setup.exe" &&
                    asset.TryGetProperty("size",out var size) && size.TryGetInt64(out var assetSize))
                    expectedSize=assetSize;
            var downloadTask = RunGh(new[] { "release", "download", tag, "--repo", Repository,
                "--pattern", "DeskSpace_Setup.exe", "--pattern", "DeskSpace_Setup.sha256.txt", "--dir", targetDir });
            long lastBytes = -1;
            while (!downloadTask.IsCompleted)
            {
                await Task.Delay(1000);
                long bytes = File.Exists(installer) ? new FileInfo(installer).Length : 0;
                if (bytes == lastBytes) continue;
                lastBytes = bytes;
                if (expectedSize > 0)
                    DeskSpaceUpdateProgress.Publish("Downloading",
                        (int)Math.Clamp(bytes * 100 / expectedSize,0,99));
                else DeskSpaceUpdateProgress.Publish("Downloading " + (bytes / 1048576) + " MB");
            }
            var download=await downloadTask;            if (download.ExitCode != 0) throw new IOException("GitHub download failed: " + Limit(download.Output));
            if (!File.Exists(installer) || !File.Exists(checksum)) throw new IOException("Update download is incomplete.");
            var expected = (await File.ReadAllTextAsync(checksum)).Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid update checksum.");
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            await using (var input = File.OpenRead(installer))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(installer);
                    throw new InvalidDataException("Installer checksum mismatch. Update blocked.");
                }
            }
            // One automatic action after initial opt-in: the silent installer closes DeskSpace
            // via Inno Setup Restart Manager, preserves LocalAppData workspace, and restarts widget.
            // The installer cannot reliably replace a still-running WPF executable.
            // An independent PowerShell process waits for this host to exit,
            // installs the already verified EXE, and guarantees host relaunch.
            DeskSpaceUpdateProgress.Publish("Installing",100);
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
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            handedOff = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Record("Update failed: " + ex.GetType().Name + " " + ex.Message);
            if (!automatic) MessageBox.Show(owner, "Update was not installed: " + ex.Message, "DeskSpace Updates", MessageBoxButton.OK, MessageBoxImage.Error);
            try { File.AppendAllText(Path.Combine(WorkspaceStore.Root, "update-errors.log"), DateTime.UtcNow.ToString("O") + " " + ex.GetType().Name + " " + ex.Message + Environment.NewLine); } catch { }
        }
        finally
        {
            if (!handedOff) DeskSpaceUpdateProgress.Clear();
            Gate.Release();
        }
    }

    private static void OpenGitHubLogin()
    {
        // Launch an interactive terminal: passwords and codes never pass through DeskSpace.
        var escaped = Gh.Replace("'", "''");
        var command = $"& '{escaped}' auth login --hostname github.com --git-protocol https --web; Write-Host 'GitHub authorization completed. You can close this window.'";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        Process.Start(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = true,
            Arguments = "-NoExit -EncodedCommand " + encoded
        });
    }

    private static async Task<(int ExitCode, string Output)> RunGh(IEnumerable<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(Gh) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new IOException("GitHub updater process could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw new TimeoutException("GitHub updater request timed out."); }
        var stdout = await output;
        var stderr = await error;
        // Keep JSON intact on success; otherwise expose GitHub's actual error.
        return (process.ExitCode, process.ExitCode == 0 ? stdout : Limit(stderr + " " + stdout));
    }
    private static string Limit(string text) => text.Length > 250 ? text.Substring(0, 250) : text;
}
