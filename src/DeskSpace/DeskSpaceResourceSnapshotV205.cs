using System;
using System.Diagnostics;
using System.Globalization;

namespace DeskSpace;

// R205: on-demand, privacy-safe cumulative process counters.
// This is NOT an instantaneous CPU utilization measurement or leak detector.
// No timer, polling loop, ETW session, file access or GPU device enumeration.
internal static class DeskSpaceResourceSnapshotV205
{
    internal static string Format(TimeSpan cpu,TimeSpan elapsed,
        int logicalCores,int handles,long managedBytes)
    {
        if(elapsed<=TimeSpan.Zero || logicalCores<=0 ||
           cpu<TimeSpan.Zero || handles<0 || managedBytes<0)
            return "Resource counters unavailable";
        double percent=100d*cpu.TotalSeconds/
            (elapsed.TotalSeconds*logicalCores);
        if(!double.IsFinite(percent))return "Resource counters unavailable";
        percent=Math.Clamp(percent,0,100);
        double mb=managedBytes/(1024d*1024d);
        return "Lifetime CPU mean "+
            percent.ToString("F1",CultureInfo.InvariantCulture)+
            "% across "+logicalCores+" logical cores; "+
            handles.ToString(CultureInfo.InvariantCulture)+
            " handles; managed heap "+
            mb.ToString("F1",CultureInfo.InvariantCulture)+" MiB";
    }
    internal static string Current()
    {
        try
        {
            using var process=Process.GetCurrentProcess();
            var age=DateTime.UtcNow-process.StartTime.ToUniversalTime();
            return Format(process.TotalProcessorTime,age,
                Environment.ProcessorCount,process.HandleCount,
                GC.GetTotalMemory(false));
        }
        catch(Exception ex)when(ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return "Resource counters unavailable";
        }
    }
}
