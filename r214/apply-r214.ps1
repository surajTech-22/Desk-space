$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$reason)
{
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($s,[regex]::Escape($old))).Count
    if($count -ne 1){throw ("R214 "+$reason+" expected exactly one anchor, got "+$count)}
    [IO.File]::WriteAllText($path,$s.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
$fence="$root/DeskSpaceMonitorFenceV152.cs"
Swap $fence @'
    internal static void Bind(WidgetShellWindow window,string screenName)
'@ @'
    // Pure topology resolution: temporary disconnection never changes the
    // desired physical display identity. The originally assigned monitor
    // becomes active again immediately when Windows reports its return.
    internal static string ResolveActiveDeviceV214(string desired,
        IReadOnlyList<string> connected,string primary)
    {
        if(connected.Count==0)return string.Empty;
        if(!string.IsNullOrWhiteSpace(desired))
        {
            var original=connected.FirstOrDefault(x=>
                string.Equals(x,desired,StringComparison.OrdinalIgnoreCase));
            if(original!=null)return original;
        }
        var fallback=connected.FirstOrDefault(x=>
            string.Equals(x,primary,StringComparison.OrdinalIgnoreCase));
        return fallback??connected[0];
    }

    internal static void Bind(WidgetShellWindow window,string screenName)
'@ 'reconnect-safe screen resolver'
Swap $fence @'
        if(Assigned.TryGetValue(window,out var name))
        {
            var bound=screens.FirstOrDefault(s=>string.Equals(s.DeviceName,name,
                StringComparison.OrdinalIgnoreCase));
            if(bound!=null)return bound;
        }
        // Monitor disconnected: re-home the original widget, not the entire app.
        var fallback=Forms.Screen.PrimaryScreen??screens[0];
        Assigned[window]=fallback.DeviceName;
        return fallback;
'@ @'
        // Never rewrite Assigned during a temporary disconnect. Keep the
        // user's intended display identity so reconnect can restore it.
        var fallback=Forms.Screen.PrimaryScreen??screens[0];
        var desired=Assigned.TryGetValue(window,out var assigned)?
            assigned:fallback.DeviceName;
        var active=ResolveActiveDeviceV214(desired,
            screens.Select(s=>s.DeviceName).ToArray(),fallback.DeviceName);
        return screens.FirstOrDefault(s=>string.Equals(
            s.DeviceName,active,StringComparison.OrdinalIgnoreCase))??fallback;
'@ 'preserve original monitor identity through disconnect'
$multi="$root/DeskSpaceMultiMonitorV151.cs"
Swap $multi @'
            DeskSpaceMonitorSessionsV154.Attach(primary,original.DeviceName,true);
            var wanted=enabled && screens.Length>1?
'@ @'
            DeskSpaceMonitorSessionsV154.Attach(primary,original.DeviceName,true);
            // Reconcile is also triggered by DisplaySettingsChanged: move a
            // reconnected primary back inside its originally assigned monitor.
            // No cross-monitor UI state or shared Deck contents are altered.
            DeskSpaceMonitorFenceV152.ClampNow(primary);
            var wanted=enabled && screens.Length>1?
'@ 'actively reconcile display placement on topology change'
Swap $multi @'
                        Place(replica,screen);
                        replica.Show();
                        // Reposition in native pixels AFTER WPF creates the HWND.
'@ @'
                        Place(replica,screen);
                        // Reposition in native pixels AFTER WPF creates the HWND.
'@ 'prevent premature satellite first paint'
Write-Host 'R214: single satellite Show only after HWND monitor binding, reconnect-safe original device ownership and topology re-clamping.'
