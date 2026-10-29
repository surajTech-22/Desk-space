using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace DeskSpace;

// A receipt is written only after the primary WPF widget has Loaded.
// This is a local transaction handshake, not a network call/telemetry.
// No data or private paths are recorded. The independent updater requires
// the exact new release version and nonce before it marks an install healthy.
internal static class DeskSpacePostInstallReceiptV185
{
    internal sealed record Pending(string Transaction,string Version);
    internal sealed record Receipt(string Transaction,string Version,int Pid);
    internal static string DirectoryPath=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","Updates","PostInstall");
    internal static string PendingPath=>Path.Combine(DirectoryPath,"pending.json");
    internal static string ReceiptPath=>Path.Combine(DirectoryPath,"ready.json");

    internal static bool ShouldAcknowledge(Pending? pending,string version)
    {
        return pending!=null &&
            Guid.TryParseExact(pending.Transaction,"N",out _) &&
            Version.TryParse(pending.Version,out var requested) &&
            Version.TryParse(version,out var actual) && actual==requested;
    }

    internal static void Ready(Window owner)
    {
        if(!owner.IsLoaded || DeskSpaceMultiMonitorV151.IsSatellite(owner))
            return;
        try
        {
            if(!File.Exists(PendingPath) ||
               new FileInfo(PendingPath).Length>4096)return;
            var pending=JsonSerializer.Deserialize<Pending>(
                File.ReadAllText(PendingPath,Encoding.UTF8));
            var version=DeskSpaceUpdater.CurrentVersion;
            if(!ShouldAcknowledge(pending,version))return;
            Directory.CreateDirectory(DirectoryPath);
            string temp=ReceiptPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                var receipt=new Receipt(pending!.Transaction,version,
                    Environment.ProcessId);
                File.WriteAllText(temp,JsonSerializer.Serialize(receipt),
                    new UTF8Encoding(false));
                File.Move(temp,ReceiptPath,true);
            }
            finally
            {
                if(File.Exists(temp))File.Delete(temp);
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine("DeskSpace postinstall receipt unavailable: "+
                error.GetType().Name);
        }
    }
}
