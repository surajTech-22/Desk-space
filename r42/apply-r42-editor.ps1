$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r42/DeskSpaceExcelEditorV42.cs' "$root/DeskSpaceExcelEditorV42.cs" -Force
Copy-Item 'r42/DeskSpaceExcelWheelV42.cs' "$root/DeskSpaceExcelWheelV42.cs" -Force
function Patch([string]$file,[string]$old,[string]$new)
{
    $data=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$data.Contains($old)){throw "R42 source anchor missing ($file): $($old.Substring(0,[Math]::Min($old.Length,120)))"}
    $data=$data.Replace($old,$new)
    [IO.File]::WriteAllText($file,$data,[Text.UTF8Encoding]::new($false))
}
$ui="$root/DeskSpaceExcelV16.cs"
Patch $ui 'internal sealed class DeskSpaceExcelV16 : IDisposable' 'internal sealed partial class DeskSpaceExcelV16 : IDisposable'
# Keep native data/model, screenshot and Excel COM, but replace the old
# confusing multi-step Create() with the new saved-list and editor grid.
$s=[IO.File]::ReadAllText($ui).Replace([string][char]13+[char]10,[string][char]10)
$head=$s.IndexOf('    public FrameworkElement Create()', [StringComparison]::Ordinal)
if($head -lt 0){throw 'R42 original Excel Create() not found'}
$brace=$s.IndexOf('{',$head)
$depth=0;$end=-1
for($i=$brace;$i -lt $s.Length;$i++){
    if($s[$i] -eq '{'){$depth++}
    elseif($s[$i] -eq '}'){$depth--;if($depth -eq 0){$end=$i+1;break}}
}
if($end -le $brace){throw 'R42 cannot isolate original WPF Excel editor Create()'}
$s=$s.Substring(0,$head)+$s.Substring($end)
# R41 capture already runs Excel COM outside the UI thread and batches rows.
# After it loads its original detailed cell view, also show the simplified
# editable header->value grid (the simple grid is the default visible view).
$s=$s.Replace('        await PresentCandidateAsync(candidate);',
              '        await PresentCandidateAsync(candidate);'+[string][char]10+
              '        ShowCapturedV42(candidate);')
if(([regex]::Matches($s,'ShowCapturedV42\(candidate\)')).Count -ne 3)
{throw 'R42 screenshot/live/verification result not linked to edit grid at all three sites'}
# The previous explicit "Save checked" remains reachable in advanced,
# but the primary Save mapping changes button writes one editable mapping.
[IO.File]::WriteAllText($ui,$s,[Text.UTF8Encoding]::new($false))
# Harden the shared value-paste guard for both the circle and Workspace.
# NEVER write saved Reference Cells, Formula Cells or blank placeholders.
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
Patch $bridge @'
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        int r,c;
'@ @'
        if(!entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Screenshot-only cell positions are not confirmed. "+
                "Link this format to the exact live Excel selection first.");
        dynamic app=Excel();
        dynamic sheet=app.ActiveSheet;
        int r,c;
'@
Patch $bridge @'
            if(!verified)decision="Unconfirmed target";
            else if(source.Role!="Value" || source.ValueKind=="formula")
'@ @'
            if(!verified)decision="Unconfirmed target";
            else if(source.Role!="Value" || source.ValueKind=="formula" ||
                    source.ValueKind=="blank")
'@
Patch $bridge @'
        if(!preview.Verified || !entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Target cells are not verified. Link a live Excel range and preview again.");
'@ @'
        if(!preview.Verified || !entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Target cells are not verified. Link a live Excel range and preview again.");
        if(!preview.Changes.Any(c=>c.CanApply &&
               c.Source.Role=="Value" &&
               c.Source.ValueKind!="formula" &&
               c.Source.ValueKind!="blank"))
            throw new InvalidOperationException(
                "No verified input cells are safe to paste. Nothing was changed.");
'@
Patch $bridge @'
            foreach(var change in preview.Changes.Where(x=>x.CanApply))
            {
                dynamic cell=sheet.Cells[change.Row,change.Column];
'@ @'
            foreach(var change in preview.Changes.Where(x=>x.CanApply &&
                x.Source.Role=="Value" &&
                x.Source.ValueKind!="formula" &&
                x.Source.ValueKind!="blank"))
            {
                dynamic cell=sheet.Cells[change.Row,change.Column];
'@
Patch $bridge @'
                if(change.Source.ValueKind=="formula")
                    cell.FormulaR1C1=change.Source.FormulaR1C1;
                else cell.Value2=TypedValue(change.Source);
'@ @'
                // Paste Special Values semantics only. Never replay formulas.
                cell.Value2=TypedValue(change.Source);
'@
Write-Host 'R42: main Workspace Excel mapping editor now shows saved format header/value rows and supports rename/update.'
Write-Host 'R42: wheel direct paste is values-only with exact destination, formula and backup protection.'
