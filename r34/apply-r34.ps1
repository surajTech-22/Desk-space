$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r34/DeskSpaceInformationV34.cs' "$root/DeskSpaceInformationV34.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$old,[string]$new) {
    if(!$script:s.Contains($old)){throw "R34 host source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
Must 'DeskSpaceInformationV33? informationOverlay' 'DeskSpaceInformationV34? informationOverlay'
Must 'new DeskSpaceInformationV33(this,' 'new DeskSpaceInformationV34(this,'
$openBegin=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
$openEnd=if($openBegin -ge 0){$s.IndexOf('    private void ClickSegment(string name)', $openBegin,[StringComparison]::Ordinal)}else{-1}
if($openBegin -lt 0 -or $openEnd -le $openBegin){throw 'R34 child arc host lifecycle missing'}
$open=$s.Substring($openBegin,$openEnd-$openBegin)
$before=@'
            informationOverlay.Show();
'@
$after=@'
            informationOverlay.Show();
'@
# The existing InfoChanged callback is kept byte-for-byte. Add a second
# callback only for a definitive child selection; hover previews must not
# collapse the parent while the user is still browsing the arc.
$anchor=@'
            });
            informationOverlay.Show();
'@
$replacement=@'
            },()=>{
                // A committed child (click, Enter, or drag release)
                // completes the expanded -> compact transition. Only
                // hide child arc; keep the selected Information shelf.
                if(ringExpanded)ToggleRing();
                informationOverlay?.HideArc();
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
            });
            informationOverlay.Show();
'@
$anchor=$anchor.Replace([string][char]13+[char]10,[string][char]10)
$replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
if(!$open.Contains($anchor)){throw 'R34 cannot attach committed selection callback'}
$open=$open.Replace($anchor,$replacement)
$s=$s.Substring(0,$openBegin)+$open+$s.Substring($openEnd)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R34: committed child selection collapses both main donut rings and preserves its compact attached shelf; edge-hover pages saved child entries.'
