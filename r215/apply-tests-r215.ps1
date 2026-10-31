$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R215 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestMonitorTopologyV214();' @'
                TestMonitorTopologyV214();
                TestMonitorIdleGuardV215();
'@ 'native invocation'
$method=@'
    private static void TestMonitorIdleGuardV215()
    {
        var monitor=Product.GetType("DeskSpace.DeskSpaceMonitorSessionsV154",true)!;
        var policy=monitor.GetMethod("ShouldGuardV215",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Enabled(bool visible,System.Windows.WindowState state)=>
            (bool)policy.Invoke(null,new object[]{visible,state})!;
        Assert(Enabled(true,System.Windows.WindowState.Normal) &&
            Enabled(true,System.Windows.WindowState.Maximized) &&
            !Enabled(false,System.Windows.WindowState.Normal) &&
            !Enabled(false,System.Windows.WindowState.Maximized) &&
            !Enabled(true,System.Windows.WindowState.Minimized) &&
            !Enabled(false,System.Windows.WindowState.Minimized),
            "R215 guard must pause when hidden/minimized and resume when visible");
        Assert(monitor.GetMethod("GuardOwnedWindows",
            BindingFlags.Static|BindingFlags.NonPublic)!=null &&
            monitor.GetMethod("Attach",BindingFlags.Static|
                BindingFlags.NonPublic)!=null,
            "R215 must preserve existing per-widget popup containment owner");
        Console.WriteLine(
            "R215 PASS: monitor HWND scanner visibility/minimized policy and existing owned-window containment.");
    }

'@
Swap '    private static void TestMonitorTopologyV214()' ($method+
 '    private static void TestMonitorTopologyV214()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R215 native monitor visibility and minimized-state tests registered.'
