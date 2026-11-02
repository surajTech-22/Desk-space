using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;

internal static class Program
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Static;
    static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException("R346 Search persistent health: "+msg);}
    [STAThread]static int Main()
    {
        string temp=Path.Combine(Path.GetTempPath(),"DeskSpaceR346-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var assembly=typeof(DeskSpace.WeatherReading).Assembly;
            var cls=assembly.GetType("DeskSpace.DeskSpaceSearchDiskHealthV346",true)!;
            var inspect=cls.GetMethod("Inspect",F)!;
            var describe=cls.GetMethod("Describe",F)!;
            var oversized=cls.GetMethod("Oversized",F)!;
            var baseline=inspect.Invoke(null,new object?[]{temp})!;
            Check(!(bool)baseline.GetType().GetProperty("CurrentAvailable")!.GetValue(baseline)!,
                "empty Search directory reported a persistent index");
            File.WriteAllBytes(Path.Combine(temp,"filenames-v336.sqlite"),new byte[2500]);
            File.WriteAllBytes(Path.Combine(temp,"filenames-v330.sqlite"),new byte[10000]);
            var report=inspect.Invoke(null,new object?[]{temp})!;
            Check((bool)report.GetType().GetProperty("CurrentAvailable")!.GetValue(report)!,
                "compact persistent cache not recognized");
            Check((long)report.GetType().GetProperty("CurrentBytes")!.GetValue(report)! ==2500 &&
                (long)report.GetType().GetProperty("LegacyBytes")!.GetValue(report)! ==10000,
                "persistent index or old-cache size report incorrect");
            var text=(string)describe.Invoke(null,new object[]{report})!;
            Check(text.Contains("Compact filename cache")&&
                text.Contains("Previous full-path cache")&&
                text.Contains("not modified"),
                "index health must disclose old cache without deletion");
            Check(!(bool)oversized.Invoke(null,new object[]{536870911L})! &&
                (bool)oversized.Invoke(null,new object[]{536870912L})!,
                "512MB alert threshold incorrect");
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var window=(Window)cls.GetMethod("Create",F)!
                .Invoke(null,new object?[]{null,temp})!;
            Check(window.Title.Contains("disk index health"),
                "read-only cache health UI not available");
            window.Close();app.Shutdown();
            Check(File.Exists(Path.Combine(temp,"filenames-v330.sqlite")) &&
                File.Exists(Path.Combine(temp,"filenames-v336.sqlite")),
                "health view deleted existing index files");
            Console.WriteLine("R346 PASS: persistent cache metadata, legacy oversized warning, "+
                "no document reads, no cache deletion and WPF health display.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{try{Directory.Delete(temp,true);}catch(Exception){}}
    }
}
