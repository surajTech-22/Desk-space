using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;

namespace DeskSpace;

// Keep transient per-monitor Deck storage failures out of WPF mouse handlers.
// Preserves the in-memory position and never changes another monitor's state.
// Unexpected programming errors still fail normally, rather than disappearing.
internal static class DeskSpaceDeckSaveGuardV265
{
    internal static bool TrySave(Action save)
    {
        if(save==null)throw new ArgumentNullException(nameof(save));
        try
        {
            save();
            return true;
        }
        catch(Exception error)when(error is IOException or InvalidDataException or
            UnauthorizedAccessException or SecurityException or
            WaitHandleCannotBeOpenedException)
        {
            // Do not log the monitor identity, user files, paths, or offsets.
            Trace.WriteLine("DeskSpace Deck position not persisted ("+
                error.GetType().Name+"); move Deck to retry.");
            return false;
        }
    }
}
