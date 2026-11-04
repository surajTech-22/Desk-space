using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DeskSpace;

// One visual truth for compact and expanded weather clocks. The weather scene
// can change with real conditions, including location-based or cached readings;
// the clock must follow the rendered SCENE, not exact city-name equality.
internal static class DeskSpaceWeatherContrastV64
{
    internal static bool LightScene(WeatherReading? weather,bool showWeather)
    {
        if(!showWeather||weather==null||!weather.IsDay)return false;
        int code=weather.Code;
        // Keep ink selection IDENTICAL to DeskSpaceWeatherSceneV11's sky
        // palette. Overcast code 3 uses the dark palette, unlike code 2.
        bool muted=(code>=51&&code<=67)||(code>=80&&code<=82)||
            code>=95||(code>=71&&code<=77)||(code>=85&&code<=86)||
            code==3||code==45||code==48;
        return !muted;
    }

    internal static Color ClockColor(WeatherReading? weather,bool showWeather)=>
        LightScene(weather,showWeather)
            ?Color.FromRgb(12,29,53):Colors.White;

    internal static Color EdgeColor(WeatherReading? weather,bool showWeather)=>
        LightScene(weather,showWeather)
            ?Colors.White:Color.FromRgb(2,8,28);

    // R366: widget clock updates once per second. Reuse immutable WPF
    // resources instead of allocating brushes and blur effects each tick.
    // The same light/dark palette and child styling remain unchanged.
    private static readonly Brush LightInk=FrozenInk(Color.FromRgb(12,29,53));
    private static readonly Brush DarkInk=FrozenInk(Colors.White);
    private static readonly Effect LightOutline=FrozenOutline(Colors.White);
    private static readonly Effect DarkOutline=FrozenOutline(Color.FromRgb(2,8,28));

    private static Brush FrozenInk(Color color)
    {
        var brush=new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
    private static Effect FrozenOutline(Color color)
    {
        var effect=new DropShadowEffect{Color=color,BlurRadius=3.5,
            ShadowDepth=0,Opacity=.95};
        effect.Freeze();
        return effect;
    }
    internal static void Apply(DependencyObject? element,
        WeatherReading? weather,bool showWeather)
    {
        if(element==null)return;
        bool light=LightScene(weather,showWeather);
        var foreground=light?LightInk:DarkInk;
        Effect? outline=showWeather&&weather!=null
            ?(light?LightOutline:DarkOutline):null;
        Visit(element,foreground,outline);
    }
    private static void Visit(DependencyObject parent,Brush ink,Effect? outline)
    {
        if(parent is TextBlock text)
        {
            if(!ReferenceEquals(text.Foreground,ink))text.Foreground=ink;
            if(!ReferenceEquals(text.Effect,outline))text.Effect=outline;
            foreach(Inline item in text.Inlines)
                if(!ReferenceEquals(item.Foreground,ink))item.Foreground=ink;
        }
        else if(parent is Control control)
        {
            if(!ReferenceEquals(control.Foreground,ink))control.Foreground=ink;
            if(!ReferenceEquals(control.Effect,outline))control.Effect=outline;
        }
        // A time/date widget may use independent TextBlocks inside an element;
        // unlike inherited Foreground this overrides their prior local colors.
        if(parent is Visual || parent is System.Windows.Media.Media3D.Visual3D)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                Visit(VisualTreeHelper.GetChild(parent,i),ink,outline);
        }
    }

    internal static double ContrastRatio(Color foreground,Color background)
    {
        static double Channel(byte byteValue)
        {
            double v=byteValue/255.0;
            return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);
        }
        static double L(Color c)=>
            .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);
        double a=L(foreground),b=L(background);
        return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
}
