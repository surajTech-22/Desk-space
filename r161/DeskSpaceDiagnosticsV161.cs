using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Safe diagnostic export: only version, monitor geometry and error categories.
// Never uploads anything; excludes user content, credentials, Excel workbooks,
// cached file names, staged clipboard files and the information vault.
internal static class DeskSpaceDiagnosticsV161
{
    private static bool subscribed;
    internal static string Root=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","Diagnostics");
    private static readonly object Gate=new();

    internal static void Install()
    {
        lock(Gate)
        {
            if(subscribed)return;
            subscribed=true;
            AppDomain.CurrentDomain.UnhandledException+=(_,e)=>
                RecordCrash(e.ExceptionObject as Exception);
            if(Application.Current!=null)
                Application.Current.DispatcherUnhandledException+=(_,e)=>
                    RecordCrash(e.Exception);
        }
    }
    internal static void RecordCrash(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Root);
            // Error type only: exception messages may contain real customer
            // information or full file paths and must never be copied to logs.
            var safe=ex==null?"Unknown":ex.GetType().FullName??"Unknown";
            var file=Path.Combine(Root,"crash-types.log");
            lock(Gate)
            {
                if(File.Exists(file)&&new FileInfo(file).Length>100_000)
                {
                    var tail=File.ReadLines(file).TakeLast(150).ToArray();
                    File.WriteAllLines(file,tail);
                }
                File.AppendAllText(file,DateTime.UtcNow.ToString("O")+
                    " "+safe+Environment.NewLine,Encoding.UTF8);
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {Debug.WriteLine("DeskSpace crash event logging unavailable");}
    }

    internal static string BuildReport()
    {
        var sb=new StringBuilder();
        sb.AppendLine("DeskSpace support summary (no personal content)");
        sb.AppendLine("UTC: "+DateTime.UtcNow.ToString("O"));
        sb.AppendLine("App version: "+typeof(DeskSpaceDiagnosticsV161).Assembly
            .GetName().Version);
        sb.AppendLine("OS architecture: "+System.Runtime.InteropServices
            .RuntimeInformation.OSArchitecture);
        sb.AppendLine("Process architecture: "+System.Runtime.InteropServices
            .RuntimeInformation.ProcessArchitecture);
        sb.AppendLine("OS description: "+System.Runtime.InteropServices
            .RuntimeInformation.OSDescription);
        sb.AppendLine("Screens: "+Forms.Screen.AllScreens.Length);
        int index=0;
        foreach(var screen in Forms.Screen.AllScreens)
        {
            var rect=screen.WorkingArea;
            sb.AppendLine("Monitor "+(++index)+": size="+rect.Width+"x"+
                rect.Height+" origin="+rect.X+","+rect.Y+
                " primary="+screen.Primary);
        }
        // No file-system paths, user names, connected account names or
        // clipboard/workspace records are written to the bundle.
        return sb.ToString();
    }

    internal static string ErrorKinds(string? text)
    {
        if(string.IsNullOrEmpty(text))return "No recorded error categories";
        var lines=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(100);
        var pattern=new Regex(
            @"\b(?:System\.)?[A-Za-z][A-Za-z0-9_.]*(?:Exception|Error)\b",
            RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200));
        var kinds=lines.SelectMany(l=>pattern.Matches(l)
            .Select(m=>m.Value)).Distinct(StringComparer.Ordinal).Take(30);
        var result=string.Join(Environment.NewLine,kinds);
        return string.IsNullOrEmpty(result)?"No recorded error categories":result;
    }

    internal static string ExportToDownloads()
    {
        var folder=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),"Downloads");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Root);
        var target=Path.Combine(folder,"DeskSpace_Diagnostics_"+
            DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")[..6]+".zip");
        using(var archive=ZipFile.Open(target,ZipArchiveMode.Create))
        {
            void Add(string name,string text)
            {
                var entry=archive.CreateEntry(name,CompressionLevel.Optimal);
                using var stream=new StreamWriter(entry.Open(),Encoding.UTF8);
                stream.Write(text);
            }
            Add("system-summary.txt",BuildReport());
            var crash=Path.Combine(Root,"crash-types.log");
            Add("crash-categories.txt",ErrorKinds(File.Exists(crash)?
                File.ReadAllText(crash):""));
            // Update logs may include file paths or remote addresses: extract
            // exception-type names only instead of archiving the raw logs.
            var update=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace","update-status.log");
            Add("update-error-categories.txt",ErrorKinds(File.Exists(update)?
                File.ReadAllText(update):""));
            Add("privacy.txt","No user files, passwords, app settings, notes, "+
                "account data, clipboard items, Excel sheets, or raw log lines "+
                "are included. Review before sharing.");
        }
        return target;
    }
}
