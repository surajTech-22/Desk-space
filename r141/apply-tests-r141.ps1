$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$f='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($f).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$name){
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R141 $($name) expected one anchor, got $n"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            TestFullExcelFormatMatrixV140();
'@ @'
            TestFullExcelFormatMatrixV140();
            TestExcelProtectionV141();
'@ 'invoke COM rejection scenarios'
Swap @'
    private static void TestExcelTableRenderedPolicyV135()
'@ @'
    private static void TestExcelProtectionV141()
    {
        var policy=Product.GetType("DeskSpace.DeskSpaceExcelProtectionV141",true)!;
        var method=policy.GetMethod("TryChange",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Eval(Func<object?> getter,Action setter,bool desired)=>
            (bool)method.Invoke(null,new object[]{getter,setter,desired})!;
        int writes=0;
        Assert(Eval(()=>true,()=>writes++,true)&&writes==0,
            "Matching Locked metadata must not cause any COM property write.");
        bool changed=false;
        Assert(Eval(()=>true,()=>{changed=true;},false)&&changed,
            "Writable protection metadata should still be copied exactly.");
        Assert(!Eval(()=>true,()=>
            throw new System.Runtime.InteropServices.COMException(
                "Unable to set the Locked property of the Range class."),
            false),"Excel COM refusal should not undo visual formatting.");
        Assert(!Eval(()=>null,()=>writes++,true)&&writes==0,
            "Unreadable Locked metadata must not be overwritten.");
        Assert(!Eval(()=>throw new System.Runtime.InteropServices.COMException(
            "Excel range getter unavailable"),()=>writes++,false)&&writes==0,
            "Protected Excel range readback must not trigger a write.");
        // Programming defects are NOT swallowed like Excel's COM refusal.
        bool threw=false;
        try
        {
            Eval(()=>true,()=>throw new ArgumentException("bad code"),false);
        }
        catch(TargetInvocationException e) when
            (e.InnerException is ArgumentException){threw=true;}
        Assert(threw,"Unexpected non-COM exceptions must not be suppressed.");
        Console.WriteLine("R141 PASS: no redundant Locked writes, writable flags copied, "+
            "COM rejections isolated and preserved, malformed code not suppressed.");
    }

    private static void TestExcelTableRenderedPolicyV135()
'@ 'compiled reflection regression'
[IO.File]::WriteAllText($f,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R141 Windows compiled checks: only necessary Excel protection property writes, restricted COM ranges preserve original protection without rolling back valid formatting.'
