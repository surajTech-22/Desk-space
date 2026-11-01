using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// R261: deterministic running-app Deck ordering. Apps with multiple distinct
// windows come first (most windows first); the rest are alphabetical. The
// comparison never uses titles, file paths or foreground process state.
internal static class DeskSpaceAppGroupOrderV261
{
    internal static IReadOnlyList<DeskSpaceRunningAppsV88.Entry> Groups(
        IEnumerable<DeskSpaceRunningAppsV88.Entry> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        return windows.Where(x=>!string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new {
                App=g.First(),
                Count=g.Select(x=>x.Handle).Distinct().Count()
            })
            .OrderBy(x=>x.Count>1?0:1)
            .ThenByDescending(x=>x.Count)
            .ThenBy(x=>x.App.Name,StringComparer.OrdinalIgnoreCase)
            .Select(x=>x.App).Take(24).ToArray();
    }

    internal static IReadOnlyList<DeskSpaceRunningAppsV88.Entry> Windows(
        DeskSpaceRunningAppsV88.Entry app,
        IEnumerable<DeskSpaceRunningAppsV88.Entry> windows)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(windows);
        return windows.Where(x=>x.Name.Equals(app.Name,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(x=>x.Handle).Select(x=>x.First())
            .OrderBy(x=>x.WindowTitle,StringComparer.OrdinalIgnoreCase)
            .ThenBy(x=>x.Handle.ToInt64()).ToArray();
    }
}
