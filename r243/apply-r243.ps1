$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R243 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
        mediaDeckTimerV123.Stop();
        DeskSpaceMediaV18.DeckIntegrationV123=false;
'@ @'
        mediaDeckTimerV123.Stop();
        // R243: no per-widget global reset here. Another monitor may
        // still have its own active Deck. Recompute after Views.Remove.
'@ 'remove destructive global media reset on one monitor shutdown'
Swap @'
        Views.Remove(this);
'@ @'
        Views.Remove(this);
        // The media player stays inside the Deck until the FINAL widget
        // goes away. Closing monitor 1 must not open an extra floating
        // player over monitor 2. Preserve other teardown hooks.
        DeskSpaceMediaV18.DeckIntegrationV123=Views.Count>0;
'@ 'share media integration ownership across independently disposed monitor Deck views'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R243: closing a secondary or primary Deck retains media integration while another widget survives.'
