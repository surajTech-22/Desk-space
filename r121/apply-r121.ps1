$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r121/DeskSpaceExcelFormatStoreV121.cs' -Destination "$r/DeskSpaceExcelFormatStoreV121.cs" -Force
Copy-Item -LiteralPath 'r121/DeskSpaceExcelFormatsV121.cs' -Destination "$r/DeskSpaceExcelFormatsV121.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$label)
{
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R121 $($label): anchor found $n times"}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),
    [Text.UTF8Encoding]::new($false))
}
$ui="$r/DeskSpaceExcelEditorV42.cs"
Swap $ui '        editButtons.Children.Add(ButtonV42("Save collection & data",' @'
        editButtons.Children.Add(ButtonV42("Save header/value mapping",
'@ 'clarify value-only save'
Swap $ui @'
        editorHead.Children.Add(editButtons);
        DockPanel.SetDock(editorHead,Dock.Top);right.Children.Add(editorHead);
'@ @'
        editorHead.Children.Add(editButtons);
        // Separate collapsed formatting editor; the existing mapping table
        // and its Save remain strictly header/value data.
        editorHead.Children.Add(BuildFormatControlsV121());
        DockPanel.SetDock(editorHead,Dock.Top);right.Children.Add(editorHead);
'@ 'embed independent format UI'
Swap $ui @'
        pasteButtons.Children.Add(RequiresExcel("Apply autofill",()=>Run(Apply),true));
        // Deliberately separate from value paste: this never copies source values.
        pasteButtons.Children.Add(RequiresExcel(
            "Check selected formatting destination",PreviewFormatV109));
        pasteButtons.Children.Add(RequiresExcel(
            "Apply selected format",ApplyFormatV109,true));
'@ @'
        pasteButtons.Children.Add(RequiresExcel(
            "Apply header/value mapping",()=>Run(Apply),true));
'@ 'remove format buttons from value paste'
Swap $ui @'
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
'@ @'
    private DeskSpaceExcelTemplateV16 FormatSourceV109()
    {
        // Format-only actions may ONLY receive templates from the dedicated
        // formats-v121.dpapi file, never the header/value library.
        return SelectedFormatV121();
    }
'@ 'strict separate format source'
Write-Host 'R121: independent encrypted formats/formulas store and UI; header/value save and autofill unchanged.'
