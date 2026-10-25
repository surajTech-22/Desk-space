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
 if($n -ne 1){throw "R103 ${tag}: expected one integration anchor, found $n"}
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
# Older WPF smoke tests searched the default tab for a paste button.
# The new default is deliberately the large read-only captured sheet. Switch
# to the Mappings tab when verifying legacy mapping actions, without weakening
# any action assertion or changing the production layout.
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
    private static Button ButtonWith(DependencyObject root,string label) =>
        Walk(root).OfType<Button>().FirstOrDefault(x=>x.Content is string s&&s==label)
        ??throw new Exception("Missing button "+label);
'@ @'
    private static Button ButtonWith(DependencyObject root,string label)
    {
        Button? found=Walk(root).OfType<Button>().
            FirstOrDefault(x=>x.Content is string s && s==label);
        if(found==null && label=="Preview matching cells")
        {
            var excelTabs=Walk(root).OfType<TabControl>().FirstOrDefault(t=>
                t.Items.OfType<TabItem>().Any(item=>
                    item.Header?.ToString()=="Mappings · edit & paste"));
            if(excelTabs!=null)
            {
                var mapping=excelTabs.Items.OfType<TabItem>().First(item=>
                    item.Header?.ToString()=="Mappings · edit & paste");
                excelTabs.SelectedItem=mapping;
                if(root is FrameworkElement surface)
                {
                    surface.Measure(new Size(1000,780));
                    surface.Arrange(new Rect(0,0,1000,780));
                    surface.UpdateLayout();
                }
                found=Walk(root).OfType<Button>().
                    FirstOrDefault(x=>x.Content is string s&&s==label);
            }
        }
        return found??throw new Exception("Missing button "+label);
    }
'@ 'legacy WPF smoke must navigate to the actual mapping controls tab'

Write-Host 'R103: full-height spreadsheet view, expandable Excel sheet, mapped edits isolated, no global date dropdown, added ranges merge all captured styles.'
