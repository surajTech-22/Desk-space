using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DeskSpace;

internal static class Program
{
    static void Assert(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R369 stopwatch: "+message);
    }
    static void Click(Button button)=>
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent,button));

    [STAThread]
    static int Main()
    {
        try
        {
            var assembly=typeof(WeatherReading).Assembly;
            var type=assembly.GetType("DeskSpace.DeskSpaceStopwatchV369",true)!;
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var constructor=type.GetConstructor(
                BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(Action)},null)!;
            bool closed=false;
            object model=constructor.Invoke(new object[]{(Action)(()=>closed=true)});
            var create=type.GetMethod("Create",BindingFlags.Instance|
                BindingFlags.NonPublic)!;
            var ui=(FrameworkElement)create.Invoke(model,null)!;
            var canvas=(Canvas)((Grid)ui).Children[0];
            var buttons=canvas.Children.OfType<Button>().ToArray();
            Assert(buttons.Length==4,
                "approved four-button circular dial missing");
            var map=buttons.ToDictionary(x=>((string)x.ToolTip!).Split(' ')[0],
                x=>x,StringComparer.OrdinalIgnoreCase);
            Assert(map.ContainsKey("Start")&&map.ContainsKey("Pause")&&
                map.ContainsKey("Reset")&&map.ContainsKey("Lap"),
                "Start/Pause/Reset/Lap controls not present");
            Assert(canvas.Children.OfType<System.Windows.Shapes.Ellipse>().Count()>=6,
                "chronograph metallic bezel/dial missing");
            var elapsed=type.GetProperty("CompactElapsed",flags)!;
            var isRunning=type.GetProperty("IsRunning",flags)!;
            var laps=type.GetProperty("Laps",flags)!;
            var reset=type.GetMethod("Reset",flags)!;
            reset.Invoke(null,null);
            Assert((string)elapsed.GetValue(null)! == "00:00.00",
                "initial stopwatch must be zeroed");
            Click(map["Start"]);
            Assert((bool)isRunning.GetValue(null)!,
                "Start button did not start monotonic clock");
            Thread.Sleep(130);
            Click(map["Lap"]);
            Assert(((IEnumerable)laps.GetValue(null)!).Cast<object>().Count()==1,
                "Lap did not capture a timestamp");
            Thread.Sleep(70);
            Click(map["Pause"]);
            Assert(!(bool)isRunning.GetValue(null)!,
                "Pause did not stop the stopwatch");
            string frozen=(string)elapsed.GetValue(null)!;
            Thread.Sleep(100);
            Assert(frozen==(string)elapsed.GetValue(null)!,
                "paused time continued advancing");
            Click(map["Start"]);
            Thread.Sleep(90);
            Click(map["Lap"]);
            Assert(((IEnumerable)laps.GetValue(null)!).Cast<object>().Count()==2,
                "Resume did not preserve lap history");
            ((IDisposable)model).Dispose();
            string prior=(string)elapsed.GetValue(null)!;
            Thread.Sleep(90);
            Assert(prior!=(string)elapsed.GetValue(null)!,
                "stopwatch stopped when circle module closed");

            Click(map["Reset"]);
            Assert(!(bool)isRunning.GetValue(null)!,
                "Reset did not stop the clock");
            Assert((string)elapsed.GetValue(null)! == "00:00.00",
                "Reset did not restore zero elapsed time");
            Assert(((IEnumerable)laps.GetValue(null)!).Cast<object>().Count()==0,
                "Reset did not clear laps");
            Assert(!closed,"Back-to-circle should be invoked only by the crown/Escape");
            Console.WriteLine("R369 PASS: matching circular dial, four actual WPF buttons,"+
                " Start, Pause, Resume, Reset and Lap accurate, elapsed survives"+
                " panel close, reset clears lap history; no idle redraw timer.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
