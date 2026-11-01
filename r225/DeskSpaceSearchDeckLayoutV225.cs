using System;

namespace DeskSpace;

// R225: Search is an actual usable input bar, not the compact donut diameter.
// All sizes are WPF device-independent pixels from the current monitor.
internal static class DeskSpaceSearchDeckLayoutV225
{
    internal static double SearchWidth(double availableDip)
    {
        if(!double.IsFinite(availableDip) || availableDip<=0)return 320;
        // Never exceed the host monitor's allowed width. On a regular screen
        // retain enough room for action, text entry and Close without overlap.
        return Math.Max(96,Math.Min(336,Math.Floor(availableDip)));
    }

    // Search temporarily owns the foreground interaction; retained files and
    // running apps are NOT erased, and the user's persistent Deck preference
    // is not modified. Other monitor widgets remain independent.
    internal static bool ShowDeck(bool enabled,bool hostVisible,
        bool searchActive,bool hasItems)=>
        enabled && hostVisible && !searchActive && hasItems;
}
