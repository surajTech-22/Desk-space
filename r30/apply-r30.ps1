$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r30/DeskSpaceInformationV30.cs' "$root/DeskSpaceInformationV30.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$a,[string]$b) {
    if(!$script:s.Contains($a)){throw "R30 WidgetHost source anchor missing: $a"}
    $script:s=$script:s.Replace($a,$b)
}
Must 'DeskSpaceInformationV27? informationOverlay' 'DeskSpaceInformationV30? informationOverlay'
Must 'new DeskSpaceInformationV27(this,' 'new DeskSpaceInformationV30(this,'

# A "context" means the attached child donut; an already-open detail or
# editing shelf remains available below the main circle until explicitly
# closed or navigation is confirmed. This is essential: the cursor must
# cross other main-ring sectors to reach a below-circle shelf.
$begin=$s.IndexOf('    private void CloseInformationContext()', [StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void OpenInformationArc()', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R30 cannot isolate parent submenu close'}
$method=$s.Substring($begin,$end-$begin)
foreach($v in @('informationOverlay?.IsOpen','informationOverlay.Hide()','informationArcVersion++'))
{if(!$method.Contains($v)){throw "R30 context safety anchor missing $v"}}
$method=$method.Replace('informationOverlay?.IsOpen','informationOverlay?.IsArcOpen')
$method=$method.Replace('informationOverlay.Hide()','informationOverlay.HideArc()')
$s=$s.Substring(0,$begin)+$method+$s.Substring($end)

# OpenInformationArc must remain available while an information detail shelf
# is open. Do not confuse shelf.IsOpen with arc.IsOpen.
$begin=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void ClickSegment(string name)', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R30 parent arc opening boundaries missing'}
$method=$s.Substring($begin,$end-$begin)
if(!$method.Contains('informationOverlay?.IsOpen==true')){throw 'R30 parent arc guard unexpectedly changed'}
$method=$method.Replace('informationOverlay?.IsOpen==true','informationOverlay?.IsArcOpen==true')
$s=$s.Substring(0,$begin)+$method+$s.Substring($end)

# Intentional click into a DIFFERENT main module closes the lower shelf.
# An active editing form warns before abandoning unsaved input.
$begin=$s.IndexOf('    private void ClickSegment(string name)',[StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void ApplySettings()', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R30 main menu click handler unavailable'}
$method=$s.Substring($begin,$end-$begin)
if(!$method.Contains('        CloseInformationContext();')){throw 'R30 previous module transition missing'}
$method=$method.Replace('        CloseInformationContext();',@'
        if(informationOverlay?.IsEditing==true &&
           !informationOverlay.ConfirmNavigation())return;
        CloseInformationContext();
        informationOverlay?.Hide();
'@)
$s=$s.Substring(0,$begin)+$method+$s.Substring($end)

# MouseLeave of the parent should be protected only when child sectors are
# open; the below-circle shelf can outlive the expanded parent independently.
Must 'informationOverlay?.IsOpen!=true && !ringGesture' 'informationOverlay?.IsArcOpen!=true && !ringGesture'
# Similarly ordinary pointer motion may leave after submenu is dismissed;
# the information shelf itself does not need to freeze the main donut open.
$begin=$s.IndexOf('    private void OnGestureMove(',[StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void OnGestureUp(', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R30 mouse motion source unavailable'}
$method=$s.Substring($begin,$end-$begin)
if(!$method.Contains('informationOverlay?.IsOpen==true')){throw 'R30 parent pointer guard not found'}
$method=$method.Replace('informationOverlay?.IsOpen==true','informationOverlay?.IsArcOpen==true')
$s=$s.Substring(0,$begin)+$method+$s.Substring($end)

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R30 child donut: hover selects and previews all saved sectors, curved highlight, ordinary click/touch, keyboard navigation, mouse-wheel overflow, deliberate + New, Delete, draft warning, existing encrypted vault preserved.'
