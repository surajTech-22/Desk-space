using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

internal static class Program
{
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static void Assert(bool condition,string message)
    {
        if(!condition)throw new Exception("R332 end-to-end search regression: "+message);
    }
    [STAThread] static int Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),
            "DeskSpaceR332-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string name="r332_unique_invoice_"+Guid.NewGuid().ToString("N")[..12]+".xlsx";
        string file=Path.Combine(folder,name);
        File.WriteAllText(file,"not a real workbook; filename search fixture only");
        try
        {
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var idx=asm.GetType("DeskSpace.DeskSpaceSearchIndexV13",true)!;
            var disk=asm.GetType("DeskSpace.DeskSpaceDiskSearchV330",true)!;
            var read=idx.GetMethod("Search",BindingFlags.Public|Static)!;
            var started=idx.GetField("started",Static)!;
            var completed=idx.GetField("completed",Static)!;
            // No real-C: full-drive scan: insert just the synthetic file.
            started.SetValue(null,true);
            completed.SetValue(null,true);
            string volume=Path.GetPathRoot(file)!;
            long epoch=(long)disk.GetMethod("BeginScan",Static)!.Invoke(null,null)!;
            var itemType=disk.GetNestedType("Item",BindingFlags.NonPublic)!;
            var item=itemType.GetConstructors().Single().Invoke(
                new object[]{name,file,false,volume});
            var listType=typeof(List<>).MakeGenericType(itemType);
            var rows=(IList)Activator.CreateInstance(listType)!;
            rows.Add(item);
            disk.GetMethod("WriteBatch",Static)!
                .Invoke(null,new object[]{volume,epoch,rows});
            var ws=new DeskSpace.Workspace();
            List<DeskSpace.SearchHit> Search(string text)=>
                (List<DeskSpace.SearchHit>)read.Invoke(null,new object[]{
                    text,ws,CancellationToken.None})!;
            var clock=Stopwatch.StartNew();
            var found=Search("r332_unique_invoice");
            clock.Stop();
            Assert(found.Any(h=>h.OpenTarget.Equals(file,
                    StringComparison.OrdinalIgnoreCase)),
                "actual shipping Search() returned no XLSX file from disk index");
            Assert(clock.ElapsedMilliseconds<5000,
                "first filename search too slow: "+clock.ElapsedMilliseconds+"ms");
            var flag=disk.GetField("SimulateReadFailureForTestV332",Static)!;
            flag.SetValue(null,true);
            try
            {
                var fallbackClock=Stopwatch.StartNew();
                var settings=Search("Display");
                fallbackClock.Stop();
                Assert(settings.Any(h=>h.Category=="Settings" &&
                    h.Name.Contains("Display",StringComparison.OrdinalIgnoreCase)),
                    "failed disk index caused ALL Windows Settings results to vanish");
                Assert(fallbackClock.ElapsedMilliseconds<5000,
                    "fallback after disk error too slow");
                var note=new DeskSpace.NoteItem{
                    Title="r332_unique_notes",
                    Content="Indexed storage failure should not hide notes"
                };
                ws.Notes.Add(note);
                var notes=Search("r332_unique_notes");
                Assert(notes.Any(h=>h.Category=="Notes"),
                    "failed disk index caused existing workspace Notes results to vanish");
            }
            finally{flag.SetValue(null,false);}
            var repeat=Stopwatch.StartNew();
            for(int i=0;i<12;i++)
            {
                var results=Search("r332_unique_invoice");
                Assert(results.Any(h=>h.OpenTarget.Equals(file,
                    StringComparison.OrdinalIgnoreCase)),
                    "rapid repeated user searches dropped known index results");
            }
            repeat.Stop();
            Assert(repeat.ElapsedMilliseconds<16000,
                "12 repeated searches exceeded expected latency budget");
            Console.WriteLine("R332 PASS: real shipping Search() finds indexed XLSX in "+
                clock.ElapsedMilliseconds+"ms, remains functional with an injected SQLite "+
                "failure (Settings and Notes visible), and survives 12 repeated searches.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            try{Directory.Delete(folder,true);}catch(Exception){}
        }
    }
}
