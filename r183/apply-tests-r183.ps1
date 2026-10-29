$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10
$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$from,[string]$to,[string]$name)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($count -ne 1){throw ("R183 "+$name+": expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($from,$to)
}
One '            TestRestorePreviewV182();' @'
            TestRestorePreviewV182();
            TestStartupRecoveryV183();
'@ 'new regression invocation'
$method=@'
    private static void TestStartupRecoveryV183()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceStartupRecoveryV183",true)!;
        var state=type.GetNestedType("State",BindingFlags.NonPublic)!;
        var next=type.GetMethod("NextStart",BindingFlags.NonPublic|BindingFlags.Static)!;
        var time=new DateTime(2026,10,29,10,0,0,DateTimeKind.Utc);
        object Start(object? previous,DateTime at)=>
            next.Invoke(null,new object?[]{previous,at})!;
        int Failures(object value)=>(int)state.GetProperty("ShortFailures")!.
            GetValue(value)!;
        bool Safe(object value)=>(bool)state.GetProperty("SafeMode")!.
            GetValue(value)!;
        var one=Start(null,time);
        var two=Start(one,time.AddSeconds(5));
        var three=Start(two,time.AddSeconds(10));
        var four=Start(three,time.AddSeconds(15));
        Assert(Failures(one)==0&&Failures(two)==1&&Failures(three)==2&&
            Failures(four)==3&&Safe(four),
            "R183 fourth rapid start after three failures must enter recovery");
        var preserved=Start(four,time.AddSeconds(20));
        Assert(Safe(preserved),"R183 recovery remains sticky until user resets it");
        var clean=Activator.CreateInstance(state,new object[]{
            time,0,true,true})!;
        Assert(Safe(Start(clean,time.AddMinutes(1))),
            "R183 a clean exit must never silently exit recovery mode");
        var late=Start(one,time.AddMinutes(11));
        Assert(Failures(late)==0&&!Safe(late),
            "R183 an old startup session cannot trigger a false crash loop");
        var healthy=Activator.CreateInstance(state,new object[]{
            time,2,true,false})!;
        Assert(Failures(Start(healthy,time.AddSeconds(20)))==0,
            "R183 a healthy previous startup resets the short-crash count");
        var health=Product.GetType("DeskSpace.DeskSpaceComponentHealthV175",true)!;
        var snapshot=health.GetMethod("Snapshot",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var widget=new Window{Width=120,Height=120,ShowInTaskbar=false};
        try
        {
            widget.Show();
            var rows=((System.Collections.IEnumerable)snapshot.Invoke(
                null,new object[]{widget,false})!).Cast<object>().ToArray();
            Assert(rows.Any(x=>
                (string)x.GetType().GetProperty("Name")!.GetValue(x)!==
                    "Startup recovery"),
                "R183 recovery state must be visible in local component status");
        }
        finally{widget.Close();}
        Console.WriteLine("R183 PASS: 3 short-failure threshold, sticky safe "+
            "startup, healthy/expired reset and visible local status.");
    }

'@
One '    private static void TestIndependentMonitorsV154()' ($method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()') 'WPF test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R183: WPF recovery transition, persisted-safe-mode logic and health report tests registered.'
