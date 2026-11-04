using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskSpace;

internal static class Program
{
    private static void Check(bool ok,string why)
    {
        if(!ok)throw new InvalidOperationException("R366 clock styling regression: "+why);
    }
    [STAThread] private static int Main()
    {
        try
        {
            var type=typeof(WeatherReading).Assembly.GetType(
                "DeskSpace.DeskSpaceWeatherContrastV64",true)!;
            var apply=type.GetMethod("Apply",BindingFlags.NonPublic|
                BindingFlags.Static)!;
            var clock=new TextBlock {Text="05:18 PM"};
            var sunny=new WeatherReading("Test",27,0,true,DateTime.UtcNow);
            var cloudy=new WeatherReading("Test",27,3,true,DateTime.UtcNow);
            void Style(WeatherReading? weather,bool enabled)
                =>apply.Invoke(null,new object?[]{clock,weather,enabled});
            Style(sunny,true);
            var lightBrush=clock.Foreground;
            var lightEffect=clock.Effect as DropShadowEffect;
            Check(lightBrush is SolidColorBrush&&lightEffect!=null,
                "sunny clock palette or edge missing");
            Check(((SolidColorBrush)lightBrush).Color==Color.FromRgb(12,29,53),
                "daylight text color changed");
            Check(lightEffect.Color==Colors.White&&lightEffect.BlurRadius==3.5&&
                lightEffect.ShadowDepth==0&&lightEffect.Opacity==.95,
                "daylight edge changed");
            for(int i=0;i<5000;i++)Style(sunny,true);
            Check(ReferenceEquals(lightBrush,clock.Foreground)&&
                ReferenceEquals(lightEffect,clock.Effect),
                "unchanged weather allocates or reapplies visual resources");

            Style(cloudy,true);
            var darkBrush=clock.Foreground;
            var darkEffect=clock.Effect as DropShadowEffect;
            Check(darkBrush is SolidColorBrush&&darkEffect!=null,
                "cloudy clock style missing");
            Check(((SolidColorBrush)darkBrush).Color==Colors.White &&
                darkEffect.Color==Color.FromRgb(2,8,28),
                "cloudy palette changed");
            for(int i=0;i<5000;i++)Style(cloudy,true);
            Check(ReferenceEquals(darkBrush,clock.Foreground)&&
                ReferenceEquals(darkEffect,clock.Effect),
                "cloudy clock redraws identical effects each tick");

            Style(sunny,false);
            Check(clock.Effect==null&&
                ((SolidColorBrush)clock.Foreground).Color==Colors.White,
                "disabled weather changed the regular clock appearance");
            Style(sunny,true);
            Check(ReferenceEquals(clock.Foreground,lightBrush)&&
                ReferenceEquals(clock.Effect,lightEffect),
                "weather palette was not reused after mode switch");
            Console.WriteLine("R366 PASS: same colors/edges, zero duplicate"+
                " visual objects across 10,000 idle updates, mode switch retained.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
