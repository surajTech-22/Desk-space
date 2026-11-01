using System;
using System.Threading;

namespace DeskSpace;

// R255: contain recoverable tool-launch failures inside the Workspace.
// Never hide fatal runtime corruption or emit sensitive exception messages.
internal static class DeskSpaceFeatureActionGuardV255
{
    private static bool IsFatal(Exception e)=>
        e is OutOfMemoryException or AccessViolationException or
        StackOverflowException or ThreadAbortException;

    internal static bool Execute(string caption,Action action,
        Action<string> warning)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(warning);
        try
        {
            action();
            return true;
        }
        catch(Exception ex)when(!IsFatal(ex))
        {
            // Show only a stable exception type; never leak workspace paths,
            // clipboard contents, passwords, filenames or COM error messages.
            string name=ex.GetType().Name;
            string title=string.IsNullOrWhiteSpace(caption)?
                "DeskSpace feature":caption.Trim();
            warning(title+" could not open ("+name+
                "). Workspace remains available.");
            return false;
        }
    }
}
