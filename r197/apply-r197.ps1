$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r197/DeskSpaceActionPaletteV197.cs' -Destination "$root/DeskSpaceActionPaletteV197.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                DeskSpaceWindowLayoutsV179.Show(this))));'
$new=@'
                DeskSpaceWindowLayoutsV179.Show(this))));
        menu.Items.Add("Action palette (Ctrl+K in widget)...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceActionPaletteV197.Show(this,tray.Visible))));
        // Focused-widget shortcut only; never a global hotkey that can
        // interfere with Excel or another application on a different monitor.
        this.PreviewKeyDown+=(_,e)=>{
            if(e.Key==System.Windows.Input.Key.K &&
               (System.Windows.Input.Keyboard.Modifiers &
                System.Windows.Input.ModifierKeys.Control)!=0)
            {
                DeskSpaceActionPaletteV197.Show(this,tray.Visible);
                e.Handled=true;
            }
        };
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R197 existing tray menu anchor expected 1, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R197: safe local Command Palette from tray or Ctrl+K in focused widget. Shared actions require explicit selection.'
