using System;
using System.Diagnostics;

namespace DeskSpace;

// R194: status is derived from counters already present in this process.
// No file contents, clipboard, saved data, window titles, or workbook paths
// are sampled. Zero additional timers and no outbound connections.
internal static class DeskSpaceHealthSignalsV194
{
    internal static string Search(bool indexing,string? coverage)
    {
        string summary=string.IsNullOrWhiteSpace(coverage)?
            "No indexed volumes reported":
            coverage.Length<=220?coverage:coverage[..220]+"…";
        return indexing?"Index warming · "+summary:
            "Progressive drive scan complete or idle · "+summary;
    }

    internal static string ProcessMemory(long bytes)
    {
        if(bytes<0)return "Working memory unavailable";
        double mb=bytes/(1024d*1024d);
        return "Working set "+mb.ToString("N0",
            System.Globalization.CultureInfo.InvariantCulture)+" MiB";
    }

    internal static string CurrentMemory()
    {
        try
        {
            using var process=Process.GetCurrentProcess();
            return ProcessMemory(process.WorkingSet64);
        }
        catch(Exception ex)when(ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return "Working memory unavailable";
        }
    }
}
