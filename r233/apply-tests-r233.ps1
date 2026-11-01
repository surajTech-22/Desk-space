$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw ("R233 "+$label+": expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestAllWindowsGracefulCloseV230();' @'
                TestAllWindowsGracefulCloseV230();
                TestOneShotGracefulExitV233();
'@ 'register native WPF regression'
$test=@'
    private static void TestOneShotGracefulExitV233()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceGracefulExitV230",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var consume=type.GetMethod("ConsumeSignal",f)!;
        bool Step(bool active)=>(bool)consume.Invoke(null,new object[]{active})!;
        Assert(!Step(false),"R233 inactive request must not trigger close");
        Assert(Step(true),"R233 first supervisor signal must attempt normal close");
        Assert(!Step(true) && !Step(true),
            "R233 held signal must not repeat unsaved-work prompts");
        Assert(!Step(false),"R233 supervisor reset must only re-arm the latch");
        Assert(Step(true) && !Step(true),
            "R233 a separate new update attempt must close once");
        Assert(!Step(false),"R233 latch must reset between separate updates");
        Console.WriteLine("R233 PASS: one normal-close attempt per update signal, no repeated prompts.");
    }

'@
Swap '    private static void TestAllWindowsGracefulCloseV230()' ($test+
'    private static void TestAllWindowsGracefulCloseV230()') 'native signal-latch assertions'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R233 native WPF test registered: veto does not cause repeated prompts during held update signal.'
