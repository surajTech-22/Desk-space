$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$file,[string]$old,[string]$new,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $text=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($text,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R121 $($label) expected one match, got $count"}
 [IO.File]::WriteAllText($file,$text.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$store="$r/DeskSpaceExcelStoreV16.cs"
# Keep the existing templates.dpapi and its backwards compatible public
# Load/Save API completely untouched.  Formats get a separate DPAPI file,
# same Windows-user encryption and atomic rollback technique.
$methods=@'
    private static readonly string FormatPathName=Path.Combine(
        Folder,"formats.dpapi");
    public static List<DeskSpaceExcelTemplateV16> LoadFormats()
    {
        if(!File.Exists(FormatPathName))return new();
        var decoded=Crypt(File.ReadAllBytes(FormatPathName),true);
        try {
            return JsonSerializer.Deserialize<List<DeskSpaceExcelTemplateV16>>(
                Encoding.UTF8.GetString(decoded)) ?? new();
        }
        finally {Array.Clear(decoded,0,decoded.Length);}
    }
    public static void SaveFormats(List<DeskSpaceExcelTemplateV16> formats)
    {
        Directory.CreateDirectory(Folder);
        if(formats.Any(x=>x.SheetVisualV101==null))
            throw new InvalidDataException("A saved Excel format has no visual snapshot.");
        var plaintext=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(formats));
        byte[] encrypted;
        try {encrypted=Crypt(plaintext,false);}
        finally {Array.Clear(plaintext,0,plaintext.Length);}
        var temp=FormatPathName+".tmp";
        File.WriteAllBytes(temp,encrypted);
        if(File.Exists(FormatPathName))
            File.Copy(FormatPathName,FormatPathName+".previous",true);
        File.Move(temp,FormatPathName,true);
    }
'@
# Insert helpers as last methods, without touching original value store.
$txt=[IO.File]::ReadAllText($store)
$last=$txt.LastIndexOf('}')
if($last -lt 0){throw 'R121 original store class not closed'}
[IO.File]::WriteAllText($store,$txt.Substring(0,$last)+$methods+
    $txt.Substring($last),[Text.UTF8Encoding]::new($false))
$editor="$r/DeskSpaceExcelEditorV42.cs"
Swap $editor @'
    private DeskSpaceExcelFormatApplyV109.Review? pendingFormatReviewV109;
'@ @'
    private readonly List<DeskSpaceExcelTemplateV16> formatLibraryV121=new();
    private ComboBox? formatPickerV121;
    private DeskSpaceExcelTemplateV16? selectedFormatV121;
    private DeskSpaceExcelFormatApplyV109.Review? pendingFormatReviewV109;
'@ 'separate format list'
Swap $editor @'
        footer.Children.Add(pasteButtons);
'@ @'
        footer.Children.Add(pasteButtons);
        // Different source, store, controls, and destination gate. These
        // controls never call SaveEditorV42 / Apply (header-value mappings).
        var formatGroup=new StackPanel{Margin=new Thickness(4,8,4,7)};
        formatGroup.Children.Add(HintV42(
            "FORMATS & FORMULAS ONLY (no saved cell values)",12));
        formatPickerV121=new ComboBox{MinWidth=230,MinHeight=29,
            DisplayMemberPath="Name",Margin=new Thickness(1,3,3,4)};
        formatPickerV121.SelectionChanged+=(_,_)=>{
            selectedFormatV121=formatPickerV121.SelectedItem
                as DeskSpaceExcelTemplateV16;
            pendingFormatReviewV109=null;
            formatTemplateIdV109=Guid.Empty;
        };
        formatGroup.Children.Add(formatPickerV121);
        var formatControls=new WrapPanel();
        formatControls.Children.Add(RequiresExcel(
            "Capture formats & formulas",()=>Run(CaptureFormatV121),true));
        formatControls.Children.Add(RequiresExcel(
            "Preview format destination",PreviewFormatV109));
        formatControls.Children.Add(RequiresExcel(
            "Apply formats & formulas",ApplyFormatV109,true));
        formatControls.Children.Add(ButtonV42(
            "Delete saved format",()=>Run(DeleteFormatV121)));
        formatGroup.Children.Add(formatControls);
        footer.Children.Add(formatGroup);
        ReloadFormatsV121();
'@ 'separate formatting controls'
Swap $editor @'
        // Deliberately separate from value paste: this never copies source values.
        pasteButtons.Children.Add(RequiresExcel(
            "Check selected formatting destination",PreviewFormatV109));
        pasteButtons.Children.Add(RequiresExcel(
            "Apply selected format",ApplyFormatV109,true));
'@ @'
        // Formatting controls use the dedicated independent formats library
        // below, not the header/value preview and autofill buttons.
'@ 'remove old shared paste toolbar'
Swap $editor @'
    private DeskSpaceExcelTemplateV16 FormatSourceV109()
'@ @'
    private void ReloadFormatsV121(Guid? selectId=null)
    {
        var keep=selectId??selectedFormatV121?.Id;
        var entries=DeskSpaceExcelStoreV16.LoadFormats();
        formatLibraryV121.Clear();
        formatLibraryV121.AddRange(entries);
        if(formatPickerV121==null)return;
        formatPickerV121.ItemsSource=null;
        formatPickerV121.ItemsSource=formatLibraryV121
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        formatPickerV121.SelectedItem=formatLibraryV121
            .FirstOrDefault(x=>x.Id==keep);
    }
    private void CaptureFormatV121()
    {
        var title=mappingNameV42?.Text.Trim();
        if(string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException(
                "Enter a format template name in Data name first.");
        var captured=DeskSpaceExcelBridgeV16.Capture(
            title,range?.Text??"",true);
        if(captured.SheetVisualV101==null)
            throw new InvalidOperationException(
                "Source range has no complete formatting snapshot.");
        // Keep the type/occupancy/real table/formula geometry INSIDE visual,
        // but do not copy the original source's cell values or signatures.
        captured.Cells.Clear();
        captured.Anchors.Clear();
        var current=DeskSpaceExcelStoreV16.LoadFormats();
        if(current.Any(x=>x.Name.Equals(title,
            StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "A format with this name exists. Rename the format first.");
        current.Add(captured);
        DeskSpaceExcelStoreV16.SaveFormats(current);
        ReloadFormatsV121(captured.Id);
        if(status!=null)status.Text=
            "Saved formats/formulas only to separate encrypted library: "+
            title+" ("+captured.SheetVisualV101.Rows+"×"+
            captured.SheetVisualV101.Columns+"). Original values not stored.";
    }
    private void DeleteFormatV121()
    {
        var old=selectedFormatV121 ??
            throw new InvalidOperationException("Choose a saved format first.");
        if(MessageBox.Show("Delete saved FORMAT '"+old.Name+"'?",
            "DeskSpace formats",MessageBoxButton.YesNo,
            MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        var current=DeskSpaceExcelStoreV16.LoadFormats();
        if(current.RemoveAll(x=>x.Id==old.Id)!=1)
            throw new InvalidOperationException(
                "Saved format has changed; refresh the format list.");
        DeskSpaceExcelStoreV16.SaveFormats(current);
        selectedFormatV121=null;
        pendingFormatReviewV109=null;
        ReloadFormatsV121();
    }
    private DeskSpaceExcelTemplateV16 FormatSourceV109()
'@ 'separate capture and format save logic'
Swap $editor @'
        if(hasUnsavedChangesV42)throw new InvalidOperationException(
            "Save your edited mapping before checking a destination.");
        if(editingTemplateV42==null)
            throw new InvalidOperationException("Select a captured Excel format first.");
        if(editingTemplateV42.SheetVisualV101==null)
'@ @'
        if(selectedFormatV121==null)
            throw new InvalidOperationException(
                "Choose a saved formats/formulas template from the separate list.");
        if(selectedFormatV121.SheetVisualV101==null)
'@ 'avoid header/value editor in format preview'
Swap $editor @'
        return editingTemplateV42;
'@ @'
        return selectedFormatV121;
'@ 'source from format library only'
Write-Host 'R121 separate encrypted stores, controls, capture and preview/apply for formats/formulas; original header/value methods untouched.'
