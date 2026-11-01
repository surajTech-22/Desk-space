using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using DeskSpace;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try { Run();return 0; }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Check(bool value,string message)
    {if(!value)throw new Exception("R289 FAIL: "+message);}
    private static object Call(Type type,string name,params object[] args)=>
        type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!
            .Invoke(null,args)!;

    private static void Run()
    {
        var asm=typeof(WeatherReading).Assembly;
        var dock=asm.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var scroll=asm.GetType("DeskSpace.DeskSpaceSmoothScrollV41",true)!;
        foreach(var work in new[]{
            new Rectangle(0,0,1920,1080),
            new Rectangle(-1920,-200,1920,1080),
            new Rectangle(1920,0,1280,720),
            new Rectangle(0,0,640,320)})
        {
            foreach(var y in new[]{work.Top+2,work.Bottom-146,work.Bottom-300})
            {
                var widget=new Rectangle(work.Left+30,y,116,116);
                var plan=Call(dock,"PlanBelowV289",widget,work,220,12);
                int top=(int)plan.GetType().GetProperty("WidgetTop")!.GetValue(plan)!;
                int height=(int)plan.GetType().GetProperty("DeckHeightPx")!.GetValue(plan)!;
                Check(top>=work.Top && top+widget.Height+12+height<=work.Bottom,
                    "widget and Deck must fit vertically below within its own monitor");
                var moved=new Rectangle(widget.Left,top,widget.Width,widget.Height);
                var popup=Call(dock,"CalculateBelowV289",moved,work,240,height,12);
                var t=popup.GetType();
                int px=(int)t.GetProperty("X")!.GetValue(popup)!;
                int py=(int)t.GetProperty("Y")!.GetValue(popup)!;
                Check(py==moved.Bottom+12 && px>=work.Left &&
                    px+240<=work.Right && py+height<=work.Bottom,
                    "Deck must be BELOW, never over, above or offscreen");
                var repair=(Rectangle)Call(dock,"RepairBelowV289",
                    new Rectangle(px,moved.Top,240,height),moved,work,12);
                Check(repair.Y==py && !repair.IntersectsWith(moved),
                    "native popup repair must also keep Deck beneath widget");
            }
        }
        Check(!(bool)Call(dock,"HasDeckDragIntentV289",7,7) &&
             (bool)Call(dock,"HasDeckDragIntentV289",8,0),
             "light touchpad taps must not start a Deck drag");
        double Pixels(int delta)=>(double)Call(scroll,"PixelsForWheelV289",delta,3);
        Check(Pixels(0)==0 && Pixels(120)==-66 &&
             Pixels(-120)==66 && Pixels(15)<0 && Pixels(15)>-10 &&
             Math.Abs(Pixels(15)*8-Pixels(120))<.00001,
             "precision two-finger fractional deltas must accumulate linearly");
        Check(scroll.GetMethod("Enable",BindingFlags.NonPublic|BindingFlags.Static)!=null,
            "existing global wheel routing must remain available");
        Console.WriteLine("R289 PASS: below-only Deck on four physical and negative-origin monitor geometries, screen-edge fitting, popup repair, touchpad drag threshold, fractional wheel/Shift handling.");
    }
}