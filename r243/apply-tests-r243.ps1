$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R243 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestExcelFailClosedTypesV242();' @'
                TestExcelFailClosedTypesV242();
                TestTwoMonitorMediaOwnershipV243();
'@ 'register native Deck ownership regression'
$method=@'
    private static void TestTwoMonitorMediaOwnershipV243()
    {
        var deck=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var media=Product.GetType("DeskSpace.DeskSpaceMediaV18",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var integrated=media.GetProperty("DeckIntegrationV123",flags)!;
        var render=deck.GetMethod("Render",BindingFlags.NonPublic|
            BindingFlags.Instance)!;
        var host1=new Window{Width=140,Height=140};
        var host2=new Window{Width=140,Height=140};
        IDisposable? first=null,second=null;
        try
        {
            first=(IDisposable)Activator.CreateInstance(deck,
                new object[]{host1})!;
            second=(IDisposable)Activator.CreateInstance(deck,
                new object[]{host2})!;
            render.Invoke(first,null);
            render.Invoke(second,null);
            Assert((bool)integrated.GetValue(null)!,
                "R243 media controls should be inside the shared Deck");
            first.Dispose();first=null;
            Assert((bool)integrated.GetValue(null)!,
                "R243 closing monitor 1 must not detach media while monitor 2 remains");
            second.Dispose();second=null;
            Assert(!(bool)integrated.GetValue(null)!,
                "R243 close integrated media only after last monitor Deck closes");
            Console.WriteLine(
                "R243 PASS: independently disposed WPF Deck views preserve "+
                "media integration until final widget shuts down.");
        }
        finally
        {
            first?.Dispose();second?.Dispose();
            host1.Close();host2.Close();
        }
    }

'@
Swap '    private static void TestExcelFailClosedTypesV242()' ($method+
 '    private static void TestExcelFailClosedTypesV242()') 'native two-view close test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R243: added WPF two-monitor Deck teardown regression for shared media controls.'
