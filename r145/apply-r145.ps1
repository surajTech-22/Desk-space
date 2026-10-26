$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$paste="$r/DeskSpaceExcelPasteSpecialV144.cs"
$editor="$r/DeskSpaceExcelUnifiedV126.cs"
Copy-Item 'r145/DeskSpaceExcelZeroIntegrityV145.cs' "$r/DeskSpaceExcelZeroIntegrityV145.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$p,[string]$before,[string]$after,[string]$label)
{
  $s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
  $old=$before.Replace($crlf,$lf);$new=$after.Replace($crlf,$lf)
  $count=([regex]::Matches($s,[regex]::Escape($old))).Count
  if($count -ne 1){throw "R145 $label: required one anchor; found $count"}
  [IO.File]::WriteAllText($p,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
}
# Back up every original cell in the selected footprint. Excel can
# auto-fill calculated table columns; clearing just the cells written
# explicitly left the auto-filled (often zero-valued) formulas behind.
Swap $paste @'
                foreach(var item in wrote)
                {
                    if(!byPosition.TryGetValue((item.Row,item.Column),out var prior) ||
                       prior.HasFormula || prior.Value!=null)continue;
                    dynamic cell=target.Cells[item.Row+1,item.Column+1];
                    cell.ClearContents();
                }
'@ @'
                // Revert every originally empty physical cell that Excel
                // changed (including automatic calculated-column fill).
                // Existing values and formulas are never cleared.
                foreach(var prior in original)
                {
                    if(prior.HasFormula || prior.Value!=null)continue;
                    dynamic cell=target.Cells[prior.Row+1,prior.Column+1];
                    if((bool)cell.HasFormula || (object?)cell.Value2!=null)
                    {
                        if((bool)cell.MergeCells)
                        {
                            dynamic merged=cell.MergeArea;
                            if(Number((object?)merged.Row)!=firstRow+prior.Row ||
                               Number((object?)merged.Column)!=firstCol+prior.Column)
                                continue;
                        }
                        cell.ClearContents();
                    }
                }
'@ 'rollback all Excel-generated contents in originally empty positions'

# Re-check that unrelated cells remain identical, including no Excel-created
# literal zero. Count formula results separately: =SUM(blank) calculates 0
# but is not an actual input value stored in the cell.
Swap $paste @'
            foreach(var was in original)
            {
                dynamic cell=target.Cells[was.Row+1,was.Column+1];
                bool nowFormula=(bool)cell.HasFormula;
                var nowExpression=nowFormula?Text((object?)cell.FormulaR1C1):"";
'@ @'
            var observedV145=new List<DeskSpaceExcelZeroIntegrityV145.CellState>();
            foreach(var was in original)
            {
                dynamic cell=target.Cells[was.Row+1,was.Column+1];
                bool nowFormula=(bool)cell.HasFormula;
                var nowExpression=nowFormula?Text((object?)cell.FormulaR1C1):"";
                var nowValue=(object?)cell.Value2;
                observedV145.Add(new(was.HasFormula,was.Value,nowFormula,
                    nowValue,was.FormulaR1C1,nowExpression));
'@ 'audit literal values versus formula-calculated zero results'

Swap $paste @'
                else if(was.HasFormula!=nowFormula ||
                        (was.HasFormula &&
                         !string.Equals(was.FormulaR1C1,nowExpression,
                            StringComparison.Ordinal)) ||
                        (!was.HasFormula &&
                         !Equals(was.Value,(object?)cell.Value2)))
'@ @'
                else if(was.HasFormula!=nowFormula ||
                        (was.HasFormula &&
                         !string.Equals(was.FormulaR1C1,nowExpression,
                            StringComparison.Ordinal)) ||
                        DeskSpaceExcelZeroIntegrityV145.HasUnexpectedConstant(
                            observedV145[observedV145.Count-1]))
'@ 'reject Excel generated literals as well as changed formulas'

Swap $paste @'
            return "Paste Special · Formulas verified: "+
                planned.Assignments.Length+" full formulas inserted; "+
                planned.AlreadyPresent+" already matching. "+
                "No source values copied; other cells preserved. "+
                "Excel will calculate formula results normally. "+
                "Safety backup: "+copy;
'@ @'
            int zeroResultsV145=DeskSpaceExcelZeroIntegrityV145.
                FormulaZeroCount(observedV145);
            return "Paste Special · Formulas verified: "+
                planned.Assignments.Length+" full formulas inserted; "+
                planned.AlreadyPresent+" already matching. "+
                "0 constant input values copied. All other cells preserved. "+
                DeskSpaceExcelZeroIntegrityV145.ZeroExplanation(zeroResultsV145)+
                " Safety backup: "+copy;
'@ 'distinguish actual constants from Excel-calculated zero values'

# The independent formula command must explain to the user exactly when a
# mapping was NOT saved. Draft formulas in the preview are NOT yet persisted.
Swap $editor @'
        if(status!=null)status.Text=
            "Format copied into an unsaved draft. Inspect it, then Save Mapping.";
'@ @'
        if(status!=null)status.Text=
            "UNSAVED DRAFT · "+snapshot.SheetVisualV101.Cells.Count(c=>
                c.StoredType=="Formula")+
            " actual formulas captured; 0 source values saved. "+
            "Select Save Mapping to persist and verify.";
'@ 'accurate unsaved formula capture status'

Swap $editor @'
        if(status!=null)status.Text="Saved '"+title+"' to the format mapping library.";
'@ @'
        if(status!=null)
        {
            var verified=selectedFormatV121?.SheetVisualV101 ??
                throw new InvalidDataException(
                    "Saved mapping could not be reloaded. No success reported.");
            status.Text="SAVED & RELOADED '"+title+"' · "+
                verified.Cells.Count(c=>c.StoredType=="Formula")+
                " complete formula expressions; 0 source input values saved. "+
                "Use Mappings → Paste Special · Formulas to paste formulas "+
                "(different from formatting-only paste).";
        }
'@ 'show saved actual formula count rather than ambiguous save success'

Write-Host 'R145: eliminate leaked Excel auto-filled zero cells after failed formula paste; validate literal data unchanged and separately report Excel-calculated zeros. Saved formula counts verified at UI.'
