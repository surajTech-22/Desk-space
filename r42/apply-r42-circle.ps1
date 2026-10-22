$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function Require([string]$old,[string]$new)
{
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($old)){throw "R42 circle Excel parent anchor missing: $($old.Substring(0,[Math]::Min(110,$old.Length)))"}
 $script:s=$script:s.Replace($old,$new)
}
Require '    private DeskSpaceExcelClipboardV39? excelClipboardShelf;' @'
    private DeskSpaceExcelClipboardV39? excelClipboardShelf;
    private DeskSpaceExcelWheelV42? excelWheel;
'@
Require '            excelClipboardShelf?.Dispose();' @'
            excelClipboardShelf?.Dispose();
            excelWheel?.Dispose();
'@
Require @'
        menu.Items.Add("Excel · reusable data",null,(_,_)=>Dispatcher.Invoke(
            ()=>OpenInWidget("Excel")));
'@ @'
        menu.Items.Add("Excel Functions · Clipboard / Information",null,(_,_)=>Dispatcher.Invoke(
            OpenExcelArc));
'@
Require @'
    private void ClickSegment(string name)
    {
        if(name=="Excel Clipboard")
'@ @'
    private void ClickSegment(string name)
    {
        if(name=="Excel")
        {
            OpenExcelArc();
            return; // The circle must NEVER open the large Excel editor.
        }
        if(name=="Excel Clipboard")
'@
# R39 introduced a separate 14th outer Excel Clipboard sector.
# Replace it with one Excel Functions parent: 6 inner + 7 outer.
# Preserve all original twelve sectors, their ordering and existing names.
Require @'
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings", "Excel",
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks", "Excel Clipboard"
'@ @'
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings",
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks", "Excel Functions"
'@
Require @'
        "⌕", "▦", "⊞", "◷", "☀", "⚙", "▥",
        "◆", "✎", "◉", "▣", "▤", "☑", "▦"
'@ @'
        "⌕", "▦", "⊞", "◷", "☀", "⚙",
        "◆", "✎", "◉", "▣", "▤", "☑", "▥"
'@
# The existing independent Excel Clipboard remains alive, but must open
# through the Excel Functions -> Clipboard child rather than a main wedge.
$clickName='        if(name=="Excel")'
if(!$s.Contains($clickName)){throw "R42 main Excel click handler missing"}
$s=$s.Replace($clickName,'        if(name=="Excel Functions")')
$outside='        if(name=="Excel Clipboard")'
if($s.Contains($outside)){
    $clickA=$s.IndexOf($outside,[StringComparison]::Ordinal)
    $clickB=$s.IndexOf('        if(name=="Information")',$clickA,[StringComparison]::Ordinal)
    if($clickB -le $clickA){throw 'R42 cannot isolate legacy Excel Clipboard sector action'}
    $s=$s.Substring(0,$clickA)+$s.Substring($clickB)
}

# Different inner and outer wedge counts. No phantom blank Excel Clipboard
# sector is drawn or hit-testable. Information stays original index 6; Excel Functions is outer index 12.
$sectorA=$s.IndexOf('    private int SectorAt(',[StringComparison]::Ordinal)
$sectorB=if($sectorA -ge 0){$s.IndexOf('    private ', $sectorA+24,[StringComparison]::Ordinal)}else{-1}
if($sectorA -lt 0 -or $sectorB -le $sectorA){throw 'R42 SectorAt boundaries unavailable'}
$sector=$s.Substring($sectorA,$sectorB-$sectorA)
if(!$sector.Contains('const double sectors=7;') -or
   !$sector.Contains('if(slot<0 || slot>=7')){
    throw 'R42 expected 14-sector input hit geometry missing'
}
$sector=$sector.Replace('const double sectors=7;',
    'double sectors=outer?7:6;')
$sector=$sector.Replace('if(slot<0 || slot>=7',
    'if(slot<0 || slot>=sectors')
$sector=$sector.Replace('return (outer?7:0)+slot;',
    'return (outer?6:0)+slot;')
$s=$s.Substring(0,$sectorA)+$sector+$s.Substring($sectorB)

$quickA=$s.IndexOf('    private int QuickAddHitAt(',[StringComparison]::Ordinal)
$quickB=if($quickA -ge 0){$s.IndexOf('    private void BuildDonut()', $quickA,[StringComparison]::Ordinal)}else{-1}
if($quickA -lt 0 -or $quickB -le $quickA){throw 'R42 QuickAddHitAt boundaries unavailable'}
$quick=$s.Substring($quickA,$quickB-$quickA)
$quick=$quick.Replace('bool outer=i>=7;','bool outer=i>=6;')
$quick=$quick.Replace('int slot=i%7;',
    'int slot=outer?i-6:i;')
$quick=$quick.Replace('(-90+slot*(360.0/7))*Math.PI/180',
    '(-90+slot*(360.0/(outer?7:6)))*Math.PI/180')
$s=$s.Substring(0,$quickA)+$quick+$s.Substring($quickB)

$drawA=$s.IndexOf('    private void BuildDonut()', [StringComparison]::Ordinal)
$drawB=if($drawA -ge 0){$s.IndexOf('    private static Geometry Sector(', $drawA,[StringComparison]::Ordinal)}else{-1}
if($drawA -lt 0 -or $drawB -le $drawA){throw 'R42 BuildDonut method boundaries unavailable'}
$draw=$s.Substring($drawA,$drawB-$drawA)
if(!$draw.Contains('bool outer=i>=7;int slot=i%7;') -or
   !$draw.Contains('double step=360.0/7;')){
    throw 'R42 expected 14-sector rendering geometry missing'
}
$draw=$draw.Replace('bool outer=i>=7;int slot=i%7;',
    'bool outer=i>=6;int slot=outer?i-6:i;')
$draw=$draw.Replace('double step=360.0/7;',
    'double step=360.0/(outer?7:6);')
$draw=$draw.Replace('(-90+slot*(360.0/7))*Math.PI/180',
    '(-90+slot*(360.0/(outer?7:6)))*Math.PI/180')
$s=$s.Substring(0,$drawA)+$draw+$s.Substring($drawB)

# R29's original Information parent stays at outer index 6.
# Add Excel Functions to outer index 12 without remapping Information.
$paintBegin=$s.IndexOf('    private void UpdateHighlightedSector(',[StringComparison]::Ordinal)
$paintEnd=if($paintBegin -ge 0){
    $s.IndexOf('    private void BuildCompact()', $paintBegin,[StringComparison]::Ordinal)
}else{-1}
if($paintBegin -lt 0 -or $paintEnd -le $paintBegin){
    throw 'R42 cannot locate live parent donut selector'
}
$paint=$s.Substring($paintBegin,$paintEnd-$paintBegin)
foreach($required in @(
    'if(selected>=0 && selected!=6)',
    'else if(selected==6 && ringExpanded && activeModule==null)',
    'CloseInformationContext();',
    'OpenInformationArc();',
    'if(selected==highlightedSector)return;'
)){
    if(!$paint.Contains($required)){throw "R42 live wheel selector changed: $required"}
}

$excelSelector=@'
        // Excel Functions is outer sector 12; Information stays outer 6.
        // Hover cannot paste Excel data; child click/drag-release is required.
        if(selected==12 && ringExpanded && activeModule==null)
            OpenExcelArc();
        if(selected>=0 && selected!=12 && excelWheel?.IsArcOpen==true)
            excelWheel.HideArc();
'@
$paint=$paint.Replace('        if(selected==highlightedSector)return;',
    $excelSelector.Replace([string][char]13+[char]10,[string][char]10)+
    '        if(selected==highlightedSector)return;')
$s=$s.Substring(0,$paintBegin)+$paint+$s.Substring($paintEnd)
# Same press/drag navigation and small status shelf as the Information
# module; selected saved format directly pastes to the running Excel workbook.
$excelArc=@'
    private void OpenExcelArc()
    {
        if(activeModule!=null)return;
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        excelWheel??=new DeskSpaceExcelWheelV42(this,()=>{
            if(ringExpanded)CollapseRing();
        });
        excelWheel.SetExcelAvailabilityChanged(open=>{
            if(ringExpanded && activeModule==null && highlightedSector==12)
                content.Text=open
                    ? "Excel Functions"+Environment.NewLine+
                      excelWheel.SavedCount+" saved"
                    : "Excel Functions"+Environment.NewLine+"Excel is not open";
        });
        if(!excelWheel.IsPasting && !excelWheel.IsArcOpen)
            excelWheel.Show();
        if(excelWheel.IsArcOpen)
            content.Text=DeskSpaceExcelWheelV42.ExcelIsOpen()
                ? "Excel Functions"+Environment.NewLine+excelWheel.SavedCount+" saved"
                : "Excel Functions"+Environment.NewLine+"Excel is not open";
    }

'@
$pos=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
if($pos -lt 0){throw 'R42 cannot find Information arc for matching child-wheel behavior'}
$s=$s.Substring(0,$pos)+$excelArc.Replace([string][char]13+[char]10,[string][char]10)+$s.Substring($pos)
# Forward parent-captured click-and-drag gestures to Excel child sectors,
# matching the already verified Information module's drag-to-select path.
$moveBegin=$s.IndexOf('    private void OnGestureMove(',[StringComparison]::Ordinal)
$moveEnd=$s.IndexOf('    private void OnGestureUp(',[Math]::Max(0,$moveBegin),[StringComparison]::Ordinal)
if($moveBegin -lt 0 -or $moveEnd -le $moveBegin){throw 'R42 parent drag-move boundaries missing'}
$move=$s.Substring($moveBegin,$moveEnd-$moveBegin)
$move=$move.Replace('if(informationOverlay?.IsOpen==true)return;',
  'if(informationOverlay?.IsOpen==true || excelWheel?.IsArcOpen==true)return;')
$forward=@'
        if(excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }
'@
$before='        informationOverlay?.EndCapturedDrag();'
if(!$move.Contains($before)){throw 'R42 Information drag-move anchor changed'}
$move=$move.Replace($before,$forward.Replace([string][char]13+[char]10,[string][char]10)+$before)
$s=$s.Substring(0,$moveBegin)+$move+$s.Substring($moveEnd)
$upBegin=$s.IndexOf('    private void OnGestureUp(',[StringComparison]::Ordinal)
$upEnd=$s.IndexOf('    private void ', $upBegin+26,[StringComparison]::Ordinal)
if($upBegin -lt 0 -or $upEnd -le $upBegin){throw 'R42 parent drag-up boundaries missing'}
$up=$s.Substring($upBegin,$upEnd-$upBegin)
$anchor='        if(ringGesture && informationOverlay?.IsArcOpen==true &&'
if(!$up.Contains($anchor)){throw 'R42 Information drag-up action missing'}
$guard=@'
        if(ringGesture && excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return;
        }
'@
$up=$up.Replace($anchor,
    $guard.Replace([string][char]13+[char]10,[string][char]10)+$anchor)
$s=$s.Substring(0,$upBegin)+$up+$s.Substring($upEnd)

# When leaving the parent's active wedge, hide only the Excel child arc,
# preserving any status shelf and avoiding redundant heavy editor creation.
# R43 shared contextual child-donut lifecycle: an independent WPF Popup
# must close BEFORE its parent changes from expanded ring to compact circle.
foreach($method in @('    private void CollapseRing()',
                     '    private void BuildCompact()')){
    $a=$s.IndexOf($method,[StringComparison]::Ordinal)
    if($a -lt 0){throw "R43 missing parent lifecycle: $method"}
    $b=$s.IndexOf('{',$a+$method.Length,[StringComparison]::Ordinal)
    if($b -lt 0){throw "R43 missing brace: $method"}
    $guard=@'
        excelWheel?.HideArc();
        if(informationOverlay?.IsArcOpen==true)
            informationOverlay.HideArc();
'@
    $s=$s.Insert($b+1,[string][char]10+
        $guard.Replace([string][char]13+[char]10,[string][char]10))
}
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R42 Excel sector now opens the Information-style saved-format child wheel, never the editor.'
Write-Host 'R42 explicit saved-format selection performs direct, guarded Paste Special Values into open Excel.'
