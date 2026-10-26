$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$view="$root/DeskSpaceExcelSheetViewV101.cs"
$editor="$root/DeskSpaceExcelUnifiedV126.cs"
Copy-Item 'r142/DeskSpaceExcelFormulaIntegrityV142.cs' "$root/DeskSpaceExcelFormulaIntegrityV142.cs" -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
    $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $a=$before.Replace($crlf,$lf)
    $b=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($source,[regex]::Escape($a))).Count
    if($count -ne 1){throw "R142 $($label): expected one exact anchor, found $count"}
    [IO.File]::WriteAllText($path,$source.Replace($a,$b),
        [Text.UTF8Encoding]::new($false))
}
# Never silently store a formula flag with missing actual expression.
Swap $view @'
            bool formula=Flag(()=> (object?)cell.HasFormula);
            if(formula)
            {
                style.FormulaA1=Format(()=> (object?)cell.Formula);
                style.FormulaR1C1=Format(()=> (object?)cell.FormulaR1C1);
            }
'@ @'
            bool formula=Flag(()=> (object?)cell.HasFormula);
            if(formula)
            {
                style.FormulaA1=Format(()=> (object?)cell.Formula);
                style.FormulaR1C1=Format(()=> (object?)cell.FormulaR1C1);
                if(string.IsNullOrWhiteSpace(style.FormulaA1) ||
                   string.IsNullOrWhiteSpace(style.FormulaR1C1) ||
                   !style.FormulaA1.StartsWith("=",StringComparison.Ordinal) ||
                   !style.FormulaR1C1.StartsWith("=",StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Excel formula detected at "+style.Address+
                        " but its actual A1/R1C1 expression could not be read. "+
                        "No incomplete mapping will be saved.");
            }
'@ 'reject formula marker without expression'

# The sheet keeps compact ƒx glyphs; a prominent, selectable formula bar
# reveals the ACTUAL expression (never the calculated cell value).
Swap $view @'
        var grid=new Grid{Background=Brushes.White};
'@ @'
        var firstFormula=visual.Cells.FirstOrDefault(c=>
            c.StoredType=="Formula");
        var formulaAddressV142=new TextBlock{
            Text=firstFormula==null?
                "No captured formulas":
                "Captured formulas: "+visual.Cells.Count(c=>c.StoredType=="Formula")+
                "  ·  "+firstFormula.Address,
            FontWeight=FontWeights.SemiBold,
            VerticalAlignment=VerticalAlignment.Center,
            Foreground=new SolidColorBrush(Color.FromRgb(35,65,104)),
            Margin=new Thickness(10,0,9,0)
        };
        var formulaTextV142=new TextBox{
            Text=firstFormula==null?"":firstFormula.FormulaA1,
            IsReadOnly=true,
            MinWidth=240,
            Height=28,
            Padding=new Thickness(5,3,5,3),
            ToolTip="Actual saved Excel A1 formula; select text to copy. "+
                "Click any ƒx cell to inspect its exact formula.",
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Disabled
        };
        var grid=new Grid{Background=Brushes.White};
'@ 'show actual formula above captured grid'

Swap $view @'
            border.MouseLeftButtonDown+=(_,_)=>{
                var address=item?.Address??(
                    ColumnLabel(entry.SourceColumn+localCol)+
                    (entry.SourceRow+localRow).ToString(CultureInfo.InvariantCulture));
                detail.Text=address+" · "+(item?.NumberFormat??"General")+
'@ @'
            border.MouseLeftButtonDown+=(_,_)=>{
                var address=item?.Address??(
                    ColumnLabel(entry.SourceColumn+localCol)+
                    (entry.SourceRow+localRow).ToString(CultureInfo.InvariantCulture));
                bool selectedFormula=item?.StoredType=="Formula";
                formulaAddressV142.Text=selectedFormula?
                    "Saved formula · "+address:
                    address+" · No formula in this cell";
                formulaTextV142.Text=selectedFormula && item!=null?
                    (!string.IsNullOrEmpty(item.FormulaA1)?
                        item.FormulaA1:item.FormulaR1C1):"";
                detail.Text=address+" · "+(item?.NumberFormat??"General")+
'@ 'click cell updates real formula display'

Swap $view @'
        var preview=new DockPanel{Background=Brushes.White,LastChildFill=true};
        var zoomBar=new StackPanel{Orientation=Orientation.Horizontal,
'@ @'
        var preview=new DockPanel{Background=Brushes.White,LastChildFill=true};
        var formulaBarV142=new DockPanel{
            LastChildFill=true,Margin=new Thickness(4,4,10,6)};
        DockPanel.SetDock(formulaAddressV142,Dock.Left);
        formulaBarV142.Children.Add(formulaAddressV142);
        formulaBarV142.Children.Add(formulaTextV142);
        DockPanel.SetDock(formulaBarV142,Dock.Top);
        preview.Children.Add(formulaBarV142);
        var zoomBar=new StackPanel{Orientation=Orientation.Horizontal,
'@ 'formula bar stays visible while physical sheet scrolls'

# Refuse saving an incomplete snapshot; verify actual A1/R1C1 text
# was retained after writing and reloading from the encrypted format library.
Swap $editor @'
        if(snapshot.SheetVisualV101==null)
            throw new InvalidOperationException(
                "Capture did not return a complete native Excel format snapshot.");
        // Only the structural visual survives in the format library.
'@ @'
        if(snapshot.SheetVisualV101==null)
            throw new InvalidOperationException(
                "Capture did not return a complete native Excel format snapshot.");
        DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(snapshot);
        // Only the structural visual survives in the format library.
'@ 'validate formula capture before it becomes a draft'

Swap $editor @'
        edited.Cells.Clear();edited.Anchors.Clear();
        if(records.Any(x=>x.Id!=edited.Id &&
'@ @'
        edited.Cells.Clear();edited.Anchors.Clear();
        DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(edited);
        if(records.Any(x=>x.Id!=edited.Id &&
'@ 'validate exact expressions before saving'

Swap $editor @'
        DeskSpaceExcelStoreV16.SaveFormats(records);
        formatDraftV126=null;
'@ @'
        DeskSpaceExcelStoreV16.SaveFormats(records);
        var persistedV142=DeskSpaceExcelStoreV16.LoadFormats()
            .FirstOrDefault(x=>x.Id==edited.Id);
        DeskSpaceExcelFormulaIntegrityV142.ValidateRoundtrip(
            edited,persistedV142);
        formatDraftV126=null;
'@ 'verify formula fidelity after library write'

Write-Host 'R142 exact Excel formula capture, visible formula bar, integrity gate, and save/reload verification installed. Blank cells and styling/paste policy unchanged.'
