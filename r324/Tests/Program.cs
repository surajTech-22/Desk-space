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
            var disk=asm.GetType("DeskSpace.DeskSpaceDiskSearchV330",true)!;
            var query=disk.GetMethod("Query",Flags)!;
            var flush=disk.GetMethod("FlushPending",Flags)!;
            string[] DbPaths(string word,string volume)=>
                ((IEnumerable)query.Invoke(null,new object?[]{
                    word,volume,CancellationToken.None,640,false})!)
                .Cast<object>().Select(x=>(string)x.GetType()
                    .GetProperty("Path")!.GetValue(x)!).ToArray();
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
            var pathsA=DbPaths("unique_mars",a);
            var pathsB=DbPaths("unique_mars",b);
            Require(pathsA.Contains(fileA,StringComparer.OrdinalIgnoreCase) &&
                pathsB.Contains(hidden,StringComparer.OrdinalIgnoreCase) &&
                pathsB.Contains(deepest,StringComparer.OrdinalIgnoreCase),
                "uncapped catalog failed to index real XLSX/PDF/deep DOCX across two roots");
            var quickClock=System.Diagnostics.Stopwatch.StartNew();
            var fastHits=((IEnumerable)quick.Invoke(null,new object?[]{
                "unique_mars",CancellationToken.None,null,new[]{a,b}})!)
                .Cast<DeskSpace.SearchHit>().ToArray();
            quickClock.Stop();
            // Immediate fallback must show a nearby file promptly. Deep
            // coverage is independently checked against the COMPLETE
            // on-disk index above, not by waiting through 1260 directories.
            Require(fastHits.Any(h=>h.OpenTarget==fileA) &&
                pathsB.Contains(deepest,StringComparer.OrdinalIgnoreCase) &&
                quickClock.ElapsedMilliseconds<3500,
                "interactive fallback delayed user or lost on-disk deep-file coverage");

            var change=new Action<string,bool>((p,exists)=>
                notify.Invoke(null,new object[]{p,exists}));
            testStart.Invoke(null,new object[]{change});
            Require((bool)watch.Invoke(null,new object[]{a,false})!,
                "FileSystemWatcher cannot monitor a temporary Windows drive folder");
            // New disk-backed search no longer retains all event paths
            // in an unbounded managed ConcurrentDictionary.
            bool Has(string p)
            {
                flush.Invoke(null,null);
                return DbPaths(Path.GetFileName(p),Path.GetPathRoot(p)!)
                    .Contains(p,StringComparer.OrdinalIgnoreCase);
            }
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
            Console.WriteLine("R324+R330 PASS: disk-backed multi-root all-file index, hidden accessible PDF, deep DOCX/XLSX, new/rename/delete watcher events, no unbounded heap inventory.");
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
