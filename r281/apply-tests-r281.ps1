$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R281 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestSmartContextDeckOptInV280();' @'
                TestSmartContextDeckOptInV280();
                TestWorkspaceProfileStorageV281();
'@ 'native test registration'
$method=@'
    private static void TestWorkspaceProfileStorageV281()
    {
        var store=Product.GetType("DeskSpace.DeskSpaceProfileStoreV281",true)!;
        var profile=Product.GetType("DeskSpace.DeskSpaceProfileV85",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var save=store.GetMethod("SaveAt",flags)!;
        var load=store.GetMethod("ReadForWrite",flags)!;
        var import=store.GetMethod("ImportAt",flags)!;
        var mutexName=store.GetMethod("MutexName",flags)!;
        var folder=Path.GetFullPath(Path.Combine(outdir,
            "r281-profiles-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"widget-profiles.json");
        object Make(string name)
        {
            var p=Activator.CreateInstance(profile,true)!;
            profile.GetProperty("Name")!.SetValue(p,name);
            return p;
        }
        void Save(string name)=>save.Invoke(null,
            new[]{(object)path,Make(name)});
        object List(params string[] names)
        {
            var t=typeof(List<>).MakeGenericType(profile);
            var result=(System.Collections.IList)Activator.CreateInstance(t)!;
            foreach(var name in names)result.Add(Make(name));
            return result;
        }
        string[] Names()=>
            ((System.Collections.IEnumerable)load.Invoke(null,
                new object[]{path})!).Cast<object>()
            .Select(x=>(string)profile.GetProperty("Name")!.GetValue(x)!)
            .ToArray();
        try
        {
            Save("Accounting");
            Save("Personal");
            var tasks=Enumerable.Range(1,8).Select(i=>
                System.Threading.Tasks.Task.Run(()=>Save("Workspace"+i)))
                .ToArray();
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert(Names().Length==10 &&
                Names().Contains("Accounting") &&
                Names().Contains("Personal") &&
                Enumerable.Range(1,8).All(i=>Names().Contains("Workspace"+i)),
                "R281 simultaneous profile saves must preserve every distinct entry");
            string prior=File.ReadAllText(path);
            int imported=(int)import.Invoke(null,
                new object[]{path,List("Accounting","Research")})!;
            Assert(imported==1 && Names().Length==11 &&
                   Names().Contains("Research") &&
                   File.ReadAllText(path)!=prior &&
                   Directory.GetFiles(folder,"*.bak").Length==1,
                "R281 import must retain existing names, add only fresh rows "+
                "and keep a recovery copy");
            var noChange=File.ReadAllText(path);
            int none=(int)import.Invoke(null,
                new object[]{path,List("Accounting")})!;
            Assert(none==0 && File.ReadAllText(path)==noChange,
                "R281 already-imported name must not trigger another write");
            bool dup=false;
            File.WriteAllText(path,
                "[{\"Name\":\"Accounting\",\"CenterMode\":\"Clock\","+
                "\"ShowWeather\":false,\"WeatherCity\":\"\",\"PinnedApps\":[]},"+
                "{\"Name\":\"accounting\",\"CenterMode\":\"Clock\","+
                "\"ShowWeather\":false,\"WeatherCity\":\"\",\"PinnedApps\":[]}]");
            var duplicateFile=File.ReadAllText(path);
            try{Save("New");}
            catch(TargetInvocationException ex) when(
                ex.InnerException is InvalidDataException){dup=true;}
            Assert(dup && File.ReadAllText(path)==duplicateFile,
                "R281 case-colliding saved profiles must not be overwritten");
            File.WriteAllText(path,"{ this is corrupted JSON");
            var corrupted=File.ReadAllText(path);
            bool saveBlocked=false,importBlocked=false;
            try{Save("New");}
            catch(TargetInvocationException ex) when(
                ex.InnerException is InvalidDataException){saveBlocked=true;}
            try{import.Invoke(null,new object[]{path,List("Another")});}
            catch(TargetInvocationException ex) when(
                ex.InnerException is InvalidDataException){importBlocked=true;}
            Assert(saveBlocked && importBlocked &&
                   File.ReadAllText(path)==corrupted,
                "R281 damaged profile database must survive Save and Import");
            File.Delete(path);
            Save("Accounting");
            var named=(string)mutexName.Invoke(null,new object[]{path})!;
            using(var locked=new System.Threading.Mutex(false,named))
            {
                Assert(locked.WaitOne(TimeSpan.FromSeconds(1)),
                    "R281 OS named profile lock available");
                try
                {
                    bool rejected=System.Threading.Tasks.Task.Run(()=>{
                        try{Save("Other");return false;}
                        catch(TargetInvocationException ex) when(
                            ex.InnerException is IOException){return true;}
                    }).GetAwaiter().GetResult();
                    Assert(rejected && Names().SequenceEqual(new[]{"Accounting"}),
                        "R281 independent writer contention must preserve profiles");
                }
                finally{locked.ReleaseMutex();}
            }
            Console.WriteLine("R281 PASS: concurrent saved Workspace profiles, "+
                "import backup, zero-change imports, corrupt/duplicate "+
                "file preservation and cross-process OS mutex contention.");
        }
        finally
        {
            try{Directory.Delete(folder,true);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
Swap '    private static void TestSmartContextDeckOptInV280()' ($method+
 '    private static void TestSmartContextDeckOptInV280()') 'native test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R281: native Windows Workspace profile store and concurrent corruption-protection fixtures registered.'
