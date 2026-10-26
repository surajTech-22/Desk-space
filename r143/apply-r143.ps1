$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$view="$r/DeskSpaceExcelSheetViewV101.cs"
$worker="$r/DeskSpaceExcelFormatApplyV109.cs"
Copy-Item 'r143/DeskSpaceExcelFormulaPasteV143.cs' "$r/DeskSpaceExcelFormulaPasteV143.cs" -Force
$lf=[string][char]10; $crlf=[string][char]13+$lf
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R143 $label expected one exact code anchor, found $hits"}
 [IO.File]::WriteAllText($file,$s.Replace($a,$b),
    [Text.UTF8Encoding]::new($false))
}

# R142 saved the real expressions but inserted a separate formula panel.
# Restore the original sheet structure and show the saved A1 text in EACH cell.
Swap $view @'
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
'@ @'
        var grid=new Grid{Background=Brushes.White};
'@ 'remove separate formula panel'

Swap $view @'
                // Raw cell values and long formula expressions never spill over
                // neighboring cells; exact formula remains in tooltip/details.
                Text=isFormula?"ƒx":FeaturePlaceholder(item,feature),
'@ @'
                // Show the ACTUAL captured A1 expression in its own cell.
                // Long expressions are clipped to the physical Excel cell,
                // and remain complete in the tooltip and cell inspector.
                // Original transaction values are never shown.
                Text=isFormula?
                    (item!.FormulaA1.Length>0?item.FormulaA1:item.FormulaR1C1):
                    FeaturePlaceholder(item,feature),
'@ 'replace fx with exact in-cell formula'

Swap $view @'
                bool selectedFormula=item?.StoredType=="Formula";
                formulaAddressV142.Text=selectedFormula?
                    "Saved formula · "+address:
                    address+" · No formula in this cell";
                formulaTextV142.Text=selectedFormula && item!=null?
                    (!string.IsNullOrEmpty(item.FormulaA1)?
                        item.FormulaA1:item.FormulaR1C1):"";
'@ @'
                // Cell inspection below already shows its exact A1 expression.
'@ 'remove redundant formula panel selection handler'

Swap $view @'
        var formulaBarV142=new DockPanel{
            LastChildFill=true,Margin=new Thickness(4,4,10,6)};
        DockPanel.SetDock(formulaAddressV142,Dock.Left);
        formulaBarV142.Children.Add(formulaAddressV142);
        formulaBarV142.Children.Add(formulaTextV142);
        DockPanel.SetDock(formulaBarV142,Dock.Top);
        preview.Children.Add(formulaBarV142);
'@ @'
'@ 'remove detached bar from layout'

# The strict existing gate is unchanged. An additional permitted case
# requires *every* real input cell matched, all formula slots blank, all
# other saved blank cells blank. No arbitrary partial ranges are accepted.
Swap $worker @'
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        if(!decision.MayApply)problems.Add(decision.Explanation);
'@ @'
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        if(!decision.MayApply &&
           DeskSpaceExcelFormulaPasteV143.Ready(order,to))
            decision=new DeskSpaceExcelFullRangeGateV107.Decision(
                DeskSpaceExcelFullRangeGateV107.Status.CompleteMatchingLayout,
                to.Count,to.Count(c=>c.HasData),from.Count(c=>c.HasData),
                DeskSpaceExcelFormulaPasteV143.Explanation);
        if(!decision.MayApply)problems.Add(decision.Explanation);
'@ 'recognise complete-input missing-formula layout'

Swap $worker @'
                      "Apply cell-specific formatting. Existing values and formulas retained.")
'@ @'
                      "Apply cell-specific formatting. "+
                      (decision.Explanation==DeskSpaceExcelFormulaPasteV143.Explanation?
                          "Restore only saved formulas into verified empty formula cells; "+
                          "retain all user-entered inputs.":
                          "Existing values and formulas retained."))
'@ 'make missing-formula action clear in the preview'

Swap $worker @'
            bool originallyEmpty=live.Review.Gate.State==
                DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
            BuildMissingStructure(live,originallyEmpty);
            WriteSnapshot(live.Range,live.Visual,true);
            if(originallyEmpty)
                KeepBlankDestinationValuesV136(live);
'@ @'
            bool originallyEmpty=live.Review.Gate.State==
                DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
            bool restoreFormulasV143=
                live.Review.Gate.Explanation==
                    DeskSpaceExcelFormulaPasteV143.Explanation;
            BuildMissingStructure(live,originallyEmpty);
            WriteSnapshot(live.Range,live.Visual,true);
            if(originallyEmpty)
                KeepBlankDestinationValuesV136(live);
            else if(restoreFormulasV143)
                DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(
                    live.Range,live.Visual.Cells);
'@ 'paste exactly captured formulas only after backup and matching-input gate'

Swap $worker @'
                else if(signature.Formula!=(IsFormula(cell)?Text(cell.FormulaR1C1):"") ||
                        signature.Value!=Text((object?)cell.Value2))
                    throw new InvalidDataException(
                        "An existing Excel value/formula changed unexpectedly.");
'@ @'
                else if(restoreFormulasV143 && s.StoredType=="Formula" &&
                        signature.Formula.Length==0 && signature.Value.Length==0)
                {
                    if(!IsFormula(cell) || Text(cell.FormulaR1C1)!=s.FormulaR1C1)
                        throw new InvalidDataException(
                            "Inserted formula readback did not exactly match "+
                            "saved R1C1 expression at "+Text(cell.Address));
                }
                else if(signature.Formula!=(IsFormula(cell)?Text(cell.FormulaR1C1):"") ||
                        signature.Value!=Text((object?)cell.Value2))
                    throw new InvalidDataException(
                        "An existing Excel value/formula changed unexpectedly.");
'@ 'verify inserted R1C1 expressions and preserve other business values'

Swap $worker @'
            if(originallyEmpty)live.Range.ClearContents();
        }
        WriteSnapshot(live.Range,before,true);
'@ @'
            if(originallyEmpty)live.Range.ClearContents();
            else
            {
                // In formula-ready mode, only previously blank source slots
                // may have received new content. On failure, remove new
                // content from those slots, never from pre-existing inputs.
                foreach(var prior in before.Cells.Where(c=>c.StoredType=="Blank"))
                {
                    dynamic cell=live.Range.Cells[prior.Row+1,prior.Column+1];
                    if((bool)cell.MergeCells)
                    {
                        dynamic region=cell.MergeArea;
                        if(ToInt(region.Row)!=live.FirstRow+prior.Row ||
                           ToInt(region.Column)!=live.FirstColumn+prior.Column)
                            continue;
                    }
                    if(IsFormula(cell) || (object?)cell.Value2!=null)
                        cell.ClearContents();
                }
            }
        }
        WriteSnapshot(live.Range,before,true);
'@ 'safely roll back only contents of previously blank cells'

Write-Host 'R143: in-cell exact formula preview, no detached formula bar, formula paste only when all saved inputs match and formula destinations are blank. Fully empty sheets still have no formulas/zeros. Never overwrite existing formulas, backups and rollback retained.'
