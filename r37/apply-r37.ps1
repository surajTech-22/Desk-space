$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
# R36 has already assembled the existing Windows media player. Replace only
# that media source; do not touch the vault, workspaces or other modules.
if(!(Test-Path "$root/DeskSpaceMediaV18.cs")){throw 'Existing media source missing'}
Copy-Item 'r37/DeskSpaceMediaV37.cs' "$root/DeskSpaceMediaV18.cs" -Force

# Exercise the actual WPF controls in the existing Windows UI smoke test.
$smokePath='r36/UiSmoke/Program.cs'
$smoke=[IO.File]::ReadAllText($smokePath)
$anchor='            var buttons=Walk(frame).OfType<Button>().Select(b=>b.ToolTip?.ToString())'
if(!$smoke.Contains($anchor)){throw 'WPF smoke insertion anchor missing'}
$extra=@'
            var muteButtons=Walk(frame).OfType<Button>()
                .Where(b=>(b.ToolTip?.ToString()??"")=="Mute or unmute system sound")
                .ToArray();
            Assert(muteButtons.Length>=3,
                "System mute must be accessible in compact, minimized and advanced media views");
'@
$smoke=$smoke.Replace($anchor,$extra+$anchor)
[IO.File]::WriteAllText($smokePath,$smoke,[Text.UTF8Encoding]::new($false))
Write-Host 'R37: restore existing Windows system mute on compact transport and minimized media pill; WPF smoke assertion installed.'
