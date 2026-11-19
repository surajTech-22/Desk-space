$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$editor=Join-Path $root 'DeskSpaceExcelEditorV42.cs'
$excel=Join-Path $root 'DeskSpaceExcelV16.cs'
foreach($p in @($editor,$excel)){if(!(Test-Path $p)){throw "R345 shipping Excel source missing: $p"}}
Copy-Item 'r345/DeskSpaceExcelPreflightV345.cs' (Join-Path $root 'DeskSpaceExcelPreflightV345.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function One([string]$file,[string]$old,[string]$new,[string]$reason)
{
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R345 $reason expected one anchor, found $hits"}
 [IO.File]::WriteAllText($file,$s.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
One $excel @'
    private DeskSpaceExcelProposalV16? latest;
'@ @'
    private DeskSpaceExcelProposalV16? latest;
    private string? pasteApprovalProofV345;
    private bool approvalOverwriteV345,approvalManualV345;
'@ 'exact reviewed Excel paste proof fields'
One $excel @'
    private async void Preview()
    {
        try
        {
        var entry=Selected;
'@ @'
    private async void Preview()
    {
        pasteApprovalProofV345=null;
        try
        {
        var entry=Selected;
'@ 'invalidate old approval upon any new Excel preview'
One $excel @'
    private async void Apply()
    {
'@ @'
    private void ReviewPreflightV345()
    {
        var entry=Selected;
        if(latest==null||previewTemplateId!=entry.Id)
            throw new InvalidOperationException("Preview the Excel mapping first.");
        if(!latest.Verified || latest.Writable<=0)
            throw new InvalidOperationException("No safe, writable Excel mapping proposal.");
        var owner=Window.GetWindow(templates);
        pasteApprovalProofV345=DeskSpaceExcelPreflightV345.Review(owner,latest);
        if(pasteApprovalProofV345!=null)
        {
            approvalOverwriteV345=overwrite.IsChecked==true;
            approvalManualV345=manual.IsChecked==true;
            status.Text="Exact paste plan approved. Final confirmation and a fresh workbook backup/readback are still required.";
        }
        else status.Text="Excel preflight review cancelled. Nothing was written.";
    }
    private async void Apply()
    {
'@ 'new user-facing explicit Excel plan review'
One $excel @'
        if(!latest.Verified||latest.Writable==0)
            throw new InvalidOperationException("Nothing is approved to autofill.");
        var choice=MessageBox.Show(
'@ @'
        if(!latest.Verified||latest.Writable==0)
            throw new InvalidOperationException("Nothing is approved to autofill.");
        if(pasteApprovalProofV345==null ||
            !string.Equals(pasteApprovalProofV345,
                DeskSpaceExcelPreflightV345.Proof(latest),
                StringComparison.Ordinal) ||
            approvalOverwriteV345!=(overwrite.IsChecked==true) ||
            approvalManualV345!=(manual.IsChecked==true))
            throw new InvalidOperationException(
                "Review and approve the exact current paste plan. Any option or preview change requires new approval.");
        // Single-use proof even if user rejects the existing final Yes/No.
        pasteApprovalProofV345=null;
        var choice=MessageBox.Show(
'@ 'no Excel write without exact unmodified approved preview'
One $editor @'
        pasteButtons.Children.Add(RequiresExcel(
            "Paste matching information",()=>{
'@ @'
        pasteButtons.Children.Add(RequiresExcel(
            "Review exact paste safety plan",()=>
                Run(ReviewPreflightV345)));
        pasteButtons.Children.Add(RequiresExcel(
            "Paste matching information",()=>{
'@ 'integrate review into existing Captured Sheet/Mappings interface'
if(!(Get-Content $excel -Raw).Contains('pasteApprovalProofV345=DeskSpaceExcelPreflightV345.Review')){
 throw 'R345 exact Excel review not active in shipping source'
}
Write-Host 'R345: captured-sheet Excel value paste now needs exact reviewed-plan approval; existing backup and live readback unchanged.'
