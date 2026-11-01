using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DeskSpace;

// R283: one reusable visual language for the widget, Deck, Settings Studio
// and command surfaces. No data storage, network requests, theme watchers,
// UI automation, global style override or altered donut sector geometry.
internal static class DeskSpaceDesignV283
{
    internal sealed record Palette(
        bool Dark,bool HighContrast,Color Canvas,Color Surface,
        Color Raised,Color Navigation,Color Border,Color Text,
        Color Muted,Color Accent,Color AccentSoft,Color Hover);

    internal static Palette Resolve(bool dark,bool highContrast,Color accent)
    {
        if(highContrast)
            return new(dark,true,
                SystemColors.WindowColor,SystemColors.WindowColor,
                SystemColors.ControlColor,SystemColors.WindowColor,
                SystemColors.WindowTextColor,SystemColors.WindowTextColor,
                SystemColors.GrayTextColor,SystemColors.HighlightColor,
                SystemColors.HighlightColor,SystemColors.HighlightColor);
        if(dark)
            return new(true,false,
                Color.FromRgb(12,18,35),Color.FromRgb(20,29,49),
                Color.FromRgb(30,42,66),Color.FromRgb(15,23,41),
                Color.FromRgb(75,94,127),Color.FromRgb(244,247,255),
                Color.FromRgb(167,185,216),accent,
                DeskSpaceAppearanceV148.Mix(accent,Color.FromRgb(35,45,78),.72),
                Color.FromRgb(47,61,91));
        return new(false,false,
            Color.FromRgb(242,245,251),Colors.White,
            Color.FromRgb(248,250,255),Color.FromRgb(232,237,249),
            Color.FromRgb(210,218,235),Color.FromRgb(28,40,68),
            Color.FromRgb(91,108,142),accent,
            DeskSpaceAppearanceV148.Mix(accent,Colors.White,.84),
            Color.FromRgb(235,240,252));
    }

    internal static Palette Current=>Resolve(
        DeskSpaceAppearanceV148.IsDarkFor(DeskSpaceAppearanceV148.Current),
        SystemParameters.HighContrast,DeskSpaceAppearanceV148.AccentColor);

    internal static Brush Solid(Color value)
    {
        var b=new SolidColorBrush(value);
        b.Freeze();
        return b;
    }

    internal static Brush Gradient(Color first,Color last,double angle=115)
    {
        var b=new LinearGradientBrush(first,last,angle);
        b.Freeze();
        return b;
    }

    internal static Brush WidgetFace()
    {
        if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
        var p=Current;
        return Gradient(
            DeskSpaceAppearanceV148.Mix(p.Accent,
                Color.FromRgb(26,43,74),.72),
            Color.FromRgb(16,27,50),130);
    }

    internal static void ApplyDeck(Border panel)
    {
        var p=Current;
        panel.CornerRadius=new CornerRadius(18);
        panel.Background=p.HighContrast?Solid(p.Surface):
            Gradient(p.Raised,p.Navigation,125);
        panel.BorderBrush=Solid(p.Border);
        panel.BorderThickness=new Thickness(p.HighContrast?2:1);
        panel.SnapsToDevicePixels=true;
        panel.Effect=p.HighContrast?null:new DropShadowEffect
        {
            Color=Color.FromRgb(3,9,27),
            BlurRadius=17,ShadowDepth=5,Opacity=.38,
            Direction=270
        };
    }

    internal static void ApplyWindow(Window window,Palette p)
    {
        window.Background=Solid(p.Canvas);
        window.Foreground=Solid(p.Text);
        window.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        window.UseLayoutRounding=true;
        window.SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(window,
            TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(window,TextRenderingMode.ClearType);
    }

    // The Button template preserves the native Button, keyboard focus,
    // automation peer and Click commands. Rounded chrome has no click or
    // mouse handlers of its own.
    internal static void Button(Button button,bool primary=false,
        Palette? palette=null)
    {
        var p=palette??Current;
        button.Background=Solid(primary?p.Accent:p.Raised);
        button.Foreground=Solid(primary && !p.HighContrast?
            Color.FromRgb(8,16,33):p.Text);
        button.BorderBrush=Solid(primary?p.Accent:p.Border);
        button.BorderThickness=new Thickness(p.HighContrast?2:1);
        button.Padding=new Thickness(13,9,13,9);
        button.MinHeight=Math.Max(button.MinHeight,34);
        button.Cursor=System.Windows.Input.Cursors.Hand;
        var border=new FrameworkElementFactory(typeof(Border)){
            Name="Chrome"
        };
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(Control.Background))
            {RelativeSource=new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        border.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding(nameof(Control.BorderBrush))
            {RelativeSource=new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        border.SetBinding(Border.BorderThicknessProperty,
            new System.Windows.Data.Binding(nameof(Control.BorderThickness))
            {RelativeSource=new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        var content=new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Stretch);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);
        content.SetValue(ContentPresenter.MarginProperty,
            new Thickness(0));
        border.AppendChild(content);
        var template=new ControlTemplate(typeof(Button))
        {
            VisualTree=border
        };
        // Keyboard focus and hover must be visible even with custom chrome.
        // Trigger is on the existing native Button, never a mouse-only layer.
        var keyboard=new Trigger{
            Property=UIElement.IsKeyboardFocusedProperty,Value=true
        };
        keyboard.Setters.Add(new Setter(Border.BorderBrushProperty,
            Solid(p.Accent),"Chrome"));
        keyboard.Setters.Add(new Setter(Border.BorderThicknessProperty,
            new Thickness(2),"Chrome"));
        template.Triggers.Add(keyboard);
        var hovered=new Trigger{
            Property=UIElement.IsMouseOverProperty,Value=true
        };
        hovered.Setters.Add(new Setter(Border.BorderBrushProperty,
            Solid(p.Accent),"Chrome"));
        template.Triggers.Add(hovered);
        button.Template=template;
        if(string.IsNullOrEmpty(AutomationProperties.GetName(button)) &&
           button.Content is string label)
            AutomationProperties.SetName(button,label);
    }

    internal static void Input(TextBox text,Palette? palette=null)
    {
        var p=palette??Current;
        text.Background=Solid(p.Raised);
        text.Foreground=Solid(p.Text);
        text.BorderBrush=Solid(p.Border);
        text.BorderThickness=new Thickness(1);
        text.Padding=new Thickness(11,9,11,9);
        text.CaretBrush=Solid(p.Accent);
        text.SelectionBrush=Solid(p.AccentSoft);
        text.MinHeight=Math.Max(36,text.MinHeight);
    }

    internal static void Selection(ListBox list,Palette? palette=null)
    {
        var p=palette??Current;
        list.Background=Solid(p.Surface);
        list.Foreground=Solid(p.Text);
        list.BorderBrush=Solid(p.Border);
        list.BorderThickness=new Thickness(1);
    }

    internal static Border Card(UIElement child,Palette? palette=null)
    {
        var p=palette??Current;
        return new Border{
            Child=child,CornerRadius=new CornerRadius(15),
            Padding=new Thickness(16),
            Background=Solid(p.Raised),BorderBrush=Solid(p.Border),
            BorderThickness=new Thickness(1),
            SnapsToDevicePixels=true
        };
    }

    internal static TextBlock Eyebrow(string text,Palette? palette=null)
    {
        var p=palette??Current;
        return new TextBlock{
            Text=text,FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Solid(p.Accent),
            Margin=new Thickness(0,0,0,6)
        };
    }
}
