$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestExcelBackupReceiptV252();'
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R253 native call anchor expected 1, got "+$n)}
$s=$s.Replace($a,$a+$lf+'                TestResourceBudgetV253();')
$anchor='    private static void TestExcelBackupReceiptV252()'
$method=@'
    private static void TestResourceBudgetV253()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceResourceBudgetV253",true)!;
        var evaluate=type.GetMethod("Evaluate",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string Check(long bytes,int handles,int threads)=>
            (string)evaluate.Invoke(null,new object[]{bytes,handles,threads})!;
        Assert(Check(500L*1024*1024,300,34).Contains("Within advisory budget"),
            "R253 modest resource use remains healthy");
        Assert(Check(1024L*1024*1024+1,300,34)
            .Contains("memory above 1 GiB"),
            "R253 private memory threshold warning");
        Assert(Check(500L*1024*1024,5001,34).Contains("handles above 5000"),
            "R253 handle count threshold warning");
        Assert(Check(500L*1024*1024,300,257).Contains("threads above 256"),
            "R253 thread count threshold warning");
        Assert(Check(-1,0,0).Contains("unavailable") &&
               Check(0,-1,0).Contains("unavailable") &&
               Check(0,0,-1).Contains("unavailable"),
            "R253 invalid process metrics must not look healthy");
        Assert(type.GetMethod("Current",BindingFlags.NonPublic|
            BindingFlags.Static)!=null,
            "R253 on-demand process counter entry must remain available");
        Console.WriteLine(
            "R253 PASS: safe advisory resource budget, threshold boundaries, "+
            "invalid counters and on-demand process inspector.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R253 native method anchor expected 1, got "+$n)}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R253: native Windows runtime resource budget policy test registered.'
