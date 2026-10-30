$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='            TestSearchSmartActionsV193();'
$new='            TestSearchSmartActionsV193();'+$lf+'            TestComponentSignalsV194();'
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R194 native smoke call anchor "+$n)}
$s=$s.Replace($old,$new)
$old='    private static void TestSearchSmartActionsV193()'
$method=@'
    private static void TestComponentSignalsV194()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceHealthSignalsV194",true)!;
        var search=type.GetMethod("Search",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var memory=type.GetMethod("ProcessMemory",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Scan(bool indexing,string coverage)=>(string)search.Invoke(
            null,new object?[]{indexing,coverage})!;
        string Mem(long bytes)=>(string)memory.Invoke(null,new object[]{bytes})!;
        Assert(Scan(true,"C: 100 …").StartsWith("Index warming") &&
            Scan(false,"C: 100 ✓").Contains("scan complete") &&
            Scan(false,"").Contains("No indexed volumes") &&
            Scan(false,new string('x',2000)).Length<290,
            "R194 on-demand Search status must use bounded existing index counters");
        Assert(Mem(104857600).Contains("100 MiB") &&
            Mem(-1)=="Working memory unavailable",
            "R194 resource health must format non-sensitive working set only");
        var component=Product.GetType("DeskSpace.DeskSpaceComponentHealthV175",true)!;
        var snapshot=component.GetMethod("Snapshot",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var window=new System.Windows.Window{Width=120,Height=120,ShowInTaskbar=false};
        try
        {
            window.Show();
            var rows=((System.Collections.IEnumerable)snapshot.Invoke(
                null,new object[]{window,true})!).Cast<object>().ToArray();
            Assert(rows.Any(x=>(string)x.GetType().GetProperty("Name")!
                .GetValue(x)! =="File search") &&
                rows.Any(x=>(string)x.GetType().GetProperty("Name")!
                .GetValue(x)! =="Process resources"),
                "R194 actual WPF health panel must expose search and memory statuses");
        }
        finally{window.Close();}
        Console.WriteLine("R194 PASS: bounded local search progress, memory counters and native status panel.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R194 test method anchor "+$n)}
$s=$s.Replace($old,$method.Replace($crlf,$lf)+$old)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R194 native WPF on-demand status regression registered.'
