$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
# R101/R102 have already staged the latest capture classes. Keep their
# full data model and live Excel connection; patch only the editor layout.
function Swap([string]$file,[string]$before,[string]$after,[string]$tag)
{
 $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R103 $tag: expected one integration anchor, found $n"}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
$ed="$r/DeskSpaceExcelEditorV42.cs"
Swap $ed @'
        typeTools.Children.Add(HintV42("Incoming date order:",10));
'@ @'
        // Incoming short-date order is inferred per cell from the saved
        // Excel number format; do not clutter this view with a global choice.
'@ 'remove global date-order prompt'
Swap $ed @'
        typeTools.Children.Add(dateInputOrderV102);
'@ @'
        // Retain the legacy control internally for compatibility with
        // existing normalization code, but never show it in the main UI.
'@ 'hide global date-order dropdown'
Swap $ed @'
        DockPanel.SetDock(footer,Dock.Bottom);right.Children.Add(footer);
'@ @'
        // The preview must use the whole editor height. Paste, format note
        // and type-repair tools belong in the MAPPINGS tab only.
'@ 'free preview height previously lost to bottom toolbar'
Swap $ed @'
        tabs.Items.Add(new TabItem{
            Header="Captured sheet · formats & formulas",
            Content=sheetPreviewHostV101});
        tabs.Items.Add(new TabItem{
            Header="Mappings · edit & paste",
            Content=mappingGridV42});
'@ @'
        var previewPane=new DockPanel{LastChildFill=true};
        var previewToolbar=new DockPanel{
            Margin=new Thickness(4,3,4,6)};
        var larger=ButtonV42("Expand sheet preview",()=>{
            var current=editingTemplateV42;
            if(current==null)
            {
                if(editorInfoV42!=null)
                    editorInfoV42.Text="Capture or load a mapping first.";
                return;
            }
            var enlarged=new Window{
                Title="DeskSpace · "+current.SourceSheet+" · Excel sheet preview",
                Width=Math.Max(950,SystemParameters.WorkArea.Width*0.85),
                Height=Math.Max(650,SystemParameters.WorkArea.Height*0.85),
                MinWidth=600,MinHeight=400,
                Background=Brushes.White,
                WindowStartupLocation=WindowStartupLocation.CenterScreen,
                Content=DeskSpaceExcelSheetViewV101.Create(current)
            };
            var owner=Window.GetWindow(mappingGridV42);
            if(owner!=null)enlarged.Owner=owner;
            enlarged.Show();
        });
        DockPanel.SetDock(larger,Dock.Right);
        previewToolbar.Children.Add(larger);
        previewToolbar.Children.Add(HintV42(
            "Captured Excel layout · sample placeholders, never old values",11));
        DockPanel.SetDock(previewToolbar,Dock.Top);
        previewPane.Children.Add(previewToolbar);
        previewPane.Children.Add(sheetPreviewHostV101);
        var mappingPane=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(footer,Dock.Bottom);
        mappingPane.Children.Add(footer);
        mappingPane.Children.Add(mappingGridV42);
        tabs.Items.Add(new TabItem{
            Header="Captured sheet · formats & formulas",
            Content=previewPane});
        tabs.Items.Add(new TabItem{
            Header="Mappings · edit & paste",
            Content=mappingPane});
'@ 'large sheet tab with independent expand button and secondary mappings'
Swap $ed @'
            existing.SourceRow=rowBase;existing.SourceColumn=colBase;
            editingTemplateV42=existing;
'@ @'
            // Adding fields must merge their real style snapshots as well
            // as their formulas and values. Previously the saved visual
            // kept only the first captured selection.
            existing.SheetVisualV101=DeskSpaceExcelSheetViewV101.MergeCapturedVisuals(
                existing.SheetVisualV101,shiftR,shiftC,
                candidate.SheetVisualV101,
                candidate.SourceRow-rowBase,candidate.SourceColumn-colBase,
                existing.RowCount,existing.ColumnCount);
            existing.SourceRow=rowBase;existing.SourceColumn=colBase;
            editingTemplateV42=existing;
'@ 'merge all captured styles on subsequent capture add-fields'
Write-Host 'R103: full-height spreadsheet view, expandable Excel sheet, mapped edits isolated, no global date dropdown, added ranges merge all captured styles.'
