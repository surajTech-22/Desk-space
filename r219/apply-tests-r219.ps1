$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R219 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestWindowPreviewMonitorV218();' @'
                TestWindowPreviewMonitorV218();
                TestUpdateChecksumStagingV219();
'@ 'native invocation'
$method=@'
    private static void TestUpdateChecksumStagingV219()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceVerifiedUpdateQueueV191",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var normalize=type.GetMethod("NormalizeOfficialChecksumV219",f)!;
        var stage=type.GetMethod("Stage",f)!;
        var peek=type.GetMethod("Peek",f)!;
        var clear=type.GetMethod("Clear",f)!;
        var still=type.GetMethod("IsStillVerified",f)!;
        string hash=new string('A',64);
        string Normalize(string text)
        {
            try{return (string)normalize.Invoke(null,new object[]{text})!;}
            catch(TargetInvocationException e) when(e.InnerException!=null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(e.InnerException).Throw();
                throw;
            }
        }
        void Reject(string text)
        {
            bool blocked=false;
            try{Normalize(text);}
            catch(InvalidDataException){blocked=true;}
            Assert(blocked,"R219 reject untrusted SHA-256 line: "+
                text.Replace("\r","\\r").Replace("\n","\\n"));
        }
        Assert(Normalize(hash)==hash &&
            Normalize(hash.ToLowerInvariant())==hash &&
            Normalize(hash+"  DeskSpace_Setup.exe\r\n")==hash &&
            Normalize(hash+" *DeskSpace_Setup.exe\n")==hash &&
            Normalize(hash+"  deskspace_setup.EXE")==hash,
            "R219 official setup checksum with filename must normalize to SHA256");
        Reject(hash+"  Other.exe");
        Reject(hash+"  DeskSpace_Setup.exe\n"+hash);
        Reject(hash+"  DeskSpace_Setup.exe unexpected");
        Reject("sha256:"+hash);
        Reject(hash[..63]+"Z  DeskSpace_Setup.exe");
        Reject("");
        var root=Path.Combine(Path.GetTempPath(),"deskspace-r219-"+
            Guid.NewGuid().ToString("N"));
        const string tag="v1.5.219";
        var folder=Path.Combine(root,tag);
        var installer=Path.Combine(folder,"DeskSpace_Setup.exe");
        Directory.CreateDirectory(folder);
        try
        {
            // Synthetic PE-like file, never executable or launched. Large
            // enough to exercise R191's real five-megabyte revalidation gate.
            using(var output=new FileStream(installer,FileMode.CreateNew,
                FileAccess.Write,FileShare.None))
            {
                output.SetLength(5_000_128);
                output.Position=0; output.WriteByte(0x4d);output.WriteByte(0x5a);
            }
            string actual=Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(installer)));
            string published=actual.ToLowerInvariant()+"  DeskSpace_Setup.exe\r\n";
            stage.Invoke(null,new object[]{
                installer,folder,tag,published,"stable"});
            var queued=peek.Invoke(null,new object[]{"stable"});
            Assert(queued!=null &&
                (string)queued.GetType().GetProperty("Sha256")!.GetValue(queued)! ==
                actual,
                "R219 queue must save canonical bare SHA256 after official filename record");
            Assert((bool)still.Invoke(null,new object[]{queued!})!,
                "R219 deferred verified installer must pass SHA256 and PE checks");
            using(var output=new FileStream(installer,FileMode.Open,
                FileAccess.Write,FileShare.None))
            {output.Position=64;output.WriteByte(1);}
            Assert(!(bool)still.Invoke(null,new object[]{queued!})!,
                "R219 must reject any changed cached installer");
            clear.Invoke(null,new object[]{queued!});
            Assert(peek.Invoke(null,new object[]{"stable"})==null,
                "R219 must clear invalidated staging after verification failure");
            bool rejected=false;
            try{stage.Invoke(null,new object[]{
                Path.Combine(folder,"untrusted.exe"),folder,tag,published,"stable"});}
            catch(TargetInvocationException ex)
            {rejected=ex.InnerException is InvalidDataException;}
            Assert(rejected,"R219 must still reject arbitrary executable staging paths");
            Console.WriteLine(
                "R219 PASS: official checksum line, real staged-queue digest, cached integrity and invalid-file rejection.");
        }
        finally{Directory.Delete(root,true);}
    }

'@
Swap '    private static void TestWindowPreviewMonitorV218()' ($method+
 '    private static void TestWindowPreviewMonitorV218()') 'native end-to-end checksum staging test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R219: native Windows official sha256sum/queue/reverify/mutation regression inserted.'
