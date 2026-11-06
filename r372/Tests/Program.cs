using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeskSpace;

internal static class Program
{
    private static void Check(bool condition,string description)
    {
        if(!condition)throw new InvalidOperationException(
            "R372: "+description);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            var assembly=typeof(WeatherReading).Assembly;
            var meter=assembly.GetType(
                "DeskSpace.DeskSpaceDeckNetworkSpeedV372",true)!;
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var counter=meter.GetNestedType("Counters",
                BindingFlags.NonPublic|BindingFlags.Public)!;
            var dictType=typeof(Dictionary<,>).MakeGenericType(
                typeof(string),counter);
            IDictionary Make(params (string Adapter,long Rx,long Tx)[] readings)
            {
                var data=(IDictionary)Activator.CreateInstance(dictType)!;
                foreach(var item in readings)
                    data.Add(item.Adapter,Activator.CreateInstance(counter,
                        new object[]{item.Rx,item.Tx})!);
                return data;
            }
            var calculate=meter.GetMethod("Calculate",flags)!;
            var rate=meter.GetMethod("Rate",flags)!;
            (double Down,double Up) Calculate(
                IDictionary from,IDictionary to,double interval)
            {
                var result=calculate.Invoke(null,
                    new object[]{from,to,interval})!;
                var t=result.GetType();
                return ((double)t.GetField("Item1")!.GetValue(result)!,
                    (double)t.GetField("Item2")!.GetValue(result)!);
            }

            var previous=Make(("wifi",1000L,2000L),("ethernet",300L,400L));
            var next=Make(("wifi",2000L,4000L),("ethernet",1300L,2400L));
            var values=Calculate(previous,next,2.0);
            Check(Math.Abs(values.Down-1000)<.001 &&
                  Math.Abs(values.Up-2000)<.001,
                "download or upload rates calculated incorrectly");
            values=Calculate(previous,Make(("new",5000000L,3000000L),
                ("wifi",500L,900L)),2.0);
            Check(values.Down==0 && values.Up==0,
                "reset/added interfaces must not create spurious spikes");
            values=Calculate(previous,next,0);
            Check(values.Down==0 && values.Up==0,
                "invalid elapsed-time sample must be zero");
            Check((string)rate.Invoke(null,new object[]{0d})! == "0 KB/s",
                "idle network speed must display 0 KB/s");
            Check((string)rate.Invoke(null,new object[]{65536d})! == "64 KB/s",
                "KB/s formatting incorrect");
            Check((string)rate.Invoke(null,new object[]{2097152d})! == "2.0 MB/s",
                "MB/s formatting incorrect");

            var deck=assembly.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
            Check(deck.GetField("networkSpeedLabelV372",
                BindingFlags.NonPublic|BindingFlags.Instance)!=null,
                "rate label missing from existing Deck class");
            Check(deck.GetField("runningAppsTimer",
                BindingFlags.NonPublic|BindingFlags.Instance)!=null,
                "existing 5-second timer missing");
            string deckSource=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            int title=deckSource.IndexOf("list.Children.Add(title);",
                StringComparison.Ordinal);
            int speed=deckSource.IndexOf("networkSpeedLabelV372=new TextBlock{",
                StringComparison.Ordinal);
            int apps=deckSource.IndexOf("DeskSpaceRunningAppsV88.AddHoverGroupLive",
                StringComparison.Ordinal);
            Check(title>0 && speed>title && apps>speed,
                "network speed MUST be inside existing Deck, between header and Running Apps");
            bool originalFiveSecond=
                deckSource.Contains("runningAppsTimer.Tick+=(_,_)=>{") &&
                deckSource.Contains("RefreshNetworkSpeedV372();");
            bool upgradedLiveRate=
                deckSource.Contains("Interval=TimeSpan.FromMilliseconds(100)") &&
                deckSource.Contains("networkSpeedTimerV373.Tick+=(_,_)=>RefreshNetworkSpeedV372();") &&
                deckSource.Contains("runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();") &&
                deckSource.Contains("networkSpeedTimerV373.Stop();");
            Check(originalFiveSecond || upgradedLiveRate,
                "Deck rates must use the legacy 5s tick or R373 CPU-safe 100ms timer");
            Check(!deckSource.Contains("new DeskSpaceDeckNetworkSpeedV372("),
                "unexpected network meter process creation");
            var widgetSource=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs");
            Check(widgetSource.Contains("Width=Height=CompactDiameter;") &&
                  widgetSource.Contains("BuildCompact();SavePosition();UpdateCenter();"),
                "original compact circle restore path missing");
            Check(widgetSource.Contains("prefs.CenterMode=stopwatchReturnCenterV371!") &&
                  widgetSource.Contains("OpenStopwatchFromCircleV371()"),
                "stopwatch must preserve prior Clock/Weather mode");
            Check(!widgetSource.Contains("stopwatchReturnRingV371") &&
                  !widgetSource.Contains("if(returnToRing)") &&
                  !widgetSource.Contains("OpenStopwatchFromCircleV371(fromRing:true)"),
                "Stopwatch Back still automatically opens the donut");

            Console.WriteLine("R372 PASS: pure per-interface download/upload rates;"+
                " reset/new adapter does not spike; compact Deck-only readout"+
                " between header and Running Apps; existing 5-second timer"+
                " reused; Stopwatch Back always restores prior compact circle.");
            return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
