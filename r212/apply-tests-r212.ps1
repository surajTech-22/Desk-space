$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R212 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestContentDuplicatesV211();' @'
                TestContentDuplicatesV211();
                TestCopyIntegrityV212();
'@ 'native invocation'
$method=@'
    private static void TestCopyIntegrityV212()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceCopyIntegrityV212",true)!;
        var verify=type.GetMethod("VerifyWritten",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        string temp=Path.Combine(Path.GetTempPath(),
            "deskspace-r212-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string staged=Path.Combine(temp,"staged.partial");
        var content=System.Text.Encoding.UTF8.GetBytes(
            "DeskSpace synthetic copied payload");
        var hash=System.Security.Cryptography.SHA256.HashData(content);
        void Verify(long length,byte[] digest,
            System.Threading.CancellationToken token)
        {
            try
            {
                verify.Invoke(null,new object[]{staged,length,digest,token});
            }
            catch(TargetInvocationException ex)when(ex.InnerException!=null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                    ex.InnerException).Throw();
                throw;
            }
        }
        try
        {
            File.WriteAllBytes(staged,content);
            Verify(content.Length,hash,System.Threading.CancellationToken.None);
            var changed=(byte[])content.Clone();
            changed[0]^=0x01;
            File.WriteAllBytes(staged,changed);
            bool badSha=false;
            try{Verify(content.Length,hash,System.Threading.CancellationToken.None);}
            catch(InvalidDataException){badSha=true;}
            Assert(badSha,"R212 must reject same-length staged corruption");
            bool wrongSize=false;
            try{Verify(content.Length-1,hash,System.Threading.CancellationToken.None);}
            catch(InvalidDataException){wrongSize=true;}
            Assert(wrongSize,"R212 must reject staged growth");
            File.WriteAllBytes(staged,Array.Empty<byte>());
            Verify(0,System.Security.Cryptography.SHA256.HashData(
                Array.Empty<byte>()),System.Threading.CancellationToken.None);
            using(var cancelled=new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();
                bool stopped=false;
                try{Verify(0,System.Security.Cryptography.SHA256.HashData(
                    Array.Empty<byte>()),cancelled.Token);}
                catch(OperationCanceledException){stopped=true;}
                Assert(stopped,"R212 readback must observe cancellation");
            }
            var copy=Product.GetType("DeskSpace.DeskSpaceFolderCopyV164",true)!;
            Assert(copy.GetMethod("Copy",BindingFlags.Static|
                BindingFlags.NonPublic)!=null,
                "R212 must integrate with existing shared Deck copy service");
        }
        finally{Directory.Delete(temp,true);}
        Console.WriteLine("R212 PASS: staged-byte SHA-256 readback, size mismatch, empty payload and cancellation.");
    }

'@
Swap '    private static void TestContentDuplicatesV211()' ($method+
 '    private static void TestContentDuplicatesV211()') 'native test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R212: native staging readback verification suite registered.'
