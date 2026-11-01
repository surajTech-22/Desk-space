$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R211 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestZipIntegrityV210();' @'
                TestZipIntegrityV210();
                TestContentDuplicatesV211();
'@ 'test invocation'
$method=@'
    private static void TestContentDuplicatesV211()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceContentDuplicateV211",true)!;
        var lookup=type.GetMethod("FindMatching",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var temp=Path.Combine(Path.GetTempPath(),
            "deskspace-r211-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var first=Path.Combine(temp,"one.txt");
            var second=Path.Combine(temp,"two.txt");
            var different=Path.Combine(temp,"different.txt");
            File.WriteAllText(first,"DeskSpace unique synthetic sample");
            File.WriteAllText(second,"DeskSpace unique synthetic sample");
            File.WriteAllText(different,"DeskSpace unique synthetic samplX");
            string? Find(string source,string[] candidates,
                System.Threading.CancellationToken token)
            {
                try
                {
                    return (string?)lookup.Invoke(null,new object[]{
                        source,candidates,token});
                }
                catch(TargetInvocationException ex)when(ex.InnerException!=null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                        ex.InnerException).Throw();
                    throw;
                }
            }
            Assert(Find(first,new[]{different,second},
                System.Threading.CancellationToken.None)==second,
                "R211 same contents at a different filename must match by SHA-256");
            Assert(Find(first,new[]{different},
                System.Threading.CancellationToken.None)==null,
                "R211 same-length different bytes must not be treated as duplicates");
            Assert(Find(first,Array.Empty<string>(),
                System.Threading.CancellationToken.None)==null,
                "R211 no held files means no duplicate");
            var emptyA=Path.Combine(temp,"emptyA.bin");
            var emptyB=Path.Combine(temp,"emptyB.bin");
            File.WriteAllBytes(emptyA,Array.Empty<byte>());
            File.WriteAllBytes(emptyB,Array.Empty<byte>());
            Assert(Find(emptyA,new[]{emptyB},
                System.Threading.CancellationToken.None)==emptyB,
                "R211 empty files should match correctly");
            using(var cancelled=new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();
                bool stopped=false;
                try{Find(first,new[]{second},cancelled.Token);}
                catch(OperationCanceledException){stopped=true;}
                Assert(stopped,"R211 fingerprinting must observe cancellation");
            }
            Assert(File.ReadAllText(first)=="DeskSpace unique synthetic sample" &&
                File.ReadAllText(second)=="DeskSpace unique synthetic sample",
                "R211 duplicate checking must never modify original or held bytes");
            var host=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
            Assert(host.GetNestedType("DuplicateChoiceV211",
                BindingFlags.NonPublic)!=null,
                "R211 UI must offer explicit keep/skip decisions");
        }
        finally
        {
            Directory.Delete(temp,true);
        }
        Console.WriteLine("R211 PASS: content fingerprints, mismatch, empty, cancellation and read-only UI state.");
    }

'@
Swap '    private static void TestZipIntegrityV210()' ($method+
 '    private static void TestZipIntegrityV210()') 'test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R211 native SHA-256 duplicate and shared decision smoke tests registered.'
