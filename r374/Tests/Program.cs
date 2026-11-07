using System;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using DeskSpace;

internal static class Program
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("R374: "+message);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            var assembly=typeof(WeatherReading).Assembly;
            var deck=assembly.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
            var timer=deck.GetField("networkSpeedTimerV373",
                BindingFlags.Instance|BindingFlags.NonPublic);
            Check(timer?.FieldType==typeof(DispatcherTimer),
                "existing Deck local network timer missing or replaced");
            string source=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs");
            string meter=File.ReadAllText(
                "DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs");
            Check(source.Contains("Interval=TimeSpan.FromSeconds(1)};"),
                "network speed must refresh once per second");
            Check(!source.Contains("Interval=TimeSpan.FromMilliseconds(100)};"),
                "100ms network-speed refresh must be retired");
            Check(source.Contains("networkSpeedTimerV373.Tick+=(_,_)=>RefreshNetworkSpeedV372();"),
                "the one-second timer must update the live speed label");
            Check(source.Contains("Interval=TimeSpan.FromSeconds(5)") &&
                  source.Contains("runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();"),
                "existing Running Apps refresh must remain at five seconds");
            Check(source.Contains("networkSpeedTimerV373.Stop();") &&
                  source.Contains("!searchActiveV225 && !deckMinimized") &&
                  source.Contains("popup.Opened+=(_,_)=>SyncNetworkSpeedTimerV373();") &&
                  source.Contains("popup.Closed+=(_,_)=>SyncNetworkSpeedTimerV373();"),
                "Deck timer must stop when minimized, hidden, or closed");
            Check(source.Contains("networkSpeedLabelV372=new TextBlock{") &&
                  source.Contains("once per second while the Deck is open"),
                "in-Deck label or per-second tooltip missing");
            int title=source.IndexOf("list.Children.Add(title);",
                StringComparison.Ordinal);
            int speed=source.IndexOf("networkSpeedLabelV372=new TextBlock{",
                StringComparison.Ordinal);
            int apps=source.IndexOf("DeskSpaceRunningAppsV88.AddHoverGroupLive",
                StringComparison.Ordinal);
            Check(title>=0 && speed>title && apps>speed,
                "network speed must stay inside Deck, below header, above Running Apps");
            Check(meter.Contains("cachedAdapters=Array.Empty<NetworkInterface>()") &&
                  meter.Contains("now-adaptersRefreshedTicks>=5*Stopwatch.Frequency"),
                "cached network adapters and CPU safety must be preserved");
            Console.WriteLine(
                "R374 PASS: network speed updates every 1s; original Deck-only"+
                " placement unchanged; 5s Running Apps and NIC cache preserved;"+
                " hidden Deck timer stopped; stopwatch unrelated.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
