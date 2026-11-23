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
                Color.FromRgb(15,18,25),Color.FromRgb(21,26,36),
                Color.FromRgb(30,37,52),Color.FromRgb(19,23,32),
                Color.FromRgb(45,54,74),Color.FromRgb(241,245,249),
                Color.FromRgb(148,163,184),accent,
                DeskSpaceAppearanceV148.Mix(accent,Color.FromRgb(30,37,52),.76),
                Color.FromRgb(40,49,69));
        return new(false,false,
            Color.FromRgb(243,245,249),Color.FromRgb(246,248,252),
            Colors.White,Color.FromRgb(234,238,245),
            Color.FromRgb(220,227,238),Color.FromRgb(15,23,42),
            Color.FromRgb(71,85,105),accent,
            DeskSpaceAppearanceV148.Mix(accent,Colors.White,.88),
            Color.FromRgb(232,238,247));
    }

    internal static double RelativeLuminance(Color c)
    {
        static double Linearize(byte v)
        {
            double s=v/255.0;
            return s<=0.03928?s/12.92:Math.Pow((s+0.055)/1.055,2.4);
        }
        return 0.2126*Linearize(c.R)+0.7152*Linearize(c.G)+0.0722*Linearize(c.B);
    }

    internal static double ContrastRatio(Color a,Color b)
    {
        double la=RelativeLuminance(a);
        double lb=RelativeLuminance(b);
        return (Math.Max(la,lb)+0.05)/(Math.Min(la,lb)+0.05);
    }

    internal static Color OnAccent(Color accent,bool highContrast=false)
    {
        if(highContrast)return SystemColors.HighlightTextColor;
        var darkInk=Color.FromRgb(15,23,42);
        return ContrastRatio(Colors.White,accent)>=ContrastRatio(darkInk,accent)
            ?Colors.White:darkInk;
    }

    internal static Color AccentText(Palette p)
    {
        if(p.HighContrast)return p.Accent;
        var target=p.Dark?Colors.White:Color.FromRgb(15,23,42);
        var candidate=p.Accent;
        for(int i=0;i<=20;i++)
        {
            if(ContrastRatio(candidate,p.Raised)>=4.6 &&
               ContrastRatio(candidate,p.Surface)>=4.6)
                return candidate;
            double frac=Math.Min(1.0,(i+1)*0.05);
            candidate=DeskSpaceAppearanceV148.Mix(p.Accent,target,frac);
        }
        return target;
    }

    internal static Color AccentHover(Palette p)
    {
        if(p.HighContrast)return p.Hover;
        return DeskSpaceAppearanceV148.Mix(
            p.Accent,p.Dark?Colors.White:Color.FromRgb(15,23,42),.14);
    }

    internal static Color SecondaryButtonBg(Palette p)
    {
        if(p.HighContrast)return p.Raised;
        return p.Dark?Color.FromRgb(38,48,68):Color.FromRgb(238,242,247);
    }

    internal static Color InputBg(Palette p)
    {
        if(p.HighContrast)return p.Surface;
        return p.Dark?Color.FromRgb(19,24,35):Color.FromRgb(248,250,252);
    }

    internal static Color Success(Palette p)=>
        p.HighContrast?p.Text:(p.Dark?Color.FromRgb(52,211,153):Color.FromRgb(21,128,61));

    internal static Color Danger(Palette p)=>
        p.HighContrast?p.Text:(p.Dark?Color.FromRgb(248,113,113):Color.FromRgb(220,38,38));

    internal static Color Warning(Palette p)=>
        p.HighContrast?p.Text:(p.Dark?Color.FromRgb(251,191,36):Color.FromRgb(180,83,9));

    internal static Color Info(Palette p)=>
        p.HighContrast?p.Text:(p.Dark?Color.FromRgb(96,165,250):Color.FromRgb(29,78,216));

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
        button.Background=Solid(primary?p.Accent:SecondaryButtonBg(p));
        button.Foreground=Solid(primary?OnAccent(p.Accent,p.HighContrast):p.Text);
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
        hovered.Setters.Add(new Setter(Border.BackgroundProperty,
            Solid(primary?AccentHover(p):p.Hover),"Chrome"));
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
        text.Background=Solid(InputBg(p));
        text.Foreground=Solid(p.Text);
        text.BorderBrush=Solid(p.Border);
        text.BorderThickness=new Thickness(1);
        text.Padding=new Thickness(11,9,11,9);
        text.CaretBrush=Solid(AccentText(p));
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
            Child=child,CornerRadius=new CornerRadius(14),
            Padding=new Thickness(16),
            Background=Solid(p.Raised),BorderBrush=Solid(p.Border),
            BorderThickness=new Thickness(1),
            SnapsToDevicePixels=true,
            Effect=p.HighContrast?null:new DropShadowEffect
            {
                Color=p.Dark?Color.FromRgb(4,6,12):Color.FromRgb(15,23,42),
                BlurRadius=12,ShadowDepth=2,
                Opacity=p.Dark?.22:.06,Direction=270
            }
        };
    }

    internal static TextBlock Eyebrow(string text,Palette? palette=null)
    {
        var p=palette??Current;
        return new TextBlock{
            Text=text,FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Solid(AccentText(p)),
            Margin=new Thickness(0,0,0,6)
        };
    }
}
