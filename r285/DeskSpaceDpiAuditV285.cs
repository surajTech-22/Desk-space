using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// P0 DPI: manifest is the source of truth. This read-only diagnostic reports
// the DPI *of the real DeskSpace process*, not of a separate synthetic WPF test.
// Generated only by a user click or explicit --dpi-audit CLI argument.
// No window title, paths, opened files, clipboard or personal content is read.
internal static class DeskSpaceDpiAuditV285
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point{public int X,Y;}

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool AreDpiAwarenessContextsEqual(
        IntPtr left,IntPtr right);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point p,uint flags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor,
        int type,out uint x,out uint y);
    private const uint Nearest=2;

    internal sealed record Monitor(string Device,bool Primary,
        int X,int Y,int Width,int Height,int WorkX,int WorkY,
        int WorkWidth,int WorkHeight,uint DpiX,uint DpiY);
    internal sealed record Report(string Schema,string CreatedUtc,
        bool PerMonitorV2,string Awareness,int MonitorCount,
        IReadOnlyList<Monitor> Monitors);

    internal static string AwarenessLabel(bool pmV2,bool pmV1,bool system)
        => pmV2?"PerMonitorV2":pmV1?"PerMonitor":
            system?"System":"Unaware/Unknown";

    internal static Report Capture()
    {
        var thread=GetThreadDpiAwarenessContext();
        bool v2=AreDpiAwarenessContextsEqual(thread,new IntPtr(-4));
        bool v1=AreDpiAwarenessContextsEqual(thread,new IntPtr(-3));
        bool system=AreDpiAwarenessContextsEqual(thread,new IntPtr(-2));
        var screens=Forms.Screen.AllScreens;
        var rows=new List<Monitor>(screens.Length);
        foreach(var screen in screens)
        {
            var bounds=screen.Bounds;
            var work=screen.WorkingArea;
            int cx=bounds.Left+bounds.Width/2;
            int cy=bounds.Top+bounds.Height/2;
            var handle=MonitorFromPoint(new Point{X=cx,Y=cy},Nearest);
            uint dpiX=0,dpiY=0;
            try
            {
                if(handle!=IntPtr.Zero &&
                    GetDpiForMonitor(handle,0,out uint xx,out uint yy)==0)
                {
                    dpiX=xx;dpiY=yy;
                }
            }
            catch(DllNotFoundException){}
            catch(EntryPointNotFoundException){}
            rows.Add(new Monitor(screen.DeviceName,screen.Primary,
                bounds.Left,bounds.Top,bounds.Width,bounds.Height,
                work.Left,work.Top,work.Width,work.Height,dpiX,dpiY));
        }
        return new Report("deskspace-dpi-audit-v285",
            DateTime.UtcNow.ToString("O"),v2,
            AwarenessLabel(v2,v1,system),rows.Count,rows);
    }

    internal static string DownloadsFile()=>
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),"Downloads",
            "DeskSpace_Monitor_DPI_"+
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".json");

    internal static string SaveToDownloads()
    {
        var result=Capture();
        var path=DownloadsFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{
                WriteIndented=true
            });
            File.WriteAllText(temp,json,new UTF8Encoding(false));
            File.Move(temp,path,false);
        }
        finally
        {
            try{if(File.Exists(temp))File.Delete(temp);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        return path;
    }
}
