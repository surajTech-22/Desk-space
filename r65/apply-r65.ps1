$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r65/DeskSpaceDonutHomeV65.cs' (Join-Path $root 'DeskSpaceDonutHomeV65.cs') -Force
Copy-Item 'r65/DeskSpaceApplicationIconsV65.cs' (Join-Path $root 'DeskSpaceApplicationIconsV65.cs') -Force
Copy-Item 'r62/DeskSpaceSubDonutStandardV62.cs' (Join-Path $root 'DeskSpaceSubDonutStandardV62.cs') -Force
Copy-Item 'r55/DeskSpaceApplicationsV55.cs' (Join-Path $root 'DeskSpaceApplicationsV55.cs') -Force
function Read([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
$p=Join-Path $root 'WidgetHost.cs';$s=Read $p

# The visual popups can shift for monitor-edge fitting. The user owns the
# main circle's coordinates: snapshot BEFORE expansion and restore on close.
# Do not change the parent donut's wedge order, artwork or size.
$toggleSignature='    private void ToggleRing()'
$collapseSignature='    private void CollapseRing()'
$toggle=$s.IndexOf($toggleSignature,[StringComparison]::Ordinal)
$collapse=$s.IndexOf($collapseSignature,[StringComparison]::Ordinal)
if($toggle -lt 0 -or $collapse -lt 0){throw 'R65 WidgetHost does not expose main ring toggle/collapse methods'}
if(([regex]::Matches($s,[regex]::Escape($toggleSignature))).Count -ne 1 -or
   ([regex]::Matches($s,[regex]::Escape($collapseSignature))).Count -ne 1){
    throw 'R65 ambiguous host ring lifecycle'
}
$h=$s.IndexOf('{',$toggle+[int]$toggleSignature.Length)
if($h -lt 0){throw 'R65 missing toggle body'}
$s=$s.Insert($h+1,[string][char]10+
    '        if(!ringExpanded && activeModule==null) donutHome.Capture(this);')
# Locate the entire CollapseRing method, then restore at its tail. Doing so
# synchronously also avoids a deferred restoration overriding a manual drag.
$collapse=$s.IndexOf($collapseSignature,[StringComparison]::Ordinal)
$next=$s.IndexOf([string][char]10+'    private ', $collapse+$collapseSignature.Length,
    [StringComparison]::Ordinal)
if($next -lt 0){throw 'R65 cannot identify CollapseRing boundary'}
$close=$s.LastIndexOf([string][char]10+'    }',$next,[StringComparison]::Ordinal)
if($close -le $collapse){throw 'R65 missing CollapseRing final brace'}
$s=$s.Insert($close,[string][char]10+
    '        if(activeModule==null && donutHome.Restore(this)) SavePosition();')
$hostAnchor='    private void ToggleRing()'
$n=([regex]::Matches($s,[regex]::Escape($hostAnchor))).Count
if($n -ne 1){throw 'R65 cannot insert home state field'}
$s=$s.Replace($hostAnchor, @'
    // Automatic screen-edge accommodation must never become a permanent
    // relocation. Only the user dragging the widget changes its home.
    private readonly DeskSpaceDonutHomeV65 donutHome=new();
    private void ToggleRing()
'@.Replace([string][char]13+[char]10,[string][char]10))
Save $p $s
Write-Host 'R65: the main circle returns to its exact user-defined location when any temporarily relocated donut closes.'
