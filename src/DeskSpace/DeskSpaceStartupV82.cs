using System;
using System.IO;
using System.Diagnostics;

namespace DeskSpace;

internal static class DeskSpaceStartupV82
{
    // An explicit one-time migration enables Windows logon startup for the
    // existing installation. Thereafter the Settings toggle remains sovereign.
    internal static readonly string Marker=Path.Combine(
        WorkspaceStore.Root,"WindowsWidgetStartupR82.initialized");

    internal static bool EnableOnFirstLaunch(WidgetSettingsV3 preferences)
    {
        try
        {
            if(!File.Exists(Marker))
            {
                preferences.StartWithWindows=true;
                preferences.ShowAtLaunch=true;
                preferences.Save();
                Directory.CreateDirectory(Path.GetDirectoryName(Marker)!);
                File.WriteAllText(Marker,DateTime.UtcNow.ToString("O"));
            }
            preferences.ApplyWindowsStartup();
            return true;
        }
        catch(Exception exception)
        {
            Debug.WriteLine("DeskSpace startup registration: "+exception);
            return false; // never stop the widget from opening
        }
    }
}
