using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace DeskSpace;

// R188. Do not close the widget before the supervisor knows the executable
// directory can be replaced. Other live processes may contain unsaved work.
// Read process IDs/paths only, never enumerate titles, documents or content.
internal static class DeskSpaceUpdateInstallGateV188
{
    // Pure validation logic is separately exercised by native WPF smoke.
    // If Windows refuses to give us a peer executable path, defer rather than
    // guessing it is safe to replace the program currently in use.
    internal static bool BlocksInstall(int peerPid,int updaterPid,
        string? peerExe,string targetExe,bool pathReadable)
    {
        if(peerPid==updaterPid)return false;
        if(!pathReadable||string.IsNullOrWhiteSpace(peerExe))return true;
        try
        {
            return string.Equals(Path.GetFullPath(peerExe),
                Path.GetFullPath(targetExe),StringComparison.OrdinalIgnoreCase);
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return true;
        }
    }

    internal static bool OtherInstalledProcessRunning(string targetExe)
    {
        var thisPid=Environment.ProcessId;
        Process[] peers;
        try{peers=Process.GetProcessesByName("DeskSpace");}
        catch(Exception e)when(e is Win32Exception or InvalidOperationException)
        {
            // We cannot prove it is safe to replace installed binaries.
            return true;
        }
        bool busy=false;
        try
        {
            foreach(var peer in peers)
            {
                try
                {
                    if(peer.Id==thisPid || peer.HasExited)continue;
                    string? peerExe=null;
                    bool readable=false;
                    try
                    {
                        peerExe=peer.MainModule?.FileName;
                        readable=true;
                    }
                    catch(Exception e)when(e is Win32Exception or
                        InvalidOperationException or NotSupportedException)
                    {
                        // Protect open state: never force-close an unknown peer.
                    }
                    if(BlocksInstall(peer.Id,thisPid,peerExe,targetExe,readable))
                        busy=true;
                }
                catch(InvalidOperationException)
                {
                    // A process that exited meanwhile is not an install blocker.
                }
                catch(Win32Exception)
                {
                    busy=true;
                }
            }
        }
        finally
        {
            foreach(var peer in peers)peer.Dispose();
        }
        return busy;
    }
}
