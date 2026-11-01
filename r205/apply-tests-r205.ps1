$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                TestWorkflowDryRunV203();'
$rep=$old+$lf+'                TestResourceSnapshotV205();'
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R205 isolated native invocation anchor missing'
}
$s=$s.Replace($old,$rep)
$old='    private static void TestWorkflowDryRunV203()'
$method=@'
    private static void TestResourceSnapshotV205()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceResourceSnapshotV205",true)!;
        var format=type.GetMethod("Format",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Measure(TimeSpan cpu,TimeSpan age,int cores,int handles,long bytes)=>
            (string)format.Invoke(null,new object[]{cpu,age,cores,handles,bytes})!;
        var result=Measure(TimeSpan.FromSeconds(4),TimeSpan.FromSeconds(8),2,42,
            2097152);
        Assert(result.Contains("Lifetime CPU mean 25.0%") &&
            result.Contains("42 handles") && result.Contains("2.0 MiB") &&
            Measure(TimeSpan.Zero,TimeSpan.Zero,4,0,0)==
                "Resource counters unavailable",
            "R205 resource snapshot must be bounded and identify lifetime average");
        Console.WriteLine("R205 PASS: on-demand CPU/handle/managed-memory snapshots.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R205 isolated test method anchor missing'
}
[IO.File]::WriteAllText($p,$s.Replace($old,$method.Replace($crlf,$lf)+$old),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R205 native counter formatting/invalid-value checks registered.'
