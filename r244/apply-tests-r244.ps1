$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R244 "+$why+": expected one anchor; got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestTwoMonitorMediaOwnershipV243();' @'
                TestTwoMonitorMediaOwnershipV243();
                TestFileDuplicatesV244();
'@ 'register native duplicate fixture test'
$method=@'
    private static void TestFileDuplicatesV244()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceFileDuplicatesV244",true)!;
        var scan=type.GetMethod("Scan",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        (bool Good,int Count,int Groups,string[] Names) Inspect(
            string folder,System.Threading.CancellationToken token)
        {
            var result=scan.Invoke(null,new object[]{folder,token})!;
            var t=result.GetType();
            bool allowed=(bool)t.GetProperty("Accepted")!.GetValue(result)!;
            int count=(int)t.GetProperty("Examined")!.GetValue(result)!;
            var groups=((System.Collections.IEnumerable)
                t.GetProperty("Groups")!.GetValue(result)!).Cast<object>().ToArray();
            var names=groups.SelectMany(group=>((System.Collections.IEnumerable)
                group.GetType().GetProperty("Names")!.GetValue(group)!)
                .Cast<string>()).ToArray();
            return (allowed,count,groups.Length,names);
        }
        var folder=Path.Combine(outdir,"r244-duplicates");
        Directory.CreateDirectory(folder);
        try
        {
            string a=Path.Combine(folder,"A.txt");
            string b=Path.Combine(folder,"B.txt");
            string c=Path.Combine(folder,"C.txt");
            File.WriteAllText(a,"identical text content");
            File.WriteAllText(b,"identical text content");
            File.WriteAllText(c,"completely different");
            string before=Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a)));
            Directory.CreateDirectory(Path.Combine(folder,"nested"));
            File.WriteAllText(Path.Combine(folder,"nested","nested-copy.txt"),
                "identical text content");
            string hidden=Path.Combine(folder,"hidden-copy.txt");
            File.WriteAllText(hidden,"identical text content");
            File.SetAttributes(hidden,FileAttributes.Hidden);
            var result=Inspect(folder,System.Threading.CancellationToken.None);
            Assert(result.Good && result.Groups==1 &&
                result.Count==3 && result.Names.Length==2 &&
                result.Names.Contains("A.txt") && result.Names.Contains("B.txt") &&
                !result.Names.Contains("nested-copy.txt") &&
                !result.Names.Contains("hidden-copy.txt"),
                "R244 exact same-hash group, no recursion and hidden file excluded");
            string after=Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a)));
            Assert(before==after && File.Exists(b) && File.Exists(c),
                "R244 read-only hash scan must never change any selected file");
            using(var scope=new System.Threading.CancellationTokenSource())
            {
                scope.Cancel();
                bool cancelled=false;
                try{Inspect(folder,scope.Token);}
                catch(System.Reflection.TargetInvocationException ex)
                {
                    cancelled=ex.InnerException is OperationCanceledException;
                }
                Assert(cancelled,"R244 cancelled scan must not continue");
            }
            var massive=Path.Combine(folder,"huge.bin");
            using(var stream=File.Create(massive))stream.SetLength(64L*1024*1024+1);
            Assert(!Inspect(folder,System.Threading.CancellationToken.None).Good,
                "R244 reject oversized file before any hashing");
            File.Delete(massive);
            for(int i=0;i<130;i++)
                File.WriteAllText(Path.Combine(folder,"extra-"+i+".txt"),"x");
            Assert(!Inspect(folder,System.Threading.CancellationToken.None).Good,
                "R244 reject more than 128 eligible files before any hashing");
            Assert(type.GetMethod("Show",BindingFlags.NonPublic|
                BindingFlags.Static)!=null,"R244 user-initiated WPF action missing");
            Console.WriteLine(
                "R244 PASS: SHA-256 duplicates, non-recursion, hidden exclusion, "+
                "cancellation, preflight caps, source preservation and native UI entry.");
        }
        finally{Directory.Delete(folder,true);}
    }

'@
Swap '    private static void TestTwoMonitorMediaOwnershipV243()' ($method+
 '    private static void TestTwoMonitorMediaOwnershipV243()') 'inject on-disk fixture test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R244: real-file native Windows duplicate inspector regression registered.'
