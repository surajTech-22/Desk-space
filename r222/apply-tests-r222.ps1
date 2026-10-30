$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R222 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestUpdateChecksumStagingV219();' @'
                TestUpdateChecksumStagingV219();
                TestInstallerAtomicV222();
'@ 'native call'
$method=@'
    private static void TestInstallerAtomicV222()
    {
        var t=Product.GetType("DeskSpace.DeskSpaceAtomicInstallerV222",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var stage=t.GetMethod("StageFromStreamAsync",flags)!;
        var cleanup=t.GetMethod("CleanupAbandoned",flags)!;
        var root=Path.Combine(Path.GetTempPath(),
            "deskspace-r222-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var installer=Path.Combine(root,"DeskSpace_Setup.exe");
        var original=System.Text.Encoding.UTF8.GetBytes("pre-existing verified installer");
        File.WriteAllBytes(installer,original);
        async Task Stage(byte[] bytes,System.Threading.CancellationToken token)
        {
            using var input=new MemoryStream(bytes,writable:false);
            try
            {
                var task=(Task)stage.Invoke(null,new object[]{input,installer,token})!;
                await task;
            }
            catch(TargetInvocationException e)when(e.InnerException!=null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                    e.InnerException).Throw();
                throw;
            }
        }
        try
        {
            bool tooSmall=false;
            try{Stage(new byte[64],System.Threading.CancellationToken.None)
                    .GetAwaiter().GetResult();}
            catch(InvalidDataException){tooSmall=true;}
            Assert(tooSmall &&
                File.ReadAllBytes(installer).SequenceEqual(original),
                "R222 truncated transfer must retain the previous cached installer");
            Assert(!Directory.GetFiles(root,"*.partial").Any(),
                "R222 failed transfer must clean temporary installer");
            var payload=new byte[5_000_256];
            payload[0]=0x4d;payload[1]=0x5a;payload[^1]=0x7f;
            Stage(payload,System.Threading.CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(File.ReadAllBytes(installer).SequenceEqual(payload),
                "R222 completed installer must atomically replace the cached file");
            Assert(!Directory.GetFiles(root,"*.partial").Any(),
                "R222 successful transfer leaves no .partial file");
            using(var cts=new System.Threading.CancellationTokenSource())
            {
                cts.Cancel();
                bool stopped=false;
                try{Stage(payload,cts.Token).GetAwaiter().GetResult();}
                catch(OperationCanceledException){stopped=true;}
                Assert(stopped&&File.ReadAllBytes(installer).SequenceEqual(payload),
                    "R222 cancellation must not modify an already verified installer");
            }
            var stale=Path.Combine(root,"DeskSpace_Setup.exe."+
                new string('a',32)+".partial");
            var recent=Path.Combine(root,"DeskSpace_Setup.exe."+
                new string('b',32)+".partial");
            var unrelated=Path.Combine(root,"user-notes.txt");
            File.WriteAllText(stale,"abandoned");
            File.WriteAllText(recent,"still downloading");
            File.WriteAllText(unrelated,"keep");
            File.SetLastWriteTimeUtc(stale,DateTime.UtcNow-TimeSpan.FromHours(24));
            File.SetLastWriteTimeUtc(recent,DateTime.UtcNow);
            cleanup.Invoke(null,new object[]{root,DateTime.UtcNow});
            Assert(!File.Exists(stale)&&File.Exists(recent)&&
                File.ReadAllText(unrelated)=="keep",
                "R222 cleanup removes only old recognizable abandoned partials");
        }
        finally{Directory.Delete(root,true);}
        Console.WriteLine("R222 PASS: successful atomic installer transfer, truncation/cancel preservation and bounded orphan cleanup.");
    }

'@
Swap '    private static void TestUpdateChecksumStagingV219()' ($method+
 '    private static void TestUpdateChecksumStagingV219()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R222 WPF atomic installer and cancellation regression registered.'
