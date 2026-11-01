$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R216 "+$reason+" expected one anchor, found "+$count)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestMonitorIdleGuardV215();' @'
                TestMonitorIdleGuardV215();
                TestEventDrivenDisplaysV216();
'@ 'test invocation'
$method=@'
    private static void TestEventDrivenDisplaysV216()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceMultiMonitorV151",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var recovery=type.GetField("ReconcileFallbackV216",flags)!;
        var interval=(TimeSpan)recovery.GetValue(null)!;
        Assert(interval>=TimeSpan.FromSeconds(30),
            "R216 monitor recovery should not constantly poll Windows displays");
        foreach(var name in new[]{"StartPreferenceWatcherV216",
            "SchedulePreferenceRefreshV216","StopPreferenceWatcherV216"})
            Assert(type.GetMethod(name,flags)!=null,
                "R216 watcher lifecycle is missing "+name);
        var monitor=type.GetField("preferenceWatcherV216",flags);
        var debounce=type.GetField("preferenceDebounceV216",flags);
        Assert(monitor?.FieldType==typeof(System.IO.FileSystemWatcher) &&
            debounce?.FieldType==typeof(System.Windows.Threading.DispatcherTimer),
            "R216 must use per-process filesystem events and dispatcher debounce");
        Assert(type.GetMethod("Register",flags)!=null &&
            type.GetMethod("Configure",flags)!=null,
            "R216 must preserve existing widget display configuration");
        Console.WriteLine(
            "R216 PASS: display preference event watcher, bounded debounce and slow recovery fallback.");
    }

'@
Swap '    private static void TestMonitorIdleGuardV215()' ($method+
 '    private static void TestMonitorIdleGuardV215()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R216 native WPF watcher/recovery lifecycle acceptance registered.'
