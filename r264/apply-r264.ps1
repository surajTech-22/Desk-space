$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r264/DeskSpacePinnedRecentMonitorV264.cs' -Destination "$root/DeskSpacePinnedRecentMonitorV264.cs" -Force
$p="$root/DeskSpacePinnedRecentPickerV262.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
            WindowStartupLocation=owner.IsVisible?
                WindowStartupLocation.CenterOwner:
                WindowStartupLocation.CenterScreen};
'@
$new=@'
            // CenterOwner and CenterScreen use unconstrained WPF placement;
            // the attached monitor fence positions in physical pixels.
            WindowStartupLocation=WindowStartupLocation.Manual};
        DeskSpacePinnedRecentMonitorV264.Attach(window,owner);
'@
$old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R264 pinned document popup anchor missing or duplicated: $n"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R264: pinned document picker belongs to its originating monitor, without primary-screen/DIP positioning.'
