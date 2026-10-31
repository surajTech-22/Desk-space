$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R227 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestCompactDeckHeaderV226();' @'
                TestCompactDeckHeaderV226();
                TestSupervisorLoopGuardV227();
'@ 'native invocation'
$method=@'
    private static void TestSupervisorLoopGuardV227()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceReleaseAttemptV227",true)!;
        var policy=type.GetMethod("WasRecentlyAttempted",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var wait=type.GetField("ReplayWindow",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var window=(TimeSpan)wait.GetValue(null)!;
        Assert(window>=TimeSpan.FromMinutes(2) &&
            window<=TimeSpan.FromMinutes(15),
            "R227 release hints must be bounded, without indefinite suppression");
        var now=new DateTime(2026,10,30,9,50,0,DateTimeKind.Utc);
        bool Skip(string prev,DateTime last,string current,DateTime at)=>
            (bool)policy.Invoke(null,new object[]{prev,last,current,at})!;
        Assert(Skip("tester-v1.5.1097",now.AddSeconds(-4),
            "tester-v1.5.1097",now) &&
            Skip("v1.5.1097",now.AddMinutes(-7),"v1.5.1097",now) &&
            !Skip("tester-v1.5.1096",now.AddMinutes(-1),
                "tester-v1.5.1097",now) &&
            !Skip("tester-v1.5.1097",now.AddMinutes(-10),
                "tester-v1.5.1097",now) &&
            !Skip("tester-v1.5.1097",now.AddSeconds(1),
                "tester-v1.5.1097",now),
            "R227 must handle same-tag retry, newer release, expiry and clock skew");
        Assert(type.GetMethod("ShouldDispatch",
            BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "R227 cross-process persistent release replay guard absent");
        var updater=Product.GetType("DeskSpace.DeskSpacePublicUpdaterV44",true)!;
        Assert(updater.GetMethod("CheckAsync",BindingFlags.Static|
            BindingFlags.Public)!=null,
            "R227 manual and scheduled canonical GitHub update checks must remain");
        Console.WriteLine(
            "R227 PASS: cross-process release hint guard, bounded suppression and official updater preserved.");
    }

'@
Swap '    private static void TestCompactDeckHeaderV226()' ($method+
 '    private static void TestCompactDeckHeaderV226()') 'native test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R227 native release-event loop regression registered.'
