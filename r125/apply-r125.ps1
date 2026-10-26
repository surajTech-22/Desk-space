$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$path,[string]$before,[string]$after,[string]$label)
{
    $lf=[string][char]10;$crlf=[string][char]13+$lf
    $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $before=$before.Replace($crlf,$lf)
    $after=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($source,[regex]::Escape($before))).Count
    if($count -ne 1){throw "R125 ${label}: expected one anchor, found $count"}
    [IO.File]::WriteAllText($path,$source.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
function ReplaceSpan([string]$path,[string]$first,[string]$last,
    [string]$replacement,[string]$label)
{
    $lf=[string][char]10;$crlf=[string][char]13+$lf
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $start=$s.IndexOf($first,[StringComparison]::Ordinal)
    if($start -lt 0 -or $s.IndexOf($first,$start+1,[StringComparison]::Ordinal) -ge 0){
        throw "R125 ${label}: start marker missing or ambiguous"
    }
    $end=$s.IndexOf($last,$start+$first.Length,[StringComparison]::Ordinal)
    if($end -lt 0){throw "R125 ${label}: end marker missing"}
    [IO.File]::WriteAllText($path,
        $s.Substring(0,$start)+$replacement.Replace($crlf,$lf)+$s.Substring($end),
        [Text.UTF8Encoding]::new($false))
}
$editor="$root/DeskSpaceExcelEditorV42.cs"
Swap $editor @'
            "Data mappings · copy / paste",()=>{
'@ @'
            "Information / Data · matched headers",()=>{
'@ 'honest title for strict data information route'
Swap $editor @'
            "Formats & formulas · copy / paste",()=>{
'@ @'
            "Format / Formula mappings · copy / paste",()=>{
'@ 'formats mapping main copy route'
Swap $editor @'
            Header="Data mappings · values",Content=content
'@ @'
            Header="Information / Data · header match",Content=content
'@ 'strict data tab'
Swap $editor @'
            Header="Formats & formulas · separate library",
'@ @'
            Header="Format / Formula mappings · separate library",
'@ 'format mappings separate tab'

# Never offer manual/selected-cell VALUE destination, even in More options.
# Existing value overwrite toggle is untouched; all writes still require
# unique header-signature verification and the verified Excel preview.
Swap $editor @'
        options.Children.Add(manual);options.Children.Add(overwrite);
'@ @'
        // Data/information paste is header verified ONLY; the manual target
        // checkbox exists for historical UI compatibility but is not shown.
        manual.IsChecked=false;
        options.Children.Add(overwrite);
'@ 'remove advanced selected-cell data override'

ReplaceSpan $editor @'
        // Header-based paste and user-anchored paste are two explicit
'@ @'
        footer.Children.Add(pasteButtons);
'@ @'
        // Information/data paste ONLY: header signature match, else BLOCK.
        // No manual destination, no prompt to paste despite mismatched labels.
        pasteButtons.Children.Add(RequiresExcel(
            "Preview verified data headers",()=>{
                if(manual!=null)manual.IsChecked=false;
                Run(Preview);
            }));
        pasteButtons.Children.Add(RequiresExcel(
            "Paste matching information",()=>{
                if(manual!=null)manual.IsChecked=false;
                Run(Apply);
            },true));
        footer.Children.Add(HintV42(
            "DATA / INFORMATION: paste only when the saved header / value "+
            "reference signature matches this worksheet uniquely. If headers "+
            "do not match, block. There is no selected-cell fallback. "+
            "Formats and formulas use a different mapping page.",11));
'@ 'data paste strict controls only'

# Reject every manual data preview at the engine boundary. This protects
# other widget/Workspace callers and is intentionally BEFORE any Excel COM.
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
Swap $bridge @'
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        dynamic app=Excel();
'@ @'
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        // R125: values/information are NEVER pasted without a uniquely
        // matched saved header signature. Manual Excel selection is reserved
        // for the entirely different format/formula mapper (V109).
        if(useSelection)
            throw new InvalidOperationException(
                "Information paste requires verified matching headers. "+
                "Selected-cell fallback is disabled for data.");
        dynamic app=Excel();
'@ 'no selected-cell bypass for any value writer'

# Restore the strict existing wheel data route. R124's mismatch prompt was
# applied to information paste by mistake. The formatting mapper already
# uses its own selected-range, header-independent format-only engine.
$wheel="$root/DeskSpaceExcelWheelV42.cs"
ReplaceSpan $wheel @'
            // First try the verified header/value mapping signature.
'@ @'
            int ready=preview.Changes.Count(
'@ @'
            // DATA/INFORMATION values: a unique saved header signature is
            // mandatory; mismatch/ambiguity BLOCKS. No fallback, no prompts.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
                selection:false,overwrite:true);
            if(!preview.Verified)
                throw new InvalidOperationException(
                    "The open Excel sheet does not uniquely match the saved "+
                    "reference headers. Data paste blocked; nothing changed. "+
                    preview.Reason);
'@ 'remove R124 information mismatch fallback'

ReplaceSpan $wheel @'
            if(selectedCellPasteV124)
'@ @'
            if(disposed)return;
'@ @'
            Status("Excel paste · "+entry.Name,
                "Matched "+preview.SheetName+". Backing up workbook and "+
                "pasting "+ready+" verified information values…");
            applying=true;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                preview,entry,overwrite:true);
'@ 'restore strict header-matched writer'

# Old WPF button expectations should track the two distinct routes, not
# silently revert the UI to a shared capture/paste button.
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            "Data mappings · copy / paste",
'@ @'
            "Information / Data · matched headers",
'@ 'WPF assert visible strict data navigation'
Swap $smoke @'
            "Preview saved-header match",
'@ @'
            "Preview verified data headers",
'@ 'WPF assert renamed header preview'
Swap $smoke @'
            "Paste reviewed mapping values",
'@ @'
            "Paste matching information",
'@ 'WPF assert renamed information paste'

# Compiled WPF guard checks engine rejection without Excel running.
Swap $smoke @'
            TestFormatHeadersAndDeckV123();
'@ @'
            TestFormatHeadersAndDeckV123();
            TestStrictInformationPasteV125();
'@ 'wire strict value path test'
$method=@'
    private static void TestStrictInformationPasteV125()
    {
        var bridge=Product.GetType("DeskSpace.DeskSpaceExcelBridgeV16",true)!;
        var type=Product.GetType("DeskSpace.DeskSpaceExcelTemplateV16",true)!;
        var preview=bridge.GetMethod("Preview",
            BindingFlags.Public|BindingFlags.Static)!;
        var dummy=Activator.CreateInstance(type)!;
        bool rejected=false;
        try
        {
            preview.Invoke(null,new object[]{dummy,true,false});
        }
        catch(TargetInvocationException ex)
        {
            rejected=ex.InnerException is InvalidOperationException &&
                ex.InnerException.Message.Contains(
                    "requires verified matching headers",
                    StringComparison.OrdinalIgnoreCase);
        }
        Assert(rejected,
            "R125 information paste must reject selected-cell route BEFORE Excel COM");
        Console.WriteLine("R125 PASS: data requires matching headers; "+
            "format mapping remains independent.");
    }

'@
Swap $smoke @'
    private static void TestExcelTypeAwareSheetV101()
'@ ($method+'    private static void TestExcelTypeAwareSheetV101()') 'compiled strict-data guard test'

Write-Host 'R125: information paste uniquely matched headers or BLOCK, no manual fallback; independent format/formula mapping unchanged.'
