$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$format="$r/DeskSpaceExcelFormatApplyV109.cs"
$wheel="$r/DeskSpaceExcelWheelV42.cs"
$store="$r/DeskSpaceExcelStoreV16.cs"
Copy-Item 'r146/DeskSpaceExcelTempCleanupV146.cs' "$r/DeskSpaceExcelTempCleanupV146.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
    $body=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($body,[regex]::Escape($a))).Count
    if($count -ne 1){throw "R146 $label expected exactly one source match; got $count"}
    [IO.File]::WriteAllText($path,$body.Replace($a,$b),
        [Text.UTF8Encoding]::new($false))
}
# R132 already removed created.Name=table.Name so Excel generates a UNIQUE
# workbook-wide name for each ListObject. Remove the conflicting old preflight
# that still insists the newly generated table must reuse source Table1.
Swap $format @'
        if(targetTableCount==0 && (allEmpty || fullData))
        {
            // Names are workbook-global. Reject collisions before any write.
            try
            {
                dynamic workbookSheets=app.ActiveWorkbook.Worksheets;
                for(int sheetNo=1;sheetNo<=ToInt(workbookSheets.Count);sheetNo++)
                {
                    dynamic other=workbookSheets[sheetNo];
                    dynamic tables=other.ListObjects;
                    for(int i=1;i<=ToInt(tables.Count);i++)
                        if(features.Tables.Any(t=>string.Equals(t.Name,
                            Text(tables[i].Name),StringComparison.OrdinalIgnoreCase)))
                            problems.Add("Excel Table name already exists in workbook.");
                }
            }
            catch(COMException){problems.Add("Could not verify workbook-wide Excel Table name uniqueness.");}
        }
'@ @'
        // R146: source Table1 already existing elsewhere is not a conflict.
        // R132 creates a new ListObject with Excel's own unique Name.
        // Still reject overlapping existing tables, invalid structures,
        // mismatched cells and unsafe placement using checks above.
'@ 'allow Excel-generated globally unique table names'

Swap $wheel @'
        if(disposed)return;
        bool open=ExcelIsOpen();
'@ @'
        if(disposed)return;
        // Hourly check, deletes only abandoned encrypted .tmp sidecars.
        // No Excel workbooks, recovery backups or user files are touched.
        DeskSpaceExcelTempCleanupV146.CleanIfDue();
        bool open=ExcelIsOpen();
'@ 'safe recurring mapping temp cleanup'

Swap $wheel @'
        shelfMessage.Text=visual==null?
            "This saved format has no complete Excel layout. Recapture in Workspace.":
            "Format mapping: "+visual.Rows+" × "+visual.Columns+
            " cells · "+(Group(entry).Length==0?"Ungrouped":Group(entry))+
            ". Original transaction data is not stored. Select the destination "+
            "in Excel before clicking this mapping to review full-range "+
            "eligibility. It will NEVER perform information/value paste.";
        Position();shelf.IsOpen=true;
'@ @'
        shelfMessage.Text=visual==null?
            "This saved format has no complete Excel layout. Recapture in Workspace.":
            "Format mapping: "+visual.Rows+" × "+visual.Columns+
            " cells · "+(Group(entry).Length==0?"Ungrouped":Group(entry))+
            ". Select the destination in Excel. Donut click applies FORMATS "+
            "ONLY (blank input cells stay blank). To paste actual formulas, "+
            "use the separate Paste formulas button below. No source data copied.";
        if(visual!=null && visual.Cells.Any(c=>c.StoredType=="Formula"))
        {
            var pasteFormulasV146=new Button{
                Content="ƒx  Paste saved formulas only",
                ToolTip="Excel Paste Special: formulas only, no formats or values. "+
                    "Works into blank formula cells and never overwrites user data.",
                Height=29,
                Padding=new Thickness(8,3,8,3),
                Margin=new Thickness(3,5,3,3),
                HorizontalAlignment=HorizontalAlignment.Stretch,
                Background=new SolidColorBrush(Color.FromRgb(60,90,151)),
                Foreground=Brushes.White,
                BorderThickness=new Thickness(0),
                FontWeight=FontWeights.SemiBold
            };
            pasteFormulasV146.Click+=(_,e)=>{
                e.Handled=true;
                if(!ExcelIsOpen())
                    Status("Formula paste blocked",
                        "Open the destination workbook in Microsoft Excel first.");
                else
                    _=PasteFormulasV146Async(entry);
            };
            shelfBody.Children.Add(pasteFormulasV146);
        }
        Position();shelf.IsOpen=true;
'@ 'explicit formula-only paste control on donut mapping shelf'

Swap $wheel @'
    private async System.Threading.Tasks.Task PasteFormatV130Async(
'@ @'
    // Independent from the format-only donut action and from header/value
    // information paste. Same saved format library as Workspace Mappings.
    private async System.Threading.Tasks.Task PasteFormulasV146Async(
        DeskSpaceExcelTemplateV16 entry)
    {
        if(disposed || pasting)return;
        pasting=true;
        try
        {
            Status("Paste Special · Formulas · "+entry.Name,
                "Checking selected Excel formula cells and creating safety backup…");
            var result=await DeskSpaceExcelWorkerV41.
                PasteSpecialFormulasAsync(entry);
            if(disposed)return;
            Status("Formulas pasted",result);
            successTimer.Start();
        }
        catch(Exception error)
        {
            if(!disposed)Status("Formula paste blocked",error.Message);
        }
        finally{pasting=false;}
    }
    private async System.Threading.Tasks.Task PasteFormatV130Async(
'@ 'wire actual Excel FormulaR1C1 COM paste to donut command'

# Mapping saves are atomic and use *.dpapi.tmp. Clean orphan files only
# when older than 24 hours and after the canonical encrypted file exists.
Swap $store @'
    public static List<DeskSpaceExcelTemplateV16> Load()
    {
        if(!File.Exists(PathName))return new();
'@ @'
    public static List<DeskSpaceExcelTemplateV16> Load()
    {
        DeskSpaceExcelTempCleanupV146.CleanIfDue();
        if(!File.Exists(PathName))return new();
'@ 'clean stale info mapping tmp when loading'

Swap $store @'
    public static List<DeskSpaceExcelTemplateV16> LoadFormats()
    {
        if(!File.Exists(FormatPathName))return new();
'@ @'
    public static List<DeskSpaceExcelTemplateV16> LoadFormats()
    {
        DeskSpaceExcelTempCleanupV146.CleanIfDue();
        if(!File.Exists(FormatPathName))return new();
'@ 'clean stale format mapping tmp when loading'
Write-Host 'R146 installed: safe repeated Excel table creation (Excel auto-names unique tables), explicit donut formula paste, hourly abandoned mapping temp cleanup. Workbook safety backups and open Excel files retained.'
