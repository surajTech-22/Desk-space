using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DeskSpace;

internal static class Program
{
    static readonly BindingFlags Stat=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly BindingFlags Inst=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool good,string reason)
    {
        if(!good)throw new InvalidOperationException("R368: "+reason);
    }

    [STAThread] static int Main()
    {
        var disk=typeof(WeatherReading).Assembly.GetType(
            "DeskSpace.DeskSpaceDiskSearchV330",true)!;
        var sessionType=disk.GetNestedType("FullScanSessionV368",
            BindingFlags.NonPublic)!;
        var itemType=disk.GetNestedType("Item",BindingFlags.NonPublic)!;
        var ctor=itemType.GetConstructors().Single();
        var listType=typeof(System.Collections.Generic.List<>).MakeGenericType(itemType);
        var open=sessionType.GetConstructor(Inst,null,
            new[]{typeof(string)},null)!;
        var write=sessionType.GetMethod("WriteBatch",Inst)!;
        var complete=sessionType.GetMethod("Complete",Inst)!;
        var writes=sessionType.GetProperty("DurableWritesV368",Inst)!;
        var count=disk.GetMethod("Count",Stat)!;
        var query=disk.GetMethod("Query",Stat)!;
        var cleanup=disk.GetMethod("CompleteScan",Stat)!;
        string volume="R368-TEST-"+Guid.NewGuid().ToString("N")+":\\";
        IList MakeBatch(int first,int last,bool altered=false)
        {
            var list=(IList)Activator.CreateInstance(listType)!;
            for(int i=first;i<last;i++)
            {
                string name=i==1678?"urgent_rareinvoice_1678.xlsx":
                    "statement_"+i.ToString("D6")+".pdf";
                string path=volume+"Folder"+(i/125).ToString("D3")+"\\"+name;
                list.Add(ctor.Invoke(new object[]{name,path,altered&&i==25,volume}));
            }
            return list;
        }
        (long Writes, long Count) Scan(int first,int last,bool completeScan=true,
            bool altered=false)
        {
            using var scan=(IDisposable)open.Invoke(new object[]{volume});
            for(int pos=first;pos<last;pos+=300)
                write.Invoke(scan,new object[]{MakeBatch(pos,Math.Min(last,pos+300),altered)});
            long changes=(long)writes.GetValue(scan)!;
            if(completeScan)complete.Invoke(scan,null);
            return (changes,(long)count.Invoke(null,new object?[]{volume})!);
        }

        try
        {
            const int rows=2100;
            var first=Scan(0,rows);
            Check(first.Writes==rows&&first.Count==rows,
                "initial whole-drive cache missed filenames");
            var second=Scan(0,rows);
            Check(second.Writes==0&&second.Count==rows,
                "identical startup scan rewrote unchanged durable SQLite rows");
            var updated=Scan(0,rows,altered:true);
            Check(updated.Writes==1&&updated.Count==rows,
                "one changed entry was not the only durable update");
            var oldAgain=Scan(0,rows);
            Check(oldAgain.Writes==1&&oldAgain.Count==rows,
                "changed metadata did not reconcile correctly");
            var aborted=Scan(0,200,completeScan:false);
            Check(aborted.Count==rows,
                "incomplete scan deleted existing saved filenames");
            var deleted=Scan(0,rows-1);
            Check(deleted.Count==rows-1,
                "completed scan failed to remove missing filename");
            var results=((IEnumerable)query.Invoke(null,new object?[]{
                "rareinvoice",volume,CancellationToken.None,640,false})!)
                .Cast<object>().ToArray();
            Check(results.Length==1,"rare deep XLSX search changed");
            var restored=Scan(0,rows);
            Check(restored.Writes==1&&restored.Count==rows,
                "restoring missing indexed filename failed");
            Check((long)count.Invoke(null,new object?[]{volume})! ==rows,
                "final cached catalog inaccurate");
            Console.WriteLine("R368 PASS: full-drive filenames retained; identical"+
                " repeat scan performs ZERO durable updates; one changed"+
                " record updates once; cancelled scans never delete;"+
                " completed scans remove missing records; deep XLSX search intact.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally
        {
            try{cleanup.Invoke(null,new object[]{volume,long.MaxValue});}
            catch(Exception){}
        }
    }
}
