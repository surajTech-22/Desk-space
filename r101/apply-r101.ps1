$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('DeskSpaceExcelSheetViewV101.cs','DeskSpaceExcelInputTypeV101.cs')){
 Copy-Item -LiteralPath "r101/$name" -Destination "$root/$name" -Force
}
function Modify([string]$path,[string]$before,[string]$after,[string]$description)
{
 $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R101 $description expected exactly one anchor; got $n"}
 $s=$s.Replace($before,$after)
 [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
$store="$root/DeskSpaceExcelStoreV16.cs"
Modify $store @'
    public int ColumnCount {get;set;}
'@ @'
    public int ColumnCount {get;set;}
    // Separate data-free sheet visual: formatted blanks, fonts, fills,
    // borders, merged regions, actual/expected types and formulas.
    public DeskSpaceExcelSheetVisualV101? SheetVisualV101 {get;set;}
'@ 'add independent encrypted sheet visual model'
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
Modify $bridge @'
        if(entry.Cells.Count==0)
            throw new InvalidOperationException("The selected cells contain no values to save.");
        return entry;
'@ @'
        // Capture physical Excel styles and formula metadata from the
        // actual range. The visual deliberately stores NO old input values.
        entry.SheetVisualV101=DeskSpaceExcelVisualCaptureV101.Capture(range);
        if(entry.Cells.Count==0 &&
           entry.SheetVisualV101.Cells.All(x=>x.StoredType=="Blank"))
            throw new InvalidOperationException(
                "The selected range contains no values or formulas. "+
                "Choose the intended template range, including its headings.");
        return entry;
'@ 'capture data-free sheet visual inside current STA Excel worker'
$editor="$root/DeskSpaceExcelEditorV42.cs"
Modify $editor @'
    private DataGrid? mappingGridV42;
'@ @'
    private DataGrid? mappingGridV42;
    private ContentControl? sheetPreviewHostV101;
'@ 'track sheet preview container'
Modify $editor @'
        right.Children.Add(mappingGridV42);
        var editorPanel=PanelV42(right);
'@ @'
        // The spreadsheet-style view is first: preserve Excel positioning,
        // format and formulas while never showing saved invoice/transaction
        // values. Existing editable mapping workflow remains available.
        sheetPreviewHostV101=new ContentControl{
            Content=new TextBlock{
                Text="Select cells in Excel and capture to see their sheet layout.",
                Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap}
        };
        var tabs=new TabControl{Margin=new Thickness(0,4,0,0)};
        tabs.Items.Add(new TabItem{
            Header="Captured sheet · formats & formulas",
            Content=sheetPreviewHostV101});
        tabs.Items.Add(new TabItem{
            Header="Mappings · edit & paste",
            Content=mappingGridV42});
        right.Children.Add(tabs);
        var editorPanel=PanelV42(right);
'@ 'replace default table with read-only sheet preview + mapping tab'
Modify $editor @'
        mappingGridV42.ItemsSource=mappingRowsV42.ToArray();
        if(editorInfoV42!=null)editorInfoV42.Text=
'@ @'
        mappingGridV42.ItemsSource=mappingRowsV42.ToArray();
        if(sheetPreviewHostV101!=null)
            sheetPreviewHostV101.Content=DeskSpaceExcelSheetViewV101.Create(entry);
        if(editorInfoV42!=null)editorInfoV42.Text=
'@ 'refresh sheet preview after capture and mapping load'
Modify $editor @'
            mappingRowsV42.Clear();mappingGridV42!.ItemsSource=null;
            mappingNameV42?.Clear();mappingGroupV42?.Clear();
'@ @'
            mappingRowsV42.Clear();mappingGridV42!.ItemsSource=null;
            if(sheetPreviewHostV101!=null)sheetPreviewHostV101.Content=null;
            mappingNameV42?.Clear();mappingGroupV42?.Clear();
'@ 'clear old snapshot when starting a new mapping'
Write-Host 'R101: real Excel sheet geometry, style and formulas with no prior data; capture stored vs expected types; separate original mapping tab unchanged.'
