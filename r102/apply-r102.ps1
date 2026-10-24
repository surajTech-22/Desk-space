$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r102/DeskSpaceExcelNormalizeV102.cs' "$r/DeskSpaceExcelNormalizeV102.cs" -Force
function Swap([string]$file,[string]$before,[string]$after,[string]$description)
{
 $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $matches=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($matches -ne 1){throw "R102 $description anchor matched $matches times"}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
Swap "$r/DeskSpaceExcelBridgeV16.cs" @'
    private static string Text(object? o)=>
'@ @'
    // Reuse the existing multi-instance aware COM resolver on the
    // worker STA; do not launch Excel or change its selected workbook.
    internal static dynamic ExcelForNormalizationV102()=>Excel();

    private static string Text(object? o)=>
'@ 'normalization COM reuse'
Swap "$r/DeskSpaceExcelWorkerV41.cs" @'
    public static Task<string> ApplyAsync(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Apply(preview,entry,overwrite));
'@ @'
    public static Task<string> ApplyAsync(
        DeskSpaceExcelProposalV16 preview,DeskSpaceExcelTemplateV16 entry,
        bool overwrite)=>
        Sta(()=>DeskSpaceExcelBridgeV16.Apply(preview,entry,overwrite));

    internal static Task<DeskSpaceExcelNormalizePlanV102> ReviewTypesAsync(
        DeskSpaceExcelTemplateV16 model,string dateOrder)=>
        Sta(()=>DeskSpaceExcelNormalizeV102.Preview(model,dateOrder));

    internal static Task<string> RepairTypesAsync(
        DeskSpaceExcelNormalizePlanV102 plan,Guid mappingId)=>
        Sta(()=>DeskSpaceExcelNormalizeV102.Apply(plan,mappingId));
'@ 'normalize only on Excel STA worker'
$ed="$r/DeskSpaceExcelEditorV42.cs"
Swap $ed @'
    private ContentControl? sheetPreviewHostV101;
'@ @'
    private ContentControl? sheetPreviewHostV101;
    private ComboBox? dateInputOrderV102;
    private TextBlock? typeRepairSummaryV102;
    private DeskSpaceExcelNormalizePlanV102? pendingTypeRepairV102;
'@ 'type-repair UI state'
Swap $ed @'
        footer.Children.Add(pasteButtons);
        matchSummary=HintV42("Preview before any Excel changes.",10);
'@ @'
        footer.Children.Add(pasteButtons);
        // New input values can have the wrong Excel storage type even when
        // the saved template expects numbers/dates. Preview mismatches
        // using the saved DATA-FREE formatting snapshot, then confirm.
        var typeTools=new WrapPanel{Margin=new Thickness(0,3,0,4)};
        typeTools.Children.Add(HintV42("Incoming date order:",10));
        dateInputOrderV102=new ComboBox{
            Width=175,Height=28,
            ItemsSource=new[]{
                "Ask if ambiguous","Day first · DD/MM/YYYY",
                "Month first · MM/DD/YYYY"},
            SelectedIndex=0,Margin=new Thickness(4,0,8,0)};
        dateInputOrderV102.SelectionChanged+=(_,_)=>{
            pendingTypeRepairV102=null;
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Review again after changing the date order.";
        };
        typeTools.Children.Add(dateInputOrderV102);
        typeTools.Children.Add(RequiresExcel(
            "Check new input types",()=>CheckInputTypesV102()));
        typeTools.Children.Add(RequiresExcel(
            "Apply reviewed corrections",()=>ApplyInputTypesV102(),true));
        footer.Children.Add(typeTools);
        typeRepairSummaryV102=HintV42(
            "Compare NEW selected data with captured number/date formats. "+
            "Ambiguous values stay unchanged. No workbook edits during Check.",10);
        footer.Children.Add(typeRepairSummaryV102);
        matchSummary=HintV42("Preview before any Excel changes.",10);
'@ 'type-repair preview and apply controls'
Swap $ed @'
    private static void CommitCellEditsV42(DataGrid grid)
'@ @'
    private async void CheckInputTypesV102()
    {
        try
        {
            var template=editingTemplateV42 ??
                throw new InvalidOperationException(
                    "Capture or select a saved Excel mapping first.");
            if(hasUnsavedChangesV42)
                throw new InvalidOperationException(
                    "Save the mapping before checking new input data.");
            pendingTypeRepairV102=null;
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Checking selected Excel range without editing it…";
            string dateOrder=(dateInputOrderV102?.SelectedIndex??0) switch{
                1=>"DMY",2=>"MDY",_=>"Ask"};
            var plan=await DeskSpaceExcelWorkerV41.ReviewTypesAsync(
                template,dateOrder);
            if(!ReferenceEquals(template,editingTemplateV42))return;
            pendingTypeRepairV102=plan;
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text=
                    plan.Changes.Count+" proposed fixes ("+
                    plan.Values+" value conversions; "+
                    plan.Formats+" captured number/date formats). "+
                    plan.Warnings.Count+" cells need separate review. "+
                    string.Join(" | ",plan.Warnings.Take(3));
        }
        catch(Exception ex)
        {
            pendingTypeRepairV102=null;
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="No input corrections applied: "+ex.Message;
        }
    }

    private async void ApplyInputTypesV102()
    {
        var plan=pendingTypeRepairV102;
        if(plan==null)
        {
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Check the current input types first.";
            return;
        }
        if(plan.Changes.Count==0)
        {
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Nothing to correct safely.";
            return;
        }
        var template=editingTemplateV42;
        if(template==null || template.Id!=plan.MappingId)
        {
            pendingTypeRepairV102=null;
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Mapping changed; preview again.";
            return;
        }
        var answer=MessageBox.Show(
            "Apply "+plan.Values+" safe input conversions and "+
            plan.Formats+" number/date formats to "+plan.Sheet+"?"+
            Environment.NewLine+
            plan.Warnings.Count+" ambiguous or formula cells will remain unchanged."+
            Environment.NewLine+
            "A workbook backup will be created; the original workbook is NOT auto-saved.",
            "DeskSpace · Review Excel input types",
            MessageBoxButton.YesNo,MessageBoxImage.Question);
        if(answer!=MessageBoxResult.Yes)return;
        pendingTypeRepairV102=null;
        try
        {
            string backup=await DeskSpaceExcelWorkerV41.RepairTypesAsync(
                plan,template.Id);
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Converted safely. Backup: "+backup+
                    " · Review and save in Excel when ready.";
        }
        catch(Exception ex)
        {
            if(typeRepairSummaryV102!=null)
                typeRepairSummaryV102.Text="Corrections not completed: "+ex.Message+
                    " · Inspect the workbook and its backup before retrying.";
        }
    }

    private static void CommitCellEditsV42(DataGrid grid)
'@ 'reviewed and backed-up update actions'
Swap $ed @'
    private void LoadEditorV42(DeskSpaceExcelTemplateV16 entry)
    {
'@ @'
    private void LoadEditorV42(DeskSpaceExcelTemplateV16 entry)
    {
        pendingTypeRepairV102=null;
'@ 'invalidate old proposals on mapping change'
Swap $ed @'
    private void ShowCapturedV42(DeskSpaceExcelTemplateV16 candidate)
    {
'@ @'
    private void ShowCapturedV42(DeskSpaceExcelTemplateV16 candidate)
    {
        pendingTypeRepairV102=null;
'@ 'invalidate proposals after fresh capture'
Write-Host 'R102: inspect NEW Excel values against captured formatting, warn for ambiguous dates/IDs, explicit reviewed type+format repair, backup and no automatic save.'
