$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R224 "+$label+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestInstallerAtomicV222();' @'
                TestInstallerAtomicV222();
                TestPostInstallStabilityV224();
'@ 'test invocation'
$method=@'
    private static void TestPostInstallStabilityV224()
    {
        var receipt=Product.GetType("DeskSpace.DeskSpacePostInstallReceiptV185",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var wait=receipt.GetField("StableIntervalV224",f)!;
        var interval=(TimeSpan)wait.GetValue(null)!;
        Assert(interval>=TimeSpan.FromSeconds(8) &&
            interval<=TimeSpan.FromSeconds(30),
            "R224 postinstall WPF receipt must wait for a bounded healthy window");
        var guarded=receipt.GetMethod("ReadyAfterStabilityV224",f);
        var existing=receipt.GetMethod("Ready",f);
        Assert(guarded!=null && existing!=null &&
            guarded.ReturnType==typeof(void) &&
            guarded.GetParameters().Length==1 &&
            guarded.GetParameters()[0].ParameterType.FullName==
                "DeskSpace.WidgetShellWindow",
            "R224 must connect a delayed primary-only receipt without removing actual Ready");
        var scope=Product.GetType("DeskSpace.DeskSpacePostInstallReceiptV185",true)!;
        var ack=scope.GetMethod("ShouldAcknowledge",f)!;
        var pending=scope.GetNestedType("Pending",BindingFlags.NonPublic)!;
        var valid=Activator.CreateInstance(pending,
            "22222222222242228222222222222222","1.5.1096")!;
        Assert((bool)ack.Invoke(null,new object?[]{valid,"1.5.1096"})! &&
            !(bool)ack.Invoke(null,new object?[]{valid,"1.5.1093"})!,
            "R224 sustained receipt must still require the exact installed version");
        Console.WriteLine("R224 PASS: bounded WPF stability delay, exact-version receipt and primary-only supervisor flow.");
    }

'@
Swap '    private static void TestInstallerAtomicV222()' ($method+
 '    private static void TestInstallerAtomicV222()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R224 native bounded postinstall readiness acceptance registered.'
