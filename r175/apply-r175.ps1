$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r175/DeskSpaceComponentHealthV175.cs' "$root/DeskSpaceComponentHealthV175.cs" -Force
$p="$root/WidgetHost.cs"
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
$old='        DeskSpaceDiagnosticsV161.Install();'
$new=@'
        DeskSpaceDiagnosticsV161.Install();
        // The primary widget alone subscribes to OS clipboard-change
        // notifications. No clipboard content is read, stored or uploaded.
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            _=DeskSpaceClipboardSignalV175.Watch(this);
        menu.Items.Add("Component status (local only)...",null,(_,_)=>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceComponentHealthV175.Show(this,tray.Visible))));
'@.Replace($crlf,$nl).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R175 expected one diagnostic tray anchor, found $n"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R175: on-demand status from existing tray; primary-only content-blind clipboard event listener.'
