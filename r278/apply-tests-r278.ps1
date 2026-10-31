$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$next,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R278 '+$reason+' anchor='+$count)}
 $script:s=$script:s.Replace($old,$next)
}
One '                TestBatchRenameCrashJournalV277();' @'
                TestBatchRenameCrashJournalV277();
                TestIndependentMonitorPositionStoreV278();
'@ 'register native monitor test'
$method=@'
    private static void TestIndependentMonitorPositionStoreV278()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceMonitorPositionStoreV278",true)!;
        const BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
        var save=type.GetMethod("SaveAt",hidden)!;
        var read=type.GetMethod("ReadAt",hidden)!;
        var parse=type.GetMethod("ParseForWrite",hidden)!;
        var mutexName=type.GetMethod("MutexName",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var file=Path.GetFullPath(Path.Combine(outdir,
            "r278-monitors-"+Guid.NewGuid().ToString("N")+".json"));
        string primary=@"\\.\DISPLAY1",secondary=@"\\.\DISPLAY2";
        void Save(string name,int x,int y)=>
            save.Invoke(null,new object[]{file,name,x,y});
        object? Read(string name)=>
            read.Invoke(null,new object[]{file,name});
        int X(object item)=>(int)item.GetType()
            .GetProperty("X")!.GetValue(item)!;
        int Y(object item)=>(int)item.GetType()
            .GetProperty("Y")!.GetValue(item)!;
        try
        {
            Save(primary,-1750,90);
            Save(secondary,1640,180);
            Save(primary,-1580,120);
            Assert(X(Read(primary)!)==-1580 && Y(Read(primary)!)==120 &&
                   X(Read(secondary)!)==1640 &&
                   Read(@"\\.\DISPLAY3")==null,
                "R278 updating one monitor must preserve another and never copy positions");
            var tasks=Enumerable.Range(3,10).Select(i=>
                System.Threading.Tasks.Task.Run(()=>
                    Save(@"\\.\DISPLAY"+i,1500+i*10,50+i))).ToArray();
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert(X(Read(primary)!)==-1580 &&
                X(Read(secondary)!)==1640 &&
                Enumerable.Range(3,10).All(i=>
                    Read(@"\\.\DISPLAY"+i)!=null),
                "R278 concurrent independent monitor saves must preserve all identities");
            var raw=File.ReadAllText(file);
            var rows=(System.Collections.IEnumerable)parse.Invoke(null,
                new object?[]{raw,(long)new FileInfo(file).Length})!;
            Assert(rows.Cast<object>().Count()==12,
                "R278 existing widget position file is legacy-list compatible");
            bool badDuplicate=false;
            try
            {
                parse.Invoke(null,new object[]{
                    "[{\"Device\":\"MON\",\"X\":1,\"Y\":2},"+
                    "{\"Device\":\"mon\",\"X\":5,\"Y\":6}]",67L
                });
            }
            catch(TargetInvocationException ex) when(
                ex.InnerException is InvalidDataException)
            {badDuplicate=true;}
            Assert(badDuplicate,"R278 case-colliding device IDs must fail closed");
            var name=(string)mutexName.Invoke(null,new object[]{file})!;
            using(var lockForAnotherProcess=new System.Threading.Mutex(false,name))
            {
                Assert(lockForAnotherProcess.WaitOne(TimeSpan.FromSeconds(1)),
                    "R278 named position mutex should be available to the test");
                try
                {
                    var refused=System.Threading.Tasks.Task.Run(()=>{
                        try{Save(secondary,777,777);return false;}
                        catch(TargetInvocationException ex) when(
                            ex.InnerException is IOException){return true;}
                    }).GetAwaiter().GetResult();
                    Assert(refused && X(Read(secondary)!)==1640,
                        "R278 cross-thread/OS mutex contention must preserve stored positions");
                }
                finally{lockForAnotherProcess.ReleaseMutex();}
            }
            File.WriteAllText(file,"{broken monitor state");
            var original=File.ReadAllText(file);
            bool blocked=false;
            try{Save(primary,1,1);}
            catch(TargetInvocationException ex) when(
                ex.InnerException is InvalidDataException){blocked=true;}
            Assert(blocked && File.ReadAllText(file)==original &&
                   Read(secondary)==null,
                "R278 malformed position JSON must never be overwritten or reused");
            Console.WriteLine("R278 PASS: real atomic multi-monitor saves, "+
                "concurrent identities, named OS mutex contention, "+
                "corrupt-map preservation and no cross-monitor fallback.");
        }
        finally
        {
            try{if(File.Exists(file))File.Delete(file);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
One '    private static void TestBatchRenameCrashJournalV277()' ($method+
 '    private static void TestBatchRenameCrashJournalV277()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R278 native Windows tests registered for monitor positions with no physical monitor assumptions.'
