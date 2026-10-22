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
        menu.Items.Add("Excel · saved formats",null,(_,_)=>Dispatcher.Invoke(
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
Require @'
        if(selected==7 && ringExpanded && activeModule==null)
        {
            // Parent wedge is reached: open contextual sub-donut on hover.
'@ @'
        if(selected==6 && ringExpanded && activeModule==null)
        {
            // Exactly like Information: show saved Excel format child arc.
            // Hover only previews; a click/drag-release explicitly pastes.
            OpenExcelArc();
        }
        if(selected!=6 && excelWheel?.IsArcOpen==true)
            excelWheel.HideArc();
        if(selected==7 && ringExpanded && activeModule==null)
        {
            // Parent wedge is reached: open contextual sub-donut on hover.
'@
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
        if(!excelWheel.IsPasting && !excelWheel.IsArcOpen)
            excelWheel.Show();
        if(excelWheel.IsArcOpen)
            content.Text="Excel"+Environment.NewLine+
                excelWheel.SavedCount+" saved";
    }

'@
$pos=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
if($pos -lt 0){throw 'R42 cannot find Information arc for matching child-wheel behavior'}
$s=$s.Substring(0,$pos)+$excelArc.Replace([string][char]13+[char]10,[string][char]10)+$s.Substring($pos)
# When leaving the parent's active wedge, hide only the Excel child arc,
# preserving any status shelf and avoiding redundant heavy editor creation.
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R42 Excel sector now opens the Information-style saved-format child wheel, never the editor.'
Write-Host 'R42 explicit saved-format selection performs direct, guarded Paste Special Values into open Excel.'
