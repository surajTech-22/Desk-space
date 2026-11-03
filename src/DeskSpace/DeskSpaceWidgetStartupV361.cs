using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace DeskSpace;

// R361: Existing named mutex != running widget. The kernel object can outlive
// its owner while another process holds a handle. Test ownership, not creation.
internal static class DeskSpaceWidgetStartupV361
{
    internal enum Outcome { BecameOwner, SignaledExisting, UnresponsiveExisting }

    internal static Outcome ResolveExisting(
        Mutex mutex, string pipeName,
        int attempts=8, int connectMilliseconds=250, int retryMilliseconds=125)
    {
        ArgumentNullException.ThrowIfNull(mutex);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if(attempts<1 || attempts>20 || connectMilliseconds<1 ||
           connectMilliseconds>2000 || retryMilliseconds<0 ||
           retryMilliseconds>2000)
            throw new ArgumentOutOfRangeException(nameof(attempts));
        // When a stale mutex object still exists without an owner, acquiring
        // it makes us the one legitimate new widget host. No duplicate tray.
        if(TryBecomeOwner(mutex))
        {
            Audit("recovered orphaned singleton mutex");
            return Outcome.BecameOwner;
        }
        // The other owner may be inside WidgetShellWindow construction and
        // not have bound its NamedPipeServerStream yet. Single 400ms connects
        // used to silently drop valid startup/show requests during logon.
        for(int attempt=0; attempt<attempts; attempt++)
        {
            try
            {
                using var client=new NamedPipeClientStream(
                    ".",pipeName,PipeDirection.Out);
                client.Connect(connectMilliseconds);
                client.WriteByte(1); // existing Show Widget command unchanged
                client.Flush();
                Audit("forwarded launch to existing widget");
                return Outcome.SignaledExisting;
            }
            catch(Exception error) when(error is TimeoutException or
                IOException or UnauthorizedAccessException or
                InvalidOperationException)
            {
                // Retry while the primary HWND/tray/pipe are starting.
            }
            // The primary can exit during our wait. Acquire only after it
            // releases, so we never create a second widget on the same run.
            if(TryBecomeOwner(mutex))
            {
                Audit("acquired singleton after prior widget exit");
                return Outcome.BecameOwner;
            }
            if(attempt+1<attempts && retryMilliseconds>0)
                Thread.Sleep(retryMilliseconds);
        }
        Audit("existing widget owns mutex but command pipe did not respond");
        return Outcome.UnresponsiveExisting;
    }

    private static bool TryBecomeOwner(Mutex mutex)
    {
        try { return mutex.WaitOne(0); }
        catch(AbandonedMutexException) { return true; } // now owned
    }

    internal static void Audit(string status)
    {
        // Non-sensitive state only; never scan user files or auto-restart.
        try
        {
            string root=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root,"widget-startup.log"),
                DateTime.UtcNow.ToString("O")+" R361 pid="+Environment.ProcessId+
                " "+status+Environment.NewLine);
        }
        catch(Exception error) when(error is IOException or
            UnauthorizedAccessException or ArgumentException){}
    }
}
