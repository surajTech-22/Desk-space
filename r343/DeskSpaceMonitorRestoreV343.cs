using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// R343: opt-in monitor-scoped window layout restoration. This is a pure
// selection plan: never moves windows and never substitutes an unavailable
// saved monitor with another physical display.
internal static class DeskSpaceMonitorRestoreV343
{
    internal const string All="All saved monitors";

    internal sealed record Summary(int Selected,int Excluded,int DistinctMonitors,
        IReadOnlyList<string> Monitors);

    internal static IReadOnlyList<string> Options(
        DeskSpaceWindowLayoutsV179.Profile profile)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        return new[]{All}.Concat(profile.Windows
            .Select(x=>x.Monitor)
            .Where(x=>!string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    internal static DeskSpaceWindowLayoutsV179.Profile Select(
        DeskSpaceWindowLayoutsV179.Profile profile,string? monitor)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        string desired=string.IsNullOrWhiteSpace(monitor)?All:monitor;
        // A wrong or unplugged monitor never results in a silent fallback to
        // all windows. Return an empty filtered plan and require new preview.
        var windows=string.Equals(desired,All,StringComparison.Ordinal)?
            profile.Windows.ToList():
            profile.Windows.Where(x=>string.Equals(x.Monitor,desired,
                StringComparison.OrdinalIgnoreCase)).ToList();
        return new DeskSpaceWindowLayoutsV179.Profile(profile.Name,windows);
    }

    internal static Summary Describe(
        DeskSpaceWindowLayoutsV179.Profile original,
        DeskSpaceWindowLayoutsV179.Profile selected)
    {
        if(original==null||selected==null)
            throw new ArgumentNullException(original==null?nameof(original):nameof(selected));
        var monitors=selected.Windows.Select(x=>x.Monitor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        return new Summary(selected.Windows.Count,
            original.Windows.Count-selected.Windows.Count,monitors.Length,monitors);
    }
}
