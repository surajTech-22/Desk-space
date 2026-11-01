$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why) {
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R285 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$next)
}
Swap '                TestModernDialogAccessibilityV284();' @'
                TestModernDialogAccessibilityV284();
                TestPerMonitorDpiManifestPolicyV285();
'@ 'register PMv2 policy'
$method=@'
    private static void TestPerMonitorDpiManifestPolicyV285()
    {
        var audit=Product.GetType("DeskSpace.DeskSpaceDpiAuditV285",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var label=audit.GetMethod("AwarenessLabel",flags)!;
        string Label(bool v2,bool v1,bool system)=>
            (string)label.Invoke(null,new object[]{v2,v1,system})!;
        Assert(Label(true,true,true)=="PerMonitorV2" &&
            Label(false,true,true)=="PerMonitor" &&
            Label(false,false,true)=="System" &&
            Label(false,false,false)=="Unaware/Unknown",
            "R285 PMv2 awareness must differ from system-scaled mode");
        var report=audit.GetMethod("Capture",flags)!;
        var snapshot=report.Invoke(null,null)!;
        var type=snapshot.GetType();
        var schema=(string)type.GetProperty("Schema")!.GetValue(snapshot)!;
        int count=(int)type.GetProperty("MonitorCount")!.GetValue(snapshot)!;
        Assert(schema=="deskspace-dpi-audit-v285" &&
               count>=0 && count<=64,
            "R285 local monitor report must be bounded and have version schema");
        var monitors=((System.Collections.IEnumerable)
            type.GetProperty("Monitors")!.GetValue(snapshot)!)
            .Cast<object>().ToArray();
        Assert(monitors.Length==count,
            "R285 no monitor may be invented or double counted");
        // UiSmoke.exe lacks DeskSpace's application manifest: only a
        // separate launch of actual DeskSpace.exe proves PMv2 startup.
        Console.WriteLine("R285 PASS: Windows monitor report and DPI "+
            "awareness labels; actual DeskSpace.exe proof is separate.");
    }

'@
Swap '    private static void TestModernDialogAccessibilityV284()' ($method+
 '    private static void TestModernDialogAccessibilityV284()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R285 native DPI policy and monitor report test registered.'
