using System.Windows;
using System.Windows.Media;

namespace DeskSpace;

// R209: honor Windows accessibility preferences rather than requiring
// separate per-widget toggles. No background polling or registry writes.
internal static class DeskSpaceReducedMotionV209
{
    internal static bool ShouldAnimate(bool clientAreaAnimation,
        bool menuAnimation,bool highContrast)
        =>clientAreaAnimation && menuAnimation && !highContrast;

    internal static bool Enabled()=>ShouldAnimate(
        SystemParameters.ClientAreaAnimation,
        SystemParameters.MenuAnimation,
        SystemParameters.HighContrast);

    internal static void Reset(FrameworkElement surface)
    {
        // Clears any opacity clock; never leave a "frozen" invisible panel
        // when the user changes Windows accessibility settings.
        surface.BeginAnimation(UIElement.OpacityProperty,null);
        surface.Opacity=1;
        if(surface.RenderTransform is TranslateTransform offset)
        {
            offset.BeginAnimation(TranslateTransform.XProperty,null);
            offset.BeginAnimation(TranslateTransform.YProperty,null);
            surface.RenderTransform=Transform.Identity;
        }
        else if(surface.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
            surface.RenderTransform=Transform.Identity;
        }
    }
}
