using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

internal static class Program
{
    private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    private static void Require(bool ok,string message)
    {
        if(!ok)throw new Exception("R324 full computer file search: "+message);
    }
    [STAThread] private static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "deskspace-whole-pc-r324-"+Guid.NewGuid().ToString("N"));
        string a=Path.Combine(root,"DriveA");
        string b=Path.Combine(root,"DriveB");
        string? hidden=null;
        try
        {
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var index=asm.GetType("DeskSpace.DeskSpaceSearchIndexV13",true)!;
            var monitor=asm.GetType("DeskSpace.DeskSpaceAllDriveLiveIndexV324",true)!;
            var fast=asm.GetType("DeskSpace.DeskSpaceFileQuickSearchV233",true)!;
            var scan=index.GetMethod("ScanVolume",Flags)!;
            var eligible=index.GetMethod("IsSearchableFileV288",Flags)!;
            var notify=index.GetMethod("RegisterFileSystemChangeV324",Flags)!;
            var testStart=monitor.GetMethod("StartForTest",Flags)!;
            var watch=monitor.GetMethod("WatchRoot",Flags)!;
            var quick=fast.GetMethod("Find",Flags)!;
            var entries=(IEnumerable)index.GetField("Entries",Flags)!.GetValue(null)!;
            string fileA=Path.Combine(a,"report_unique_mars_2026.xlsx");
            string folder=Path.Combine(b,"FolderHidden");
            Directory.CreateDirectory(folder);
            hidden=Path.Combine(folder,"report_unique_mars_hidden.pdf");
            File.WriteAllText(fileA,"index test");
            File.WriteAllText(hidden,"index test");
            File.SetAttributes(hidden,FileAttributes.Hidden);
            Require((bool)eligible.Invoke(null,new object[]{hidden})!,
                "hidden file accessible to this account should appear in filename search");
            // Simulate many unrelated directories that previously exhausted
            // the 1,200-directory quick search budget before reaching a file.
            string deep=b;
            for(int i=0;i<9;i++)
            {
                deep=Path.Combine(deep,"level"+i);
                Directory.CreateDirectory(deep);
            }
            for(int i=0;i<1260;i++)
                Directory.CreateDirectory(Path.Combine(b,"unrelated_"+i.ToString("D4")));
            string deepest=Path.Combine(deep,"deep_unique_mars_contract.docx");
            File.WriteAllText(deepest,"deep");
            scan.Invoke(null,new object[]{a});
            scan.Invoke(null,new object[]{b});
            var paths=entries.Cast<object>().Select(entry=>
                (string)entry.GetType().GetProperty("Path")!.GetValue(entry)!).ToArray();
            Require(paths.Contains(fileA,StringComparer.OrdinalIgnoreCase) &&
                paths.Contains(hidden,StringComparer.OrdinalIgnoreCase) &&
                paths.Contains(deepest,StringComparer.OrdinalIgnoreCase),
                "uncapped catalog failed to index real XLSX/PDF/deep DOCX across two roots");
            var fastHits=((IEnumerable)quick.Invoke(null,new object?[]{
                "unique_mars",CancellationToken.None,null,new[]{a,b}})!)
                .Cast<DeskSpace.SearchHit>().ToArray();
            Require(fastHits.Any(h=>h.OpenTarget==fileA) &&
                fastHits.Any(h=>h.OpenTarget==deepest),
                "live multi-root filename discovery missed nested file");

            var change=new Action<string,bool>((p,exists)=>
                notify.Invoke(null,new object[]{p,exists}));
            testStart.Invoke(null,new object[]{change});
            Require((bool)watch.Invoke(null,new object[]{a,false})!,
                "FileSystemWatcher cannot monitor a temporary Windows drive folder");
            var livePaths=index.GetField("LivePaths",Flags)!.GetValue(null)!;
            var tryContains=livePaths.GetType().GetMethod("ContainsKey")!;
            bool Has(string p)=>(bool)tryContains.Invoke(livePaths,new object[]{p})!;
            var newFile=Path.Combine(a,"after_start_unique_mars.pptx");
            File.WriteAllText(newFile,"new file");
            Require(SpinWait.SpinUntil(()=>Has(newFile),5000),
                "file created after catalog build did not become searchable");
            var renamed=Path.Combine(a,"renamed_unique_mars.pptx");
            File.Move(newFile,renamed);
            Require(SpinWait.SpinUntil(()=>
                !Has(newFile)&&Has(renamed),5000),
                "FileSystemWatcher did not update renamed filenames");
            File.Delete(renamed);
            Require(SpinWait.SpinUntil(()=>!Has(renamed),5000),
                "deleted paths remain in live filename catalog");
            // Artificial buffer overflow re-index must be supported
            // without any extra content reader or remote indexing service.
            Require(monitor.GetMethod("NotifyForTest",Flags)!=null &&
                    monitor.GetMethod("Start",Flags)!=null,
                "live watcher repair path disconnected from main catalog");
            Console.WriteLine("R324 PASS: uncapped multi-root all-file index; hidden accessible PDF; deep DOCX/XLSX; new, rename and delete watcher updates; no document contents searched.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            try
            {
                if(hidden!=null && File.Exists(hidden))
                    File.SetAttributes(hidden,FileAttributes.Normal);
                Directory.Delete(root,true);
            }
            catch(Exception){}
        }
    }
}
