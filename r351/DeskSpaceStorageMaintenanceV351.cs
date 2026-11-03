using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// R351: run bounded, local-only cleanup after a successfully shown WPF window,
// including manual installer upgrades that bypass the R185 update supervisor.
// A separate hidden Windows PowerShell process keeps disk scanning off UI thread.
internal static class DeskSpaceStorageMaintenanceV351
{
    private static int started;
    internal static void Begin()
    {
        if (Interlocked.Exchange(ref started, 1) != 0) return;
        _ = Task.Run(Launch);
    }
    private static void Launch()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) ||
                !File.Exists(exe) ||
                !string.Equals(Path.GetFileName(exe), "DeskSpace.exe",
                    StringComparison.OrdinalIgnoreCase)) return;
            var assembly = typeof(DeskSpaceStorageMaintenanceV351).Assembly;
            var resource = assembly.GetManifestResourceNames().SingleOrDefault(
                x => x.EndsWith(".DeskSpacePostLaunchCleanupV351.ps1",
                    StringComparison.OrdinalIgnoreCase));
            if (resource == null) return;
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null) return;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var source = reader.ReadToEnd();
            // The build replaces its source placeholder with reviewed
            // helper definitions; validate the EMBEDDED functions themselves.
            if (!source.Contains("function Invoke-R348StorageCleanup",
                    StringComparison.Ordinal) ||
                !source.Contains("function Invoke-R351StagedInstallerRetention",
                    StringComparison.Ordinal)) return;
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace", "Maintenance");
            Directory.CreateDirectory(folder);
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return;
            var script = Path.Combine(folder, "DeskSpacePostLaunchCleanupV351.ps1");
            var temporary = script + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, source, new UTF8Encoding(false));
                File.Move(temporary, script, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            var start = new ProcessStartInfo("powershell.exe") {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = folder
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("-TargetExe");
            start.ArgumentList.Add(exe);
            using var process = Process.Start(start);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or System.ComponentModel.Win32Exception or
            System.Security.SecurityException)
        {
            Debug.WriteLine("R351 storage check skipped: " + ex.GetType().Name);
        }
    }
}
