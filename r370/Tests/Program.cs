using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskSpace;
using Button = System.Windows.Controls.Button;

internal static class Program
{
    static void Check(bool good,string message)
    {
        if(!good)throw new InvalidOperationException("R370: "+message);
    }
    [STAThread]
    static int Main()
    {
        try
        {
            var field=typeof(WidgetShellWindow).GetField("Segments",
                BindingFlags.NonPublic|BindingFlags.Static)!;
            var sectors=(string[])field.GetValue(null)!;
            Check(sectors.Length==12,"original 12 sectors changed");
            Check(sectors[3]=="Clock & Weather",
                "Clock and Weather were not merged into sector 3");
            Check(sectors[4]=="Stopwatch",
                "Stopwatch not placed into former Weather sector 4");
            Check(!sectors.Contains("Weather"),
                "duplicate Weather-only wedge still present");
            Check(sectors[0]=="Search"&&sectors[1]=="Calendar"&&
                  sectors[2]=="Calculator"&&sectors[5]=="Settings"&&
                  sectors[6]=="Information"&&sectors[10]=="Applications"&&
                  sectors[11]=="Excel Functions",
                "existing eleven other module positions disrupted");
            var root=Path.Combine("DeskSpace_Windows_R1","DeskSpace",
                "WidgetHost.cs");
            Check(File.Exists(root),"shipping WidgetHost source missing");
            var code=File.ReadAllText(root);
            foreach(var token in new[]{
                "Width=Height=diameter;",
                "Math.Min(OpenDiameter,",
                "Width=420,Height=420",
                "Width=380,Height=380",
                "Child=circularStage",
                "name is \"Clock\" or \"Weather\" or \"Clock & Weather\"",
                "if(name==\"Stopwatch\")",
                "OpenStopwatchFromCircleV371()",
                "prefs.CenterMode=stopwatchReturnCenterV371!",
                "activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromStopwatchV371)",
                "activeStopwatch?.Dispose();"
            })
                Check(code.Contains(token),"original-circle safety missing: "+token);
            Check(!code.Contains("if(returnToRing)") &&
                  !code.Contains("stopwatchReturnRingV371"),
                "Back must not expand the donut after restoring compact circle");
            Check(!code.Contains("Width=Math.Min(area.Width,530)"),
                "oversized stopwatch width still in shipping branch");
            Check(!code.Contains("Height=Math.Min(area.Height,574)"),
                "oversized stopwatch height still in shipping branch");

            // Render the true R369 stopwatch control hierarchy inside the
            // smaller R370 420x420 donut stage, then test all four buttons'
            // bounding corners lie INSIDE the original circular footprint.
            var t=typeof(WidgetShellWindow).Assembly.GetType(
                "DeskSpace.DeskSpaceStopwatchV369",true)!;
            var instance=t.GetConstructor(BindingFlags.NonPublic|
                BindingFlags.Instance,null,new[]{typeof(Action)},null)!
                .Invoke(new object[]{(Action)(()=>{})});
            var create=t.GetMethod("Create",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            using var cleanup=(IDisposable)instance;
            var content=(FrameworkElement)create.Invoke(instance,null)!;
            var originalDial=(Canvas)((Grid)content).Children[0];
            var buttons=originalDial.Children.OfType<Button>().ToArray();
            Check(buttons.Length==5,
                "real Start/Pause/Reset/Lap controls plus Back crown required");
            Check(buttons.Count(b=>((string)b.ToolTip!).StartsWith("Back "))==1,
                "real accessible metal crown Back control missing");
            var stage=new Grid{Width=420,Height=420};
            var inset=new Viewbox{
                Width=380,Height=380,
                Stretch=Stretch.Uniform,
                HorizontalAlignment=System.Windows.HorizontalAlignment.Center,
                VerticalAlignment=VerticalAlignment.Center,
                Child=content
            };
            stage.Children.Add(inset);
            stage.Measure(new System.Windows.Size(420,420));
            stage.Arrange(new Rect(0,0,420,420));
            stage.UpdateLayout();
            foreach(var button in buttons)
            {
                Check(button.ActualWidth>40 && button.ActualHeight>40,
                    "stopwatch control has no usable WPF hit target");
                var rectangle=button.TransformToAncestor(stage)
                    .TransformBounds(new Rect(0,0,
                        button.ActualWidth,button.ActualHeight));
                foreach(var corner in new[]{
                    rectangle.TopLeft,rectangle.TopRight,
                    rectangle.BottomLeft,rectangle.BottomRight
                })
                {
                    double dx=corner.X-210,dy=corner.Y-210;
                    Check(dx*dx+dy*dy<=210*210+3,
                        "button protrudes beyond the original donut circle");
                }
            }
            // R369's separate control tests validate monotonic time,
            // lap splits, pause/resume, reset and stopping idle redraw.
            Console.WriteLine("R371 PASS: the 4 timer buttons AND accessible Back crown stay within original circle.");
            Console.WriteLine("R370 PASS: Clock+Weather merged, Stopwatch in "+
                "freed wedge, 12 existing sectors, real four circular "+
                "buttons fit wholly INSIDE original 420-DIP circle, no "+
                "oversized independent panel; previous modules preserved.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
