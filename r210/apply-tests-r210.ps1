$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$reason)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R210 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestReducedMotionV209();' @'
                TestReducedMotionV209();
                TestZipIntegrityV210();
'@ 'native invocation'
$method=@'
    private static void TestZipIntegrityV210()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceZipIntegrityV210",true)!;
        var matches=type.GetMethod("ContentMatches",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var input=System.Text.Encoding.UTF8.GetBytes("DeskSpace synthetic test input");
        var sha=System.Security.Cryptography.SHA256.HashData(input);
        bool Match(byte[] value,long claimedSize,byte[] expected,
            System.Threading.CancellationToken cancellation)
        {
            using var decoded=new MemoryStream(value);
            try
            {
                return (bool)matches.Invoke(null,
                    new object[]{decoded,claimedSize,expected,cancellation})!;
            }
            catch(TargetInvocationException ex)when(ex.InnerException!=null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                    ex.InnerException).Throw();
                throw;
            }
        }
        Assert(Match(input,input.Length,sha,System.Threading.CancellationToken.None),
            "R210 exact extracted payload and SHA-256 must verify");
        var changed=(byte[])input.Clone();
        changed[0]^=0x01;
        Assert(!Match(changed,input.Length,sha,System.Threading.CancellationToken.None),
            "R210 same-length corrupted payload must be rejected");
        Assert(!Match(input,input.Length+1,sha,System.Threading.CancellationToken.None),
            "R210 mismatched uncompressed length must be rejected");
        Assert(Match(Array.Empty<byte>(),0,
            System.Security.Cryptography.SHA256.HashData(Array.Empty<byte>()),
            System.Threading.CancellationToken.None),
            "R210 empty files must remain valid");
        using(var cancelled=new System.Threading.CancellationTokenSource())
        {
            cancelled.Cancel();
            bool rejected=false;
            try{Match(input,input.Length,sha,cancelled.Token);}
            catch(OperationCanceledException){rejected=true;}
            Assert(rejected,"R210 archive verification must honour cancellation");
        }
        Console.WriteLine(
            "R210 PASS: SHA-256 ZIP byte readback, same-length corruption, empty files and cancellation.");
    }

'@
Swap '    private static void TestReducedMotionV209()' ($method+
 '    private static void TestReducedMotionV209()') 'native test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R210 native integrity verification and cancellation regressions registered.'
