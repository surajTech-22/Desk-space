$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Edit([string]$path,[string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
    if(!$s.Contains($old)){throw "R39 missing source anchor in $path : $($old.Substring(0,[Math]::Min($old.Length,110)))"}
    $s=$s.Replace($old,$new)
    [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
# Extend existing DPAPI record format; missing fields on older records are
# intentionally given safe defaults to preserve historical Excel mappings.
$store="$root/DeskSpaceExcelStoreV16.cs"
Edit $store '    public string NumberFormat {get;set;}="";' @'
    public string NumberFormat {get;set;}="";
    public string Role {get;set;}="Value"; // Reference, Value, Formula, Ignore
    public string FixedOrVariable {get;set;}="Review";
    public string CellAddress {get;set;}="";
    public string HeaderCellAddress {get;set;}="";
    public bool AddressVerified {get;set;}=true;
    public double ImageX {get;set;}
    public double ImageY {get;set;}
    public double ImageWidth {get;set;}
    public double ImageHeight {get;set;}
    public bool IsMerged {get;set;}
    public string MergedArea {get;set;}="";
'@
Edit $store '    public string ScreenshotSelection {get;set;}="";' @'
    public string ScreenshotSelection {get;set;}="";
    public string SourceKind {get;set;}="Live Excel";
    public bool CoordinatesVerified {get;set;}=true;
'@
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
Edit $bridge '    public static void OpenWorkbook(string path)' @'
    public static bool ExcelCopyActive()
    {
        try{
            dynamic app=Excel();
            var state=(object?)app.CutCopyMode;
            return state!=null && Convert.ToInt32(state,
                CultureInfo.InvariantCulture)==1;
        }
        catch{return false;}
    }
    public static void OpenWorkbook(string path)
'@
Edit $bridge '            SourceRow=row,SourceColumn=column,' @'
            SourceRow=row,SourceColumn=column,
            SourceKind="Live Excel",CoordinatesVerified=true,
'@
Edit $bridge @'
                    NumberFormat=Text(cell.NumberFormat)
                };
                if(hasFormula&&includeFormulas)
'@ @'
                    NumberFormat=Text(cell.NumberFormat),
                    CellAddress=Text(cell.Address),
                    AddressVerified=true
                };
                if(hasFormula)
'@
Edit $bridge @'
                else record.ValueKind="text";
                entry.Cells.Add(record);
'@ @'
                else record.ValueKind="text";
                bool bold=false,merged=false;
                try{bold=Convert.ToBoolean((object)cell.Font.Bold);}catch{}
                try{merged=Convert.ToBoolean((object)cell.MergeCells);}catch{}
                record.IsMerged=merged;
                if(merged)try{record.MergedArea=Text(cell.MergeArea.Address);}catch{}
                record.Role=hasFormula?"Formula":
                    (raw is string && (bold || r==1))?"Reference":"Value";
                record.FixedOrVariable=hasFormula?"Calculated":
                    record.Role=="Reference"?"Fixed label":"Variable input (review)";
                entry.Cells.Add(record);
'@
Edit $bridge @'
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        dynamic app=Excel();
'@ @'
    public static DeskSpaceExcelProposalV16 Preview(
        DeskSpaceExcelTemplateV16 entry,bool useSelection,bool overwrite)
    {
        if(!entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Screenshot text has no verified Excel cell addresses. "+
                "Link the screenshot to a live Excel selection before pasting.");
        dynamic app=Excel();
'@
Edit $bridge @'
            if(!verified)decision="Unconfirmed target";
            else if(destinationFormula)decision="Existing formula protected";
'@ @'
            if(!verified)decision="Unconfirmed target";
            else if(source.Role!="Value" || source.ValueKind=="formula")
                decision="Reference or formula (analysis only)";
            else if(destinationFormula)decision="Existing formula protected";
'@
Edit $bridge @'
        if(!preview.Verified)throw new InvalidOperationException(
            "Target cells are not verified. Select the first destination cell and preview again.");
'@ @'
        if(!preview.Verified || !entry.CoordinatesVerified)
            throw new InvalidOperationException(
                "Target cells are not verified. Link a live Excel range and preview again.");
'@
Write-Host 'R39: compatible DPAPI records, exact live Excel metadata, source formulas protected, screenshot verification gate.'
