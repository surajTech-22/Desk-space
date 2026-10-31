using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace DeskSpace;

// R253: advisory health check only when user opens Component Status.
// Never start a timer, enumerate windows or kill/deprioritize a process.
internal static class DeskSpaceResourceBudgetV253
{
    internal const long PrivateMemoryBudget=1024L*1024*1024;
    internal const int HandleBudget=5000;
    internal const int ThreadBudget=256;

    internal static string Evaluate(long privateBytes,int handles,int threads)
    {
        if(privateBytes<0 || handles<0 || threads<0)
            return "Runtime budget unavailable (invalid process counters)";
        var alerts=new List<string>();
        if(privateBytes>PrivateMemoryBudget)
            alerts.Add("private memory above 1 GiB");
        if(handles>HandleBudget)
            alerts.Add("handles above 5000");
        if(threads>ThreadBudget)
            alerts.Add("threads above 256");
        string observed="private "+
            (privateBytes/(1024d*1024d)).ToString("F1",
                CultureInfo.InvariantCulture)+" MiB; "+
            handles.ToString(CultureInfo.InvariantCulture)+" handles; "+
            threads.ToString(CultureInfo.InvariantCulture)+" threads";
        return alerts.Count==0?
            "Within advisory budget ("+observed+")":
            "Advisory resource warning: "+string.Join("; ",alerts)+
            " ("+observed+"). Review component status; no processes stopped.";
    }

    internal static string Current()
    {
        try
        {
            using var process=Process.GetCurrentProcess();
            return Evaluate(process.PrivateMemorySize64,process.HandleCount,
                process.Threads.Count);
        }
        catch(Exception ex)when(ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return "Runtime budget unavailable ("+ex.GetType().Name+").";
        }
    }
}
