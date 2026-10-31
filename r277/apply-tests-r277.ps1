$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R277 '+$why+' count='+$count)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestEarlyWidgetStartupRecoveryV276();' @'
                TestEarlyWidgetStartupRecoveryV276();
                TestBatchRenameCrashJournalV277();
'@ 'native test registration'
$method=@'
    private static void TestBatchRenameCrashJournalV277()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceBatchRenameRecoveryV277",true)!;
        var preview=Product.GetType(
            "DeskSpace.DeskSpaceBatchRenamePreviewV254",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var inspect=type.GetMethod("Inspect",flags)!;
        var apply=type.GetMethod("Apply",flags)!;
        var recover=type.GetMethod("Recover",flags)!;
        var journalPath=type.GetMethod("JournalPath",flags)!;
        var plan=preview.GetMethod("InspectFolder",flags)!;
        object Plan(string folder)=>
            plan.Invoke(null,new object[]{folder,"R-",""})!;
        object Apply(string folder,object prepared)=>
            apply.Invoke(null,new object[]{
                folder,prepared,"R-","",System.Threading.CancellationToken.None
            })!;
        object Review(string folder)=>inspect.Invoke(null,new object[]{folder})!;
        object Recover(string folder)=>recover.Invoke(null,new object[]{
            folder,System.Threading.CancellationToken.None})!;
        bool Prop(object value,string name)=>
            (bool)value.GetType().GetProperty(name)!.GetValue(value)!;
        int Count(object value,string name)=>
            (int)value.GetType().GetProperty(name)!.GetValue(value)!;
        var root=Path.GetFullPath(Path.Combine(outdir,"r277-journal-"+
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var log=(string)journalPath.Invoke(null,new object[]{root})!;
        string Local(string filename)=>Path.Combine(root,filename);
        void Seed()
        {
            File.WriteAllText(Local("a.txt"),"ALPHA");
            File.WriteAllText(Local("b.txt"),"BETA");
        }
        try
        {
            Seed();
            var first=Apply(root,Plan(root));
            Console.WriteLine("R277 initial outcome: "+
                first.GetType().GetProperty("Message")!.GetValue(first)+
                " / journal="+File.Exists(log)+
                " / src-a="+File.Exists(Local("a.txt"))+
                " / dst-a="+File.Exists(Local("R-a.txt")));
            Assert(Prop(first,"Success") && File.Exists(log) &&
                File.Exists(Local("R-a.txt")) &&
                File.Exists(Local("R-b.txt")),
                "R277 successful Apply must flush durable journal before file moves");
            var one=Review(root);
            Assert(Prop(one,"Found") && Prop(one,"CanRecover") &&
                Count(one,"Renamed")==2 && Count(one,"Untouched")==0,
                "R277 fresh review must reconstruct durable on-disk rename receipt");
            Assert(File.Exists(Local("R-a.txt"))&&File.Exists(Local("R-b.txt")),
                "R277 inspecting recovery must never move files on its own");
            Assert(!Prop(Apply(root,Plan(root)),"Success"),
                "R277 blocks another Apply while old recovery exists");
            Assert(Prop(Recover(root),"Success") &&
                !File.Exists(log) && File.Exists(Local("a.txt")) &&
                File.ReadAllText(Local("a.txt"))=="ALPHA",
                "R277 explicit crash recovery must restore originals and clear journal");
            var interrupted=Apply(root,Plan(root));
            Assert(Prop(interrupted,"Success"),"R277 interrupted setup");
            // Simulate process termination half-way through rename: the
            // journal describes both, but one name has already reverted.
            File.Move(Local("R-b.txt"),Local("b.txt"),false);
            var mixed=Review(root);
            Assert(Prop(mixed,"CanRecover") &&
                Count(mixed,"Renamed")==1 && Count(mixed,"Untouched")==1,
                "R277 partial-crash record must distinguish moved from untouched");
            Assert(Prop(Recover(root),"Success") &&
                File.Exists(Local("a.txt")) && File.Exists(Local("b.txt")) &&
                !File.Exists(log),
                "R277 interrupted batch must recover only still-renamed files");
            Assert(Prop(Apply(root,Plan(root)),"Success"),
                "R277 fixture third Apply");
            File.WriteAllText(Local("a.txt"),"EXTERNAL CONFLICT");
            Assert(!Prop(Review(root),"CanRecover") &&
                !Prop(Recover(root),"Success") &&
                File.ReadAllText(Local("a.txt"))=="EXTERNAL CONFLICT" &&
                File.Exists(Local("R-a.txt")) && File.Exists(log),
                "R277 both names occupied must block all recovery without overwrite");
            File.Delete(Local("a.txt"));
            Assert(Prop(Recover(root),"Success"),
                "R277 removing conflict permits a fresh, explicitly approved recovery");
            Assert(Prop(Apply(root,Plan(root)),"Success"),
                "R277 fixture all-original state");
            File.Move(Local("R-a.txt"),Local("a.txt"),false);
            File.Move(Local("R-b.txt"),Local("b.txt"),false);
            Assert(Count(Review(root),"Renamed")==0 &&
                Prop(Recover(root),"Success") && !File.Exists(log),
                "R277 journal before first move must clear only by explicit recovery");
            Assert(Prop(Apply(root,Plan(root)),"Success"),
                "R277 fixture corrupt record");
            File.WriteAllText(log,"{corrupted json, no trust");
            Assert(Prop(Review(root),"Found") &&
                !Prop(Review(root),"CanRecover") &&
                !Prop(Recover(root),"Success") &&
                File.Exists(Local("R-a.txt")),
                "R277 malformed disk journal must fail closed without any file changes");
            Console.WriteLine("R277 PASS: durable disk journal, restart review, "+
                "partial-crash recovery, user consent, conflict blocking, "+
                "no automatic moves, and malformed journal refusal.");
        }
        finally
        {
            try{if(File.Exists(log))File.Delete(log);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            try{Directory.Delete(root,true);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
Swap '    private static void TestEarlyWidgetStartupRecoveryV276()' ($method+
    '    private static void TestEarlyWidgetStartupRecoveryV276()') 'native recovery test implementation'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R277 native Windows journal, partial-crash undo and no-overwrite recovery registered.'
