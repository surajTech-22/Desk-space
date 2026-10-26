$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$view="$root/DeskSpaceExcelSheetViewV101.cs"
$worker="$root/DeskSpaceExcelWorkerV41.cs"
$editor="$root/DeskSpaceExcelEditorV42.cs"
$unified="$root/DeskSpaceExcelUnifiedV126.cs"
foreach($component in @('DeskSpaceExcelFormulaExpanderV144.cs',
    'DeskSpaceExcelFormulaOnlyPolicyV144.cs',
    'DeskSpaceExcelPasteSpecialV144.cs')){
    Copy-Item "r144/$component" "$root/$component" -Force
}
$lf=[string][char]10; $crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$name)
{
    $text=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $old=$before.Replace($crlf,$lf);$new=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($text,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R144 $($name): expected exactly one anchor, found $count"}
    [IO.File]::WriteAllText($path,$text.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
# Leave the formula itself IN the sheet cell. Give each formula a small
# top-right expand icon, and open a floating overlay at that cell.
Swap $view @'
            surface.Children.Add(text);
            border.Child=surface;
'@ @'
            surface.Children.Add(text);
            if(isFormula && item!=null)
                DeskSpaceExcelFormulaExpanderV144.Attach(border,surface,
                    item.Address,
                    item.FormulaA1.Length>0?item.FormulaA1:item.FormulaR1C1);
            border.Child=surface;
'@ 'in-cell click-to-expand full formula preview'
# Dedicated asynchronous STA Excel COM operation. Do NOT route through
# the FORMAT-only decision gate, otherwise blank formula slots are rejected.
Swap $worker @'
    public static Task<DeskSpaceExcelFormatApplyV109.Review> PreviewFormatAsync(
'@ @'
    public static Task<string> PasteSpecialFormulasAsync(
        DeskSpaceExcelTemplateV16 entry)=>
        Sta(()=>DeskSpaceExcelPasteSpecialV144.Paste(entry));

    public static Task<DeskSpaceExcelFormatApplyV109.Review> PreviewFormatAsync(
'@ 'separate Excel STA Paste Special - Formulas route'
# Show the operation alongside the existing format-only action on the
# existing Mappings tab. No new major module or separate formula window.
Swap $editor @'
        formatFoot.Children.Add(formatControls);
'@ @'
        formatControls.Children.Add(RequiresExcel(
            "Paste Special · Formulas",PasteSpecialFormulasV144,true));
        formatFoot.Children.Add(formatControls);
'@ 'visible formula-only paste command on existing mapping tab'
Swap $unified @'
    private void SaveUnifiedMappingV126()
'@ @'
    // A real, independent Excel formula-only paste operation.
    // Values, formatting and formula preview remain separate features.
    private async void PasteSpecialFormulasV144()
    {
        try
        {
            if(!formatModeV126)
                throw new InvalidOperationException(
                    "Select Format / Formula mappings and a saved format first.");
            var entry=FormatSourceV109();
            if(status!=null)
                status.Text="Checking selected Excel range for Paste Special · Formulas…";
            var result=await DeskSpaceExcelWorkerV41.PasteSpecialFormulasAsync(entry);
            if(status!=null)status.Text=result;
        }
        catch(Exception error)
        {
            if(status!=null)
                status.Text="FORMULA PASTE BLOCKED: "+error.Message;
        }
    }

    private void SaveUnifiedMappingV126()
'@ 'saved-format-only Excel formula action handler'
Write-Host 'R144 applied: Excel-like in-cell expand popup, independent Paste Special Formulas via native Excel R1C1 COM, source values untouched, destination conflict checks and safety backups.'
