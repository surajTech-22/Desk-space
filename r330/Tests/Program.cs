using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    static void Check(bool ok,string why)
    {
        if(!ok)throw new InvalidOperationException("R330 search memory regression: "+why);
    }
    [STAThread] static int Main()
    {
        var asm=typeof(DeskSpace.WeatherReading).Assembly;
        var disk=asm.GetType("DeskSpace.DeskSpaceDiskSearchV330",true)!;
        var search=asm.GetType("DeskSpace.DeskSpaceSearchIndexV13",true)!;
        var item=disk.GetNestedType("Item",BindingFlags.NonPublic)!;
        var begin=disk.GetMethod("BeginScan",Flags)!;
        var batchWrite=disk.GetMethod("WriteBatch",Flags)!;
        var query=disk.GetMethod("Query",Flags)!;
        var finish=disk.GetMethod("CompleteScan",Flags)!;
        var count=disk.GetMethod("Count",Flags)!;
        var notify=disk.GetMethod("Enqueue",Flags)!;
        var flush=disk.GetMethod("FlushPending",Flags)!;
        var entries=search.GetField("Entries",Flags)!;
        var appData=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"DeskSpace","Search");
        string testDrive="R330-TEST:"+Guid.NewGuid().ToString("N")+"\\";
        try
        {
            GC.Collect(2,GCCollectionMode.Forced,true,true);
            long start=GC.GetTotalMemory(true);
            long epoch=(long)begin.Invoke(null,null)!;
            int rows=45000;
            var ctor=item.GetConstructors().Single();
            var generic=typeof(List<>).MakeGenericType(item);
            var batch=(IList)Activator.CreateInstance(generic,384)!;
            // Construct 45k unique paths but NEVER buffer them all; indexes
            // that keep the full catalog in managed RAM fail this acceptance.
            for(int i=0;i<rows;i++)
            {
                string name=i==43123?"tax_invoice_rareneedle_43123.xlsx":
                    "normal_document_"+i.ToString("D7")+".pdf";
                string path=testDrive+"Folder"+(i/250).ToString("D3")+"\\"+name;
                batch.Add(ctor.Invoke(new object[]{name,path,false,testDrive}));
                if(batch.Count<384)continue;
                batchWrite.Invoke(null,new object[]{testDrive,epoch,batch});
                batch.Clear();
            }
            if(batch.Count>0)
            {
                batchWrite.Invoke(null,new object[]{testDrive,epoch,batch});
                batch.Clear();
            }
            finish.Invoke(null,new object[]{testDrive,epoch});
            GC.Collect(2,GCCollectionMode.Forced,true,true);
            long after=GC.GetTotalMemory(true);
            double riseMb=(after-start)/1048576.0;
            Check(riseMb<85.0,$"managed heap grew {riseMb:F1} MiB for 45k catalog names");
            Check((long)count.Invoke(null,new object?[]{testDrive})! == rows,
                "disk index did not retain every filename from scan");
            var results=((IEnumerable)query.Invoke(null,new object?[]{
                "rareneedle",testDrive,CancellationToken.None,640,false})!)
                .Cast<object>().ToArray();
            Check(results.Length==1 &&
                (string)results[0].GetType().GetProperty("Name")!.GetValue(results[0])!
                    =="tax_invoice_rareneedle_43123.xlsx",
                "substring filename search missed deep file from on-disk FTS catalog");
            Check(((IEnumerable)entries.GetValue(null)!).Cast<object>().Count()==0,
                "old memory-only full-drive Entries list still retains all filenames");
            for(int i=0;i<120;i++)
            {
                var hits=((IEnumerable)query.Invoke(null,new object?[]{
                    "normal_document",testDrive,CancellationToken.None,640,false})!).Cast<object>().Count();
                Check(hits>0&&hits<=640,"disk query result limit broken");
            }
            GC.Collect(2,GCCollectionMode.Forced,true,true);
            double rereadMb=(GC.GetTotalMemory(true)-after)/1048576.0;
            Check(rereadMb<35.0,$"120 repeated searches leaked {rereadMb:F1} MiB");
            // Stress watcher input without writing all events to SQLite.
            for(int i=0;i<20000;i++)
                notify.Invoke(null,new object[]{"C:\\DeskSpaceSynthetic\\"+i+".txt",true});
            int pending=(int)disk.GetProperty("PendingCount",Flags)!.GetValue(null)!;
            Check(pending<=4096,"watcher event backlog exceeded fixed 4096 cap");
            flush.Invoke(null,null);
            Console.WriteLine("R330 PASS: 45,000 indexed paths; rare deep XLSX substring found; "+
                $"managed rise {riseMb:F1} MiB; 120 searches retained {rereadMb:F1} MiB; "+
                "legacy full-drive heap list empty; event backlog <=4096.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            // Index is a disposable synthetic cache. Never touch user files;
            // test rows are isolated by unique fake volume prefix.
            try
            {
                var cleanup=disk.GetMethod("RemoveVolumeForTest",Flags);
                cleanup?.Invoke(null,new object[]{testDrive});
            }
            catch(Exception){}
        }
    }
}
