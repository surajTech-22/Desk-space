$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                TestKeyboardShortcutsGuideV246();'
$new='                TestKeyboardShortcutsGuideV246();'+$lf+'                TestMonitorResumePolicyV247();'
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R247 invocation expected 1, got "+$n)}
$s=$s.Replace($old,$new)
$anchor='    private static void TestKeyboardShortcutsGuideV246()'
$method=@'
    private static void TestMonitorResumePolicyV247()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceMultiMonitorV151",true)!;
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var policy=type.GetMethod("ResumeDelayForAttemptV247",flags)!;
        int Delay(int index)=>(int)policy.Invoke(null,new object[]{index})!;
        Assert(Delay(0)==1500 && Delay(1)==5000 && Delay(2)==12000 &&
               Delay(-1)==-1 && Delay(3)==-1 && Delay(500)==-1,
            "R247 wake retries must be finite, bounded and ignore invalid attempts");
        Assert(type.GetMethod("OnPowerModeV247",flags)!=null &&
               type.GetField("resumeTimerV247",flags)!=null,
            "R247 event-driven Windows PowerModes.Resume handler is registered");
        var multi=type.GetMethod("Reconcile",flags);
        Assert(multi!=null,
            "R247 wake handler must use existing monitor reconcile, not reset positions");
        Console.WriteLine(
            "R247 PASS: finite Windows resume retries at 1.5s/5s/12s; "+
            "event hookup, original monitor reconciliation and invalid-index guards.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R247 native method anchor expected 1, got "+$n)}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R247 Windows native WPF bounded resume acceptance registered.'
