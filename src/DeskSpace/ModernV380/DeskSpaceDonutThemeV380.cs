using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace DeskSpace;

internal sealed record DonutWedgeVisualV380(
    Brush Fill,
    Brush Stroke,
    double StrokeThickness,
    Brush TextBrush,
    Effect? Effect);

internal sealed record DonutHubVisualV380(
    Brush Fill,
    Brush Stroke,
    double StrokeThickness,
    Brush TextBrush,
    Effect? Effect);

/// <summary>
/// Unified Donut & Sub-Donut visual styles (Liquid Glass, Floating Petals, Neon Cyber,
/// Aurora Gradient, Minimal Wireframe, Classic Synced), composable mix layers, 7 open/hover
/// transitions, and instant click-through full-name callout pills for truncated sub-donut labels.
/// </summary>
internal static class DeskSpaceDonutThemeV380
{
    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    private static Color Mix(Color a, Color b, double frac) =>
        DeskSpaceAppearanceV148.Mix(a, b, Math.Clamp(frac, 0, 1));

    internal static string FriendlyStyleName(string? id) => id switch
    {
        "FloatingPetals" => "Floating Petals",
        "NeonCyber" => "Neon Cyber",
        "AuroraGradient" => "Aurora Gradient",
        "MinimalWireframe" => "Minimal Wireframe",
        "ClassicSynced" => "Classic Synced",
        _ => "Liquid Glass"
    };

    internal static string FriendlyTransitionName(string? id) => id switch
    {
        "IrisUnfold" => "Iris Unfold",
        "SmoothGlide" => "Smooth Glide",
        "RadialPop" => "Radial Pop",
        "SpiralSpin" => "Spiral Spin",
        "FadeSlide" => "Fade Slide",
        "Instant" => "Off (Instant)",
        _ => "Spring Bloom"
    };

    internal static double GetMainGapDegrees(DeskSpaceAppearanceV148? app = null)
    {
        var cur = app ?? DeskSpaceAppearanceV148.Current;
        return cur.MixFloatingGaps ? 3.8 : 1.5;
    }

    internal static double GetSubArcGapInset(DeskSpaceAppearanceV148? app = null)
    {
        var cur = app ?? DeskSpaceAppearanceV148.Current;
        return cur.MixFloatingGaps ? 0.9 : 0.0;
    }

    internal static DonutWedgeVisualV380 ResolveWedge(
        DeskSpaceAppearanceV148? app,
        bool highContrast,
        int depth,
        int index,
        bool isAction,
        bool isSelected,
        bool isHovered)
    {
        if (highContrast)
        {
            Brush hcFill = isHovered || isSelected
                ? SystemColors.HighlightBrush
                : isAction
                    ? SystemColors.HighlightBrush
                    : (index % 2 == 0 ? SystemColors.WindowBrush : SystemColors.ControlBrush);
            Brush hcText = isHovered || isSelected || isAction
                ? SystemColors.HighlightTextBrush
                : SystemColors.WindowTextBrush;
            return new DonutWedgeVisualV380(hcFill, SystemColors.WindowTextBrush, isHovered ? 2.5 : 2.0, hcText, null);
        }

        var cur = DeskSpaceAppearanceV148.Sanitize(DeskSpaceAppearanceV148.Clone(app ?? DeskSpaceAppearanceV148.Current));
        bool dark = DeskSpaceAppearanceV148.IsDarkFor(cur);
        Color accent = DeskSpaceAppearanceV148.TryColor(cur.Accent, out var ac) ? ac : Color.FromRgb(130, 145, 255);
        Color secondaryCompanion = Mix(accent, Color.FromRgb(56, 189, 248), 0.45);
        double softnessShift = (cur.Softness - 16) * 0.004;

        bool neon = cur.MixNeonEdge || cur.DonutStyle == "NeonCyber";
        bool glass = cur.MixGlassFrost;
        bool petals = cur.MixFloatingGaps;
        bool aurora = cur.MixAuroraGradient;
        bool wire = cur.MixWireframe;

        // Compute base interior color respecting dark/light, depth ring layering, and parity
        bool neonDarkCanvas = neon && !glass && !wire && !dark && cur.DonutStyle == "NeonCyber";
        bool effectiveDark = dark || neonDarkCanvas;

        Color darkBase0 = (index % 2 == 0) ? Color.FromRgb(22, 29, 46) : Color.FromRgb(29, 38, 58);
        Color lightBase0 = (index % 2 == 0) ? Color.FromRgb(249, 251, 255) : Color.FromRgb(236, 242, 252);
        Color ringTint = effectiveDark ? darkBase0 : lightBase0;

        if (depth == 1)
            ringTint = Mix(ringTint, accent, effectiveDark ? 0.14 : 0.10);
        else if (depth == 2)
            ringTint = Mix(ringTint, accent, effectiveDark ? 0.22 : 0.14);
        else if (depth >= 3)
            ringTint = Mix(ringTint, secondaryCompanion, effectiveDark ? 0.28 : 0.19);

        if (cur.Style == "Soft")
            ringTint = Mix(ringTint, effectiveDark ? Color.FromRgb(42, 46, 64) : Colors.White, 0.20 + softnessShift);
        else
            ringTint = Mix(ringTint, accent, Math.Max(0, 0.04 + softnessShift));

        bool active = isHovered || isSelected;
        Color fillColor = isAction
            ? Mix(accent, effectiveDark ? Colors.White : Colors.Black, isHovered ? 0.14 : 0.04)
            : active
                ? Mix(ringTint, accent, neon ? 0.28 : (effectiveDark ? 0.56 : 0.46))
                : ringTint;

        byte alpha = 255;
        if (wire)
            alpha = (byte)(isAction ? 105 : active ? 92 : (glass ? 78 : 66));
        else if (glass)
            alpha = (byte)(isAction ? 232 : active ? 222 : (effectiveDark ? 196 : 208));
        else if (neon && !aurora)
            alpha = (byte)(active || isAction ? 238 : 230);

        Brush fillBrush;
        if (aurora)
        {
            Color gStart = isAction
                ? Mix(accent, secondaryCompanion, 0.25)
                : active
                    ? accent
                    : Mix(ringTint, accent, depth switch { 0 => 0.14, 1 => 0.28, 2 => 0.40, _ => 0.52 });
            Color gEnd = isAction
                ? Mix(secondaryCompanion, accent, 0.35)
                : active
                    ? secondaryCompanion
                    : Mix(gStart, secondaryCompanion, effectiveDark ? 0.38 : 0.30);
            fillBrush = new LinearGradientBrush(WithAlpha(gStart, alpha), WithAlpha(gEnd, alpha), depth >= 2 ? 35 : 125);
        }
        else
        {
            fillBrush = new SolidColorBrush(WithAlpha(fillColor, alpha));
        }

        // Compute composable border stroke & thickness
        Color strokeColor;
        double strokeThickness;
        if (neon)
        {
            strokeColor = (active || isAction)
                ? Mix(accent, Colors.White, 0.22)
                : WithAlpha(accent, (byte)(effectiveDark ? 175 : 195));
            strokeThickness = active || isAction ? 2.2 : (wire ? 1.5 : 1.35);
        }
        else if (wire)
        {
            strokeColor = (active || isAction)
                ? accent
                : (effectiveDark ? WithAlpha(Color.FromRgb(226, 232, 240), 170) : WithAlpha(Color.FromRgb(51, 65, 85), 170));
            strokeThickness = active || isAction ? 2.1 : 1.35;
        }
        else if (glass)
        {
            strokeColor = active && cur.MotionHoverGlow
                ? Mix(accent, Colors.White, 0.20)
                : effectiveDark
                    ? WithAlpha(Colors.White, active ? (byte)170 : (byte)86)
                    : WithAlpha(Colors.White, 238);
            strokeThickness = active ? 1.9 : 1.25;
        }
        else if (petals)
        {
            strokeColor = active
                ? accent
                : effectiveDark
                    ? WithAlpha(Colors.White, 42)
                    : Color.FromArgb(140, 165, 180, 205);
            strokeThickness = active ? 1.8 : 1.15;
        }
        else
        {
            // Aurora / Classic Synced
            strokeColor = active && cur.MotionHoverGlow
                ? Mix(accent, Colors.White, 0.18)
                : effectiveDark
                    ? (aurora ? WithAlpha(Colors.White, 68) : Color.FromRgb(16, 22, 34))
                    : Colors.White;
            strokeThickness = active ? 2.0 : (depth >= 2 ? 1.5 : 1.85);
        }

        // Glow / elevation effect (composited cleanly: neon or hover glow wins, otherwise petal elevation)
        Effect? effect = null;
        if ((neon && (active || isAction || cur.DonutStyle == "NeonCyber")) || (cur.MotionHoverGlow && isHovered))
        {
            effect = new DropShadowEffect
            {
                Color = accent,
                BlurRadius = isHovered ? 13 : (active || isAction ? 9 : 5),
                ShadowDepth = 0,
                Opacity = isHovered ? 0.85 : (neon ? 0.55 : 0.45)
            };
        }
        else if (petals)
        {
            effect = new DropShadowEffect
            {
                Color = Color.FromRgb(4, 10, 24),
                BlurRadius = isHovered ? 9 : 6,
                ShadowDepth = isHovered ? 3 : 2,
                Direction = 270,
                Opacity = effectiveDark ? 0.45 : 0.22
            };
        }

        // Ensure crisp text contrast across every combination
        Color textColor;
        if (isAction)
        {
            textColor = wire && !effectiveDark ? Color.FromRgb(15, 23, 42) : DeskSpaceDesignV283.OnAccent(accent);
        }
        else if (wire)
        {
            textColor = effectiveDark ? Color.FromRgb(248, 250, 252) : Color.FromRgb(15, 23, 42);
        }
        else if (active)
        {
            textColor = effectiveDark || aurora ? Colors.White : Color.FromRgb(15, 23, 42);
        }
        else
        {
            textColor = effectiveDark ? Color.FromRgb(241, 245, 249) : Color.FromRgb(24, 34, 58);
        }

        return new DonutWedgeVisualV380(
            fillBrush,
            new SolidColorBrush(strokeColor),
            strokeThickness,
            new SolidColorBrush(textColor),
            effect);
    }

    internal static DonutHubVisualV380 ResolveHub(DeskSpaceAppearanceV148? app, bool highContrast)
    {
        if (highContrast)
        {
            return new DonutHubVisualV380(
                SystemColors.WindowBrush,
                SystemColors.WindowTextBrush,
                3.0,
                SystemColors.WindowTextBrush,
                null);
        }

        var cur = DeskSpaceAppearanceV148.Sanitize(DeskSpaceAppearanceV148.Clone(app ?? DeskSpaceAppearanceV148.Current));
        bool dark = DeskSpaceAppearanceV148.IsDarkFor(cur);
        Color accent = DeskSpaceAppearanceV148.TryColor(cur.Accent, out var ac) ? ac : Color.FromRgb(130, 145, 255);
        Color companion = Mix(accent, Color.FromRgb(56, 189, 248), 0.45);
        bool neon = cur.MixNeonEdge || cur.DonutStyle == "NeonCyber";

        if (cur.MixWireframe)
        {
            return new DonutHubVisualV380(
                new SolidColorBrush(WithAlpha(dark ? Color.FromRgb(15, 23, 42) : Colors.White, 165)),
                new SolidColorBrush(neon ? accent : (dark ? WithAlpha(Colors.White, 165) : Color.FromRgb(51, 65, 85))),
                2.2,
                new SolidColorBrush(dark ? Colors.White : Color.FromRgb(15, 23, 42)),
                neon ? new DropShadowEffect { Color = accent, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.6 } : null);
        }

        if (neon)
        {
            return new DonutHubVisualV380(
                new SolidColorBrush(Color.FromArgb(242, 12, 17, 29)),
                new SolidColorBrush(accent),
                2.6,
                Brushes.White,
                new DropShadowEffect { Color = accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.65 });
        }

        if (cur.MixAuroraGradient)
        {
            return new DonutHubVisualV380(
                new LinearGradientBrush(
                    WithAlpha(accent, cur.MixGlassFrost ? (byte)228 : (byte)255),
                    WithAlpha(companion, cur.MixGlassFrost ? (byte)228 : (byte)255),
                    135),
                new SolidColorBrush(dark ? WithAlpha(Colors.White, 145) : Colors.White),
                2.5,
                new SolidColorBrush(DeskSpaceDesignV283.OnAccent(accent)),
                null);
        }

        if (cur.MixGlassFrost)
        {
            Color glassCenter = Mix(accent, dark ? Color.FromRgb(22, 30, 50) : Colors.White, 0.34);
            return new DonutHubVisualV380(
                new SolidColorBrush(WithAlpha(glassCenter, 224)),
                new SolidColorBrush(WithAlpha(Colors.White, dark ? (byte)140 : (byte)235)),
                2.4,
                new SolidColorBrush(dark || DeskSpaceDesignV283.ContrastRatio(Colors.White, glassCenter) >= 3.2
                    ? Colors.White
                    : Color.FromRgb(15, 23, 42)),
                new DropShadowEffect { Color = Color.FromRgb(4, 10, 24), BlurRadius = 8, ShadowDepth = 2, Opacity = 0.25 });
        }

        return new DonutHubVisualV380(
            new SolidColorBrush(accent),
            new SolidColorBrush(dark ? Color.FromRgb(220, 228, 245) : Colors.White),
            3.0,
            new SolidColorBrush(DeskSpaceDesignV283.OnAccent(accent)),
            null);
    }

    internal static (Brush Bg, Brush Border, Brush Text, Brush BadgeBg, Brush BadgeFg) ResolveCalloutColors(
        DeskSpaceAppearanceV148? app,
        bool highContrast)
    {
        if (highContrast)
        {
            return (
                SystemColors.WindowBrush,
                SystemColors.WindowTextBrush,
                SystemColors.WindowTextBrush,
                SystemColors.HighlightBrush,
                SystemColors.HighlightTextBrush);
        }

        var cur = DeskSpaceAppearanceV148.Sanitize(DeskSpaceAppearanceV148.Clone(app ?? DeskSpaceAppearanceV148.Current));
        bool dark = DeskSpaceAppearanceV148.IsDarkFor(cur) || cur.DonutStyle == "NeonCyber";
        Color accent = DeskSpaceAppearanceV148.TryColor(cur.Accent, out var ac) ? ac : Color.FromRgb(130, 145, 255);

        Color bg = dark ? Color.FromArgb(242, 16, 22, 36) : Color.FromArgb(247, 255, 255, 255);
        Color text = dark ? Color.FromRgb(248, 250, 252) : Color.FromRgb(15, 23, 42);
        return (
            new SolidColorBrush(bg),
            new SolidColorBrush(accent),
            new SolidColorBrush(text),
            new SolidColorBrush(accent),
            new SolidColorBrush(DeskSpaceDesignV283.OnAccent(accent)));
    }

    internal static void AnimateOpen(
        FrameworkElement root,
        IReadOnlyList<FrameworkElement>? wedgeVisuals,
        double originX,
        double originY,
        double directionAngleDeg,
        bool isSubArc,
        DeskSpaceAppearanceV148? overrideApp = null)
    {
        if (root == null) return;
        var cur = overrideApp ?? DeskSpaceAppearanceV148.Current;
        string transition = cur.OpenTransition;

        root.BeginAnimation(UIElement.OpacityProperty, null);
        if (!DeskSpaceReducedMotionV209.Enabled() || string.Equals(transition, "Instant", StringComparison.OrdinalIgnoreCase))
        {
            root.Opacity = 1;
            root.RenderTransform = Transform.Identity;
            if (wedgeVisuals != null)
            {
                foreach (var w in wedgeVisuals)
                {
                    w.BeginAnimation(UIElement.OpacityProperty, null);
                    w.Opacity = 1;
                }
            }
            return;
        }

        var scale = new ScaleTransform(1, 1, originX, originY);
        var rotate = new RotateTransform(0, originX, originY);
        var translate = new TranslateTransform(0, 0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(rotate);
        group.Children.Add(translate);
        root.RenderTransformOrigin = new Point(0, 0);
        root.RenderTransform = group;

        double rad = directionAngleDeg * Math.PI / 180.0;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        int durMs = transition switch
        {
            "SpringBloom" => isSubArc ? 195 : 220,
            "IrisUnfold" => isSubArc ? 190 : 215,
            "RadialPop" => isSubArc ? 185 : 210,
            "SpiralSpin" => isSubArc ? 205 : 230,
            "FadeSlide" => 165,
            _ => 145
        };

        if (transition == "SpringBloom")
        {
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.42 };
            double s0 = isSubArc ? 0.88 : 0.84;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(s0, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(s0, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
        else if (transition == "IrisUnfold")
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.90, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.90, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-10.5, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.05, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        }
        else if (transition == "RadialPop")
        {
            var back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.34 };
            double push = isSubArc ? 20 : 12;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-cos * push, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = back });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-sin * push, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = back });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.80, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = back });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.80, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = back });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.05, 1, TimeSpan.FromMilliseconds(135)));
        }
        else if (transition == "SpiralSpin")
        {
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.76, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.76, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-24.0, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.05, 1, TimeSpan.FromMilliseconds(150)));
        }
        else if (transition == "FadeSlide")
        {
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            double slide = isSubArc ? 14 : 9;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-cos * slide, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-sin * slide, 0, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
        }
        else
        {
            // SmoothGlide
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durMs)) { EasingFunction = ease });
        }

        if (cur.MotionStaggerRipple && wedgeVisuals != null && wedgeVisuals.Count > 1)
        {
            var staggerEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            int n = wedgeVisuals.Count;
            double perStepMs = Math.Min(16.0, 130.0 / Math.Max(1, n - 1));
            for (int i = 0; i < n; i++)
            {
                var el = wedgeVisuals[i];
                el.Opacity = 0.08;
                var fade = new DoubleAnimation(0.08, 1.0, TimeSpan.FromMilliseconds(120))
                {
                    BeginTime = TimeSpan.FromMilliseconds(Math.Round(i * perStepMs)),
                    EasingFunction = staggerEase
                };
                el.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }
    }

    internal static void ApplyHoverLift(
        IEnumerable<UIElement>? linkedVisuals,
        bool lifted,
        double midAngleDeg,
        DeskSpaceAppearanceV148? overrideApp = null)
    {
        if (linkedVisuals == null) return;
        var cur = overrideApp ?? DeskSpaceAppearanceV148.Current;
        bool animate = DeskSpaceReducedMotionV209.Enabled() &&
                       !string.Equals(cur.OpenTransition, "Instant", StringComparison.OrdinalIgnoreCase);

        double dist = 0;
        if (lifted && cur.MotionHoverLift)
        {
            dist = cur.OpenTransition switch
            {
                "SpringBloom" or "RadialPop" => 4.5,
                "SmoothGlide" => 2.5,
                _ => 3.5
            };
        }

        double rad = midAngleDeg * Math.PI / 180.0;
        double tx = dist * Math.Cos(rad);
        double ty = dist * Math.Sin(rad);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        foreach (var el in linkedVisuals)
        {
            if (el == null) continue;
            if (el.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform(0, 0);
                el.RenderTransform = tt;
            }

            if (!animate)
            {
                tt.BeginAnimation(TranslateTransform.XProperty, null);
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.X = tx;
                tt.Y = ty;
            }
            else
            {
                tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(tx, TimeSpan.FromMilliseconds(95)) { EasingFunction = ease });
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(ty, TimeSpan.FromMilliseconds(95)) { EasingFunction = ease });
            }
        }
    }

    internal sealed class SubDonutCalloutPillV380 : IDisposable
    {
        private readonly Window owner;
        private readonly Popup popup;
        private readonly Border pillBorder;
        private readonly Border badgeBorder;
        private readonly TextBlock badgeText;
        private readonly TextBlock nameText;
        private readonly TranslateTransform slideTransform = new();
        private bool disposed;
        private string activeKey = "";

        internal bool IsVisible => !disposed && popup.IsOpen;
        internal string ActiveFullText => nameText.Text ?? "";

        internal SubDonutCalloutPillV380(Window owner)
        {
            this.owner = owner;
            badgeText = new TextBlock
            {
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = badgeText
            };
            nameText = new TextBlock
            {
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                MaxWidth = 340,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            row.Children.Add(badgeBorder);
            row.Children.Add(nameText);

            pillBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 5, 10, 5),
                BorderThickness = new Thickness(1.5),
                IsHitTestVisible = false,
                RenderTransform = slideTransform,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(4, 10, 24),
                    BlurRadius = 12,
                    ShadowDepth = 3,
                    Opacity = 0.34
                },
                Child = row
            };

            popup = new Popup
            {
                PlacementTarget = owner,
                Placement = PlacementMode.Relative,
                AllowsTransparency = true,
                StaysOpen = true,
                Child = pillBorder
            };
            DeskSpaceMonitorFenceV153.TrackPopup(owner, popup);
        }

        internal static bool IsLabelTruncated(
            string? fullText,
            double boxWidth,
            double maxHeight,
            double fontSize,
            FontWeight fontWeight,
            bool wraps)
        {
            if (string.IsNullOrWhiteSpace(fullText) || boxWidth <= 4)
                return false;

            var single = new TextBlock
            {
                Text = fullText,
                FontSize = fontSize,
                FontWeight = fontWeight,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.None
            };
            single.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (!wraps)
                return single.DesiredSize.Width > boxWidth + 0.5;

            if (single.DesiredSize.Width <= boxWidth + 0.5)
                return false;

            var wrapped = new TextBlock
            {
                Text = fullText,
                Width = boxWidth,
                FontSize = fontSize,
                FontWeight = fontWeight,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.None
            };
            wrapped.Measure(new Size(boxWidth, double.PositiveInfinity));
            if (wrapped.DesiredSize.Height > maxHeight + 0.5)
                return true;

            // Check individual longest unbroken token against boxWidth (e.g. a single word trimmed with ...)
            foreach (string token in fullText.Split(new[] { ' ', '\u200B', '-', '_', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = new TextBlock
                {
                    Text = token,
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    TextWrapping = TextWrapping.NoWrap
                };
                t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (t.DesiredSize.Width > boxWidth + 0.5)
                    return true;
            }
            return false;
        }

        internal void Show(
            Canvas sourceCanvas,
            Point outerAnchorInCanvas,
            double midAngleDeg,
            string fullName,
            string? badge = null)
        {
            if (disposed || string.IsNullOrWhiteSpace(fullName))
                return;

            string key = $"{fullName}|{badge}|{midAngleDeg:F1}";
            if (popup.IsOpen && activeKey == key)
                return;
            activeKey = key;

            var (bg, border, text, badgeBg, badgeFg) = ResolveCalloutColors(DeskSpaceAppearanceV148.Current, SystemParameters.HighContrast);
            pillBorder.Background = bg;
            pillBorder.BorderBrush = border;
            nameText.Foreground = text;
            nameText.Text = fullName;

            if (!string.IsNullOrWhiteSpace(badge))
            {
                badgeText.Text = badge;
                badgeText.Foreground = badgeFg;
                badgeBorder.Background = badgeBg;
                badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                badgeBorder.Visibility = Visibility.Collapsed;
            }

            pillBorder.Measure(new Size(420, 140));
            double pillW = Math.Max(48, pillBorder.DesiredSize.Width);
            double pillH = Math.Max(26, pillBorder.DesiredSize.Height);

            double rad = midAngleDeg * Math.PI / 180.0;
            double nx = Math.Cos(rad), ny = Math.Sin(rad);

            double offsetX = outerAnchorInCanvas.X;
            double offsetY = outerAnchorInCanvas.Y;

            try
            {
                if (sourceCanvas.IsLoaded && owner.IsLoaded &&
                    PresentationSource.FromVisual(sourceCanvas) != null &&
                    PresentationSource.FromVisual(owner) != null &&
                    new WindowInteropHelper(owner).Handle != IntPtr.Zero)
                {
                    Point screenAnchor = sourceCanvas.PointToScreen(outerAnchorInCanvas);
                    var dpi = VisualTreeHelper.GetDpi(owner);
                    double dx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    double dy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                    double pillWpx = pillW * dx;
                    double pillHpx = pillH * dy;
                    double pushPx = 10 * dx;

                    double targetCenterXpx = screenAnchor.X + nx * (pushPx + pillWpx * 0.35);
                    double targetCenterYpx = screenAnchor.Y + ny * (pushPx + pillHpx * 0.5);

                    Rect work = DeskSpaceSubArcGeometryV48.MonitorWorkArea(owner);
                    double padPx = 8 * dx;
                    double screenLeftPx = Math.Clamp(targetCenterXpx - pillWpx * 0.5, work.Left + padPx, Math.Max(work.Left + padPx, work.Right - pillWpx - padPx));
                    double screenTopPx = Math.Clamp(targetCenterYpx - pillHpx * 0.5, work.Top + padPx, Math.Max(work.Top + padPx, work.Bottom - pillHpx - padPx));

                    Point rel = owner.PointFromScreen(new Point(screenLeftPx, screenTopPx));
                    offsetX = rel.X;
                    offsetY = rel.Y;
                }
            }
            catch (InvalidOperationException)
            {
                offsetX = outerAnchorInCanvas.X;
                offsetY = outerAnchorInCanvas.Y;
            }

            popup.HorizontalOffset = offsetX;
            popup.VerticalOffset = offsetY;

            bool animate = DeskSpaceReducedMotionV209.Enabled() &&
                           !string.Equals(DeskSpaceAppearanceV148.Current.OpenTransition, "Instant", StringComparison.OrdinalIgnoreCase);

            if (!animate)
            {
                pillBorder.BeginAnimation(UIElement.OpacityProperty, null);
                slideTransform.BeginAnimation(TranslateTransform.XProperty, null);
                slideTransform.BeginAnimation(TranslateTransform.YProperty, null);
                pillBorder.Opacity = 1;
                slideTransform.X = 0;
                slideTransform.Y = 0;
                popup.IsOpen = true;
                return;
            }

            pillBorder.Opacity = 0;
            popup.IsOpen = true;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            slideTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-nx * 7, 0, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease });
            slideTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-ny * 7, 0, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease });
            pillBorder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(105)) { EasingFunction = ease });
        }

        internal void Hide()
        {
            activeKey = "";
            if (!disposed && popup.IsOpen)
                popup.IsOpen = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeKey = "";
            popup.IsOpen = false;
        }
    }
}
