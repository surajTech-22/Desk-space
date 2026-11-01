using System;
using System.IO;
using System.Text;
using System.Windows;

namespace DeskSpace;

// R286: "Exit widget" is an explicit persistent user decision. A previous
// Close() only ended the current WPF window; Windows Run / the Workspace's
// ShowAtLaunch and updater relaunch could immediately resurrect it.
// The marker never changes startup preferences, file Deck, widget placement,
// user profiles or unrelated LocalAppData content.
internal static class DeskSpaceWidgetExitV286
{
    private static readonly string FilePath=Path.Combine(
        WorkspaceStore.Root,"widget-user-exited-v286.flag");

    internal static bool Dismissed=>File.Exists(FilePath);

    // Pure gate: legacy startup resumes only if not explicitly dismissed.
    internal static bool MayOpen(bool isExplicitShow,bool dismissed,
        bool showAtLaunch)=>isExplicitShow || (!dismissed && showAtLaunch);

    internal static void MarkAt(string path)
    {
        var parent=Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(parent);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,
                "DeskSpace widget explicitly exited; show only on request.\n",
                new UTF8Encoding(false));
            File.Move(temp,path,true);
        }
        finally
        {
            try{if(File.Exists(temp))File.Delete(temp);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }
    internal static void ClearAt(string path)
    {
        if(File.Exists(path))File.Delete(path);
    }
    internal static void MarkExited()=>MarkAt(FilePath);
    internal static void AllowExplicitShow()=>ClearAt(FilePath);

    internal static void ExitFromTray(Window widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        // The explicit state MUST be durable before closing; on a denied
        // LocalAppData write, refuse to close rather than resurrect after Exit.
        try{MarkExited();}
        catch(Exception error) when(error is IOException or
            UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(widget,
                "DeskSpace could not remember Exit Widget ("+
                error.GetType().Name+"). The widget was left open to "+
                "avoid an unexpected automatic relaunch.",
                "Widget exit not saved",MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        widget.Close(); // ordinary WPF close; no forced process termination
    }
}
