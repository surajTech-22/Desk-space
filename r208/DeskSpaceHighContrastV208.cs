using System.Windows;
using System.Windows.Media;

namespace DeskSpace;

// R208: use Windows system colors when OS High Contrast is active.
// Pure color routing, no theme persistence or external UI hooks.
internal static class DeskSpaceHighContrastV208
{
    internal static Brush Wedge(bool highContrast,int index,bool selected,
        Brush normalPrimary,Brush normalSecondary,Brush normalSelected)
    {
        if(!highContrast)return selected?normalSelected:
            index%2==0?normalPrimary:normalSecondary;
        return selected?SystemColors.HighlightBrush:
            index%2==0?SystemColors.WindowBrush:SystemColors.ControlBrush;
    }
    internal static Brush Outline(bool highContrast)=>
        highContrast?SystemColors.WindowTextBrush:Brushes.White;

    internal static Brush Label(bool highContrast,Brush normal)=>
        highContrast?SystemColors.WindowTextBrush:normal;

    internal static Brush QuickAction(bool highContrast,Brush normal)=>
        highContrast?SystemColors.HighlightBrush:normal;

    internal static Brush QuickActionLabel(bool highContrast)=>
        highContrast?SystemColors.HighlightTextBrush:Brushes.White;
}
