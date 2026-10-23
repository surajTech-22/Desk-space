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
    internal static bool LightScene(WeatherReading? weather,bool showWeather)=>
        showWeather&&weather!=null&&weather.IsDay&&weather.Code<=3;

    internal static Color ClockColor(WeatherReading? weather,bool showWeather)=>
        LightScene(weather,showWeather)
            ?Color.FromRgb(12,29,53):Colors.White;

    internal static Color EdgeColor(WeatherReading? weather,bool showWeather)=>
        LightScene(weather,showWeather)
            ?Colors.White:Color.FromRgb(2,8,28);

    internal static void Apply(DependencyObject? element,
        WeatherReading? weather,bool showWeather)
    {
        if(element==null)return;
        var foreground=new SolidColorBrush(ClockColor(weather,showWeather));
        var stroke=EdgeColor(weather,showWeather);
        // A crisp, zero-distance edge around the glyph maintains readable
        // boundaries when animated clouds/rain cross the clock text.
        Effect? outline=showWeather&&weather!=null
            ?new DropShadowEffect{Color=stroke,BlurRadius=3.5,
                ShadowDepth=0,Opacity=.95}:null;
        Visit(element,foreground,outline);
    }
    private static void Visit(DependencyObject parent,Brush ink,Effect? outline)
    {
        if(parent is TextBlock text)
        {
            text.Foreground=ink;
            text.Effect=outline;
            foreach(Inline item in text.Inlines)
                item.Foreground=ink;
        }
        else if(parent is Control control)
        {
            control.Foreground=ink;
            control.Effect=outline;
        }
        else if(parent is UIElement visual)
            visual.Effect=outline;
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
