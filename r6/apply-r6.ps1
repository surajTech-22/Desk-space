$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Replace-Single([string]$name, [string]$old, [string]$new) {
    if (!(Test-Path -LiteralPath $name)) { throw "R6 source missing: $name" }
    $content = [IO.File]::ReadAllText($name)
    if (!$content.Contains($old)) { throw "R6 patch anchor missing in $name : $old" }
    $changed = $content.Replace($old, $new)
    [IO.File]::WriteAllText($name, $changed, [Text.UTF8Encoding]::new($false))
}
$root = 'DeskSpace_Windows_R1/DeskSpace'
$source = "$root/DeskSpaceUpdater.cs"
Replace-Single $source '    private static bool initialPromptShown;' @'
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
'@
Replace-Single $source 'timer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };' 'timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };'
Replace-Single $source '        if (automatic && !Load().AutomaticUpdates) return;' @'
        if (automatic && !Load().AutomaticUpdates) return;
        Record("Checking for updates" + (automatic ? " automatically" : " manually"));
'@
Replace-Single $source '                var state = Load();' @'
                Record("GitHub authentication unavailable; retrying within 5 minutes");
                if (timer != null) timer.Interval = TimeSpan.FromMinutes(5);
                var state = Load();
'@
Replace-Single $source '            var release = await RunGh(new[] { "release", "view", "--repo", Repository, "--json", "tagName,assets" });' @'
            Record("GitHub authenticated");
            if (timer != null) timer.Interval = TimeSpan.FromHours(2);
            var release = await RunGh(new[] { "release", "view", "--repo", Repository, "--json", "tagName,assets" });
'@
Replace-Single $source '            if (available <= InstalledVersion)' @'
            Record("Installed " + InstalledVersion + ", latest " + tag);
            if (available <= InstalledVersion)
'@
Replace-Single $source '            var download = await RunGh(new[] { "release", "download", tag, "--repo", Repository,' @'
            Record("Downloading verified update " + tag);
            var download = await RunGh(new[] { "release", "download", tag, "--repo", Repository,
'@
Replace-Single $source '            if (Process.Start(process) == null) throw new IOException("Windows could not start the updater.");' @'
            if (Process.Start(process) == null) throw new IOException("Windows could not start the updater.");
            Record("Verified installer launched for " + tag);
'@
Replace-Single $source '            if (!automatic) MessageBox.Show(owner, "Update was not installed: " + ex.Message,' @'
            Record("Update failed: " + ex.GetType().Name + " " + ex.Message);
            if (!automatic) MessageBox.Show(owner, "Update was not installed: " + ex.Message,
'@
Replace-Single $source '        return (process.ExitCode, await output);' @'
        var stdout = await output;
        var stderr = await error;
        // Keep JSON intact on success; otherwise expose GitHub's actual error.
        return (process.ExitCode, process.ExitCode == 0 ? stdout : Limit(stderr + " " + stdout));
'@
$widget = "$root/WidgetHost.cs"
Replace-Single $widget 'menu.Items.Add("Universal Search", null, (_, _) => Dispatcher.Invoke(OpenSearch));' @'
menu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdater.CheckAsync(this)));
        menu.Items.Add("Universal Search", null, (_, _) => Dispatcher.Invoke(OpenSearch));
'@
Write-Host 'R6 updater includes sign-in retry, status diagnostics and widget-tray manual checks.'
