$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r179/DeskSpaceWindowLayoutsV179.cs' "$root/DeskSpaceWindowLayoutsV179.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                DeskSpaceClipboardTransformsV177.Show(this))));'
$new=@'
                DeskSpaceClipboardTransformsV177.Show(this))));
        menu.Items.Add("Window layout profiles...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceWindowLayoutsV179.Show(this))));
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R179 tray integration expected 1 anchor, found $n"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R179: user-initiated window-layout capture/restore from existing widget tray; no background movement.'
