using System;
using System.Windows.Input;

namespace DeskSpace;

// R207: pure root-donut keyboard selection. Exactly two six-item rings.
// No global hotkey, no application launch, no input injection, no cross-widget state.
internal static class DeskSpaceDonutKeyboardV207
{
    internal const int RootSectors=12;
    internal const int SectorsPerRing=6;
    internal static int Next(int current,Key key)
    {
        if(current<0 || current>=RootSectors)current=0;
        return key switch
        {
            Key.Left => (current+RootSectors-1)%RootSectors,
            Key.Right => (current+1)%RootSectors,
            Key.Up => current>=SectorsPerRing?current-SectorsPerRing:current,
            Key.Down => current<SectorsPerRing?current+SectorsPerRing:current,
            Key.Home => 0,
            Key.End => RootSectors-1,
            _ => current
        };
    }
    internal static bool IsNavigation(Key key)=>key is
        Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End;
}
