using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace DeskSpace;

// R372: process-local, read-only network throughput for the EXISTING Deck.
// No extra window/Popup, sockets, traffic collection, network requests or
// additional recurring timer. The caller samples on its existing five-second
// Running Apps timer, only while the Deck is actually expanded and visible.
internal sealed class DeskSpaceDeckNetworkSpeedV372
{
    internal readonly record struct Counters(long Received,long Sent);
    private Dictionary<string,Counters>? previous;
    private long previousTicks;
    internal string Display {get;private set;}="↓ —    ↑ —";

    internal static (double Down,double Up) Calculate(
        IReadOnlyDictionary<string,Counters> before,
        IReadOnlyDictionary<string,Counters> after,double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<=0)
            return (0,0);
        long received=0,sent=0;
        foreach(var item in after)
        {
            if(!before.TryGetValue(item.Key,out var old))continue;
            // Counter reset / adapter restart must not cause a false spike.
            if(item.Value.Received>=old.Received &&
               item.Value.Sent>=old.Sent)
            {
                received+=item.Value.Received-old.Received;
                sent+=item.Value.Sent-old.Sent;
            }
        }
        return (received/seconds,sent/seconds);
    }

    internal static string Rate(double bytesPerSecond)
    {
        if(!double.IsFinite(bytesPerSecond)||bytesPerSecond<=0)return "0 KB/s";
        if(bytesPerSecond>=1048576)
            return (bytesPerSecond/1048576).ToString("0.0",
                CultureInfo.InvariantCulture)+" MB/s";
        return (bytesPerSecond/1024).ToString("0.#",
            CultureInfo.InvariantCulture)+" KB/s";
    }
    internal void Reset()
    {
        previous=null;
        previousTicks=0;
        Display="↓ —    ↑ —";
    }

    internal void Sample()
    {
        try
        {
            var current=new Dictionary<string,Counters>(
                StringComparer.OrdinalIgnoreCase);
            foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if(adapter.OperationalStatus!=OperationalStatus.Up ||
                   adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback ||
                   adapter.NetworkInterfaceType==NetworkInterfaceType.Tunnel)
                    continue;
                try
                {
                    var stats=adapter.GetIPv4Statistics();
                    current[adapter.Id]=new Counters(
                        stats.BytesReceived,stats.BytesSent);
                }
                catch(NetworkInformationException){} // adapter disappeared
            }
            long now=Stopwatch.GetTimestamp();
            if(previous!=null && previousTicks!=0)
            {
                double seconds=(now-previousTicks)/(double)Stopwatch.Frequency;
                var rates=Calculate(previous,current,seconds);
                Display="↓ "+Rate(rates.Down)+"   ↑ "+Rate(rates.Up);
            }
            else Display="↓ —    ↑ —";
            previous=current;
            previousTicks=now;
        }
        catch(NetworkInformationException){Reset();}
        catch(InvalidOperationException){Reset();}
        catch(PlatformNotSupportedException){Reset();}
    }
}
