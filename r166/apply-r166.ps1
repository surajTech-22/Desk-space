$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r166/DeskSpaceTrayRecoveryV166.cs' "$root/DeskSpaceTrayRecoveryV166.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='            attachedShelf.Enable();'
$new=@'
            attachedShelf.Enable();
            // Attach to the existing primary icon when the widget's
            // startup handler enables the Deck; this is already on Loaded.
            // Keep a single tray icon, regardless of monitor count.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                _=DeskSpaceTrayRecoveryV166.Watch(this,tray);
'@.Replace($crlf,$lf).TrimEnd()
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw "R166 primary host insertion requires 1 anchor, found $count"}
$s=$s.Replace($old,$new)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R166: primary tray recovers after Explorer/taskbar restart, even while WPF widget is hidden; no duplicate notify icons.'
