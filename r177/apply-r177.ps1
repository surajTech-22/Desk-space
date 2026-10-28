$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r177/DeskSpaceClipboardTransformsV177.cs' "$root/DeskSpaceClipboardTransformsV177.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                DeskSpaceComponentHealthV175.Show(this,tray.Visible))));'
$new=@'
                DeskSpaceComponentHealthV175.Show(this,tray.Visible))));
        menu.Items.Add("Clipboard: convert text / Markdown / JSON...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceClipboardTransformsV177.Show(this))));
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R177 expected one tray menu anchor, got $n"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R177: opt-in clipboard transform tool added to existing tray. No background reads.'
