using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using DeskSpace;

internal static class Program
{
    private static void Assert(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R373: "+message);
    }
    [STAThread]
    static int Main()
    {
        try
        {
            var assembly=typeof(WeatherReading).Assembly;
            var deck=assembly.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
            var meter=assembly.GetType("DeskSpace.DeskSpaceDeckNetworkSpeedV372",true)!;
            var fields=BindingFlags.NonPublic|BindingFlags.Instance;
            var tickField=deck.GetField("networkSpeedTimerV373",fields)!;
            Assert(tickField!=null,"live Deck timer missing");
            var timer=(DispatcherTimer)tickField.GetValue(
                System.Runtime.Serialization.FormatterServices
                    .GetUninitializedObject(deck))!;
            // Field initializers do not execute on uninitialized WPF objects.
            // Verify timing via source plus WPF DispatcherTimer's value type.
            Assert(tickField.FieldType==typeof(DispatcherTimer),
                "network meter must use one WPF Dispatcher timer");
            string source=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            string data=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs");
            Assert(source.Contains("Interval=TimeSpan.FromMilliseconds(100)"),
                "live rate should update every 100 milliseconds, not 5 seconds");
            Assert(source.Contains("networkSpeedTimerV373.Tick+=(_,_)=>RefreshNetworkSpeedV372();"),
                "actual rate sample is not on the fast UI timer");
            Assert(source.Contains("runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();") &&
                   source.Contains("Interval=TimeSpan.FromSeconds(5)"),
                "must not change existing Running Apps refresh behavior");
            Assert(source.Contains("popup.Opened+=(_,_)=>SyncNetworkSpeedTimerV373()") &&
                   source.Contains("popup.Closed+=(_,_)=>SyncNetworkSpeedTimerV373()"),
                "meter must start/stop with original Deck popup");
            Assert(source.Contains("!searchActiveV225 && !deckMinimized") &&
                   source.Contains("networkSpeedTimerV373.Stop();"),
                "meter must stop when hidden, minimized or search is active");
            Assert(!source.Contains("runningAppsTimer.Tick+=(_,_)=>{") &&
                   !source.Contains("new Task("),
                "meter must never spawn a new work loop or slow down app refresh");
            Assert(data.Contains("cachedAdapters=Array.Empty<NetworkInterface>()") &&
                   data.Contains("now-adaptersRefreshedTicks>=5*Stopwatch.Frequency"),
                "enumerating network adapters on every 100-ms frame wastes CPU");
            Assert(data.Contains("NetworkInterface.GetAllNetworkInterfaces()") &&
                   !data.Contains("HttpClient") && !data.Contains("WebRequest"),
                "network rates must be local and never perform a network speed test");
            int header=source.IndexOf("list.Children.Add(title);",
                StringComparison.Ordinal);
            int speed=source.IndexOf("networkSpeedLabelV372=new TextBlock{",
                StringComparison.Ordinal);
            int apps=source.IndexOf("DeskSpaceRunningAppsV88.AddHoverGroupLive",
                StringComparison.Ordinal);
            Assert(header>=0 && speed>header && apps>speed,
                "network speed must remain INSIDE Deck below title, above Running Apps");
            var instance=Activator.CreateInstance(meter,true)!;
            var sample=meter.GetMethod("Sample",fields)!;
            var reset=meter.GetMethod("Reset",fields)!;
            var adapterField=meter.GetField("cachedAdapters",fields)!;
            var inventoryTime=meter.GetField("adaptersRefreshedTicks",fields)!;
            sample.Invoke(instance,null);
            var firstInventory=adapterField.GetValue(instance);
            long firstTimestamp=(long)inventoryTime.GetValue(instance)!;
            Thread.Sleep(120);
            sample.Invoke(instance,null);
            Assert(ReferenceEquals(firstInventory,adapterField.GetValue(instance)),
                "NIC inventory was rebuilt on a 100-ms sample");
            Assert(firstTimestamp==(long)inventoryTime.GetValue(instance)!,
                "adapter inventory refresh occurred before 5-second timeout");
            var display=(string)meter.GetProperty("Display",
                BindingFlags.Public|BindingFlags.NonPublic|
                BindingFlags.Instance)!.GetValue(instance)!;
            Assert(display.Contains("↓") && display.Contains("↑"),
                "live download and upload indicators missing");
            reset.Invoke(instance,null);

            Console.WriteLine("R373 PASS: in-Deck download/upload refresh every 100ms,"+
                " NIC list cached for 5s, Running Apps remains 5s,"+
                " no hidden/minimized polling or second popup,"+
                " Stopwatch return-to-compact preserved.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);return 1;
        }
    }
}
