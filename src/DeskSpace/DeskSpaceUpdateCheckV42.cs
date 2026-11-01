using System.Threading.Tasks;
using System.Windows;

namespace DeskSpace;

// Route the Workspace and Widget "Check for Updates" actions through the
// existing SHA-256-verified, supervised stable-release updater.
// R42 preview tags must never intercept or block production auto-updates.
internal static class DeskSpaceUpdateCheckV42
{
    public static Task CheckAsync(Window owner) => DeskSpacePublicUpdaterV44.CheckAsync(owner);
}
