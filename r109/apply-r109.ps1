$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
if(!(Test-Path 'r109/DeskSpaceExcelFormatApplyV109.cs')){throw 'Missing R109 Excel direct format engine'}
Copy-Item -LiteralPath 'r109/DeskSpaceExcelFormatApplyV109.cs' -Destination "$r/DeskSpaceExcelFormatApplyV109.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$name)
{
    $crlf=[string][char]13+[string][char]10
    $source=[IO.File]::ReadAllText($path).Replace($crlf,[string][char]10)
    $before=$before.Replace($crlf,[string][char]10)
    $after=$after.Replace($crlf,[string][char]10)
    $matches=([regex]::Matches($source,[regex]::Escape($before))).Count
    if($matches -ne 1){throw "R109 $($name): expected one anchor, found $matches"}
    [IO.File]::WriteAllText($path,$source.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
$worker="$r/DeskSpaceExcelWorkerV41.cs"
Swap $worker @'
    public static Task<string> ApplyAsync(
'@ @'
    // Format-only operations are separate from the existing values-only autofill.
    public static Task<DeskSpaceExcelFormatApplyV109.Review> PreviewFormatAsync(
        DeskSpaceExcelTemplateV16 entry)=>
        Sta(()=>DeskSpaceExcelFormatApplyV109.Preview(entry));

    public static Task<string> ApplyFormatAsync(
        DeskSpaceExcelTemplateV16 entry,
        DeskSpaceExcelFormatApplyV109.Review approved)=>
        Sta(()=>DeskSpaceExcelFormatApplyV109.Apply(entry,approved));

    public static Task<string> ApplyAsync(
'@ 'STA-worker format methods'
$editor="$r/DeskSpaceExcelEditorV42.cs"
Swap $editor @'
    private bool editorBusyV42,hasUnsavedChangesV42,loadingEditorV42;
'@ @'
    private bool editorBusyV42,hasUnsavedChangesV42,loadingEditorV42;
    private DeskSpaceExcelFormatApplyV109.Review? pendingFormatReviewV109;
    private Guid formatTemplateIdV109=Guid.Empty;
'@ 'format approval state'
Swap $editor @'
        pasteButtons.Children.Add(RequiresExcel("Apply autofill",()=>Run(Apply),true));
'@ @'
        pasteButtons.Children.Add(RequiresExcel("Apply autofill",()=>Run(Apply),true));
        // Deliberately separate from value paste: this never copies source values.
        pasteButtons.Children.Add(RequiresExcel(
            "Check selected formatting destination",PreviewFormatV109));
        pasteButtons.Children.Add(RequiresExcel(
            "Apply selected format",ApplyFormatV109,true));
'@ 'separate user-visible format actions'
Swap $editor @'
    private void SaveEditorV42()
'@ @'
    private DeskSpaceExcelTemplateV16 FormatSourceV109()
    {
        if(hasUnsavedChangesV42)throw new InvalidOperationException(
            "Save your edited mapping before checking a destination.");
        if(editingTemplateV42==null)
            throw new InvalidOperationException("Select a captured Excel format first.");
        if(editingTemplateV42.SheetVisualV101==null)
            throw new InvalidOperationException(
                "This mapping has no cell-specific formats. Recapture it from Excel.");
        return editingTemplateV42;
    }
    private async void PreviewFormatV109()
    {
        try
        {
            var entry=FormatSourceV109();
            pendingFormatReviewV109=null;
            formatTemplateIdV109=Guid.Empty;
            if(status!=null)status.Text="Checking every selected Excel cell before formatting…";
            var checkedTarget=await DeskSpaceExcelWorkerV41.PreviewFormatAsync(entry);
            pendingFormatReviewV109=checkedTarget;
            formatTemplateIdV109=entry.Id;
            if(status!=null)status.Text=checkedTarget.Eligible?
                "READY: "+checkedTarget.ToString()+
                " Workbook: "+checkedTarget.Workbook+" · "+checkedTarget.Destination:
                "BLOCKED — Nothing will change. "+checkedTarget.ToString();
        }
        catch(Exception error)
        {
            pendingFormatReviewV109=null;
            if(status!=null)status.Text="FORMAT BLOCKED: "+error.Message;
        }
    }
    private async void ApplyFormatV109()
    {
        try
        {
            var entry=FormatSourceV109();
            var approved=pendingFormatReviewV109;
            if(approved==null || entry.Id!=formatTemplateIdV109 || !approved.Eligible)
                throw new InvalidOperationException(
                    "Choose the destination in Excel and run Check selected formatting destination first.");
            if(MessageBox.Show(
                "Apply cell-specific formatting to "+approved.Sheet+
                " "+approved.Destination+"?\n"+
                "Only if the ENTIRE destination is empty or its FULL data layout matches.\n"+
                approved.PlannedActions+"\n"+
                "Existing input values/formulas will not be overwritten.\n"+
                "The workbook will be backed up, and formatting read back before success.",
                "DeskSpace — confirm Excel formatting",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            // Reinspection happens inside Apply immediately before any change.
            if(status!=null)status.Text="Applying verified Excel cell formats…";
            string outcome=await DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved);
            if(status!=null)status.Text=outcome;
            pendingFormatReviewV109=null;formatTemplateIdV109=Guid.Empty;
        }
        catch(Exception error)
        {
            pendingFormatReviewV109=null;formatTemplateIdV109=Guid.Empty;
            if(status!=null)status.Text="FORMAT NOT VERIFIED: "+error.Message;
        }
    }

    private void SaveEditorV42()
'@ 'full-range preview/approval/readback UI'
Write-Host 'R109 development: direct per-cell Excel format apply actions (no VBA), full-range preview, backup/readback and safety gates.'
