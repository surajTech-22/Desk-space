$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r266/DeskSpaceExcelFormulaPlanV266.cs' -Destination "$root/DeskSpaceExcelFormulaPlanV266.cs" -Force
$p="$root/DeskSpaceExcelFormatApplyV109.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R266 $($label): expected exactly 1 anchor, got $n"}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
        internal required int FirstColumn;
'@ @'
        internal required int FirstColumn;
        internal required IReadOnlyList<DeskSpaceExcelFormulaPlanV266.Placement>
            FormulaPlanV266;
'@ 'retain verified formula plan in inspected target'

Swap @'
        if(order.Any(c=>!mergedChildren.Contains((c.Row,c.Column)) &&
'@ @'
        // Restore actual recorded formulas when FORMATS+FORMULAS is pasted
        // onto a wholly blank target. This is strictly cell-position based:
        // no source body values, empty source values, or synthetic zeros.
        // Validate all formula metadata and header/merge topology before
        // calling SaveCopyAs or writing ANY Excel cells.
        IReadOnlyList<DeskSpaceExcelFormulaPlanV266.Placement> formulaPlanV266=
            Array.Empty<DeskSpaceExcelFormulaPlanV266.Placement>();
        try
        {
            var inputsV266=order.Select(cell=>
                new DeskSpaceExcelFormulaPlanV266.Input(
                    cell.Row,cell.Column,cell.StoredType,cell.FormulaR1C1,
                    mergedChildren.Contains((cell.Row,cell.Column)),
                    features.Tables.Any(t=>t.HasHeaders && cell.Row==t.Row &&
                        cell.Column>=t.Column &&
                        cell.Column<t.Column+t.ColumnCount)))
                .ToArray();
            formulaPlanV266=DeskSpaceExcelFormulaPlanV266.Build(
                inputsV266,rows,cols);
        }
        catch(InvalidDataException)
        {
            problems.Add("Recorded formulas or positions are invalid. "+
                "Recapture the template before pasting.");
        }
        if(order.Any(c=>!mergedChildren.Contains((c.Row,c.Column)) &&
'@ 'exact preflight formula layout and source integrity'

Swap @'
            FirstRow=sr,FirstColumn=sc,
'@ @'
            FirstRow=sr,FirstColumn=sc,
            FormulaPlanV266=formulaPlanV266,
'@ 'pass only preflight-approved formulas to writer'

Swap @'
                      " source formulas stay saved in the library, but none "+
                      "are inserted into empty cells. All inputs stay blank."
'@ @'
                      " saved R1C1 formulas will be inserted into their "+
                      "original positions. Other input cells stay blank."
'@ 'honest user preview for combined format/formula route'

Swap @'
    // Excel may automatically fill calculated ListObject columns while the
'@ @'
    // R266: reinsert only verified captured R1C1 formulas after R136 has
    // removed Excel-generated calculated-column contents. Never copy a
    // source literal, an artificial zero, or a formula onto existing data.
    private static void RestoreCapturedFormulasV266(Checked live)
    {
        foreach(var item in live.FormulaPlanV266)
        {
            dynamic cell=live.Range.Cells[item.Row+1,item.Column+1];
            if(IsFormula(cell) || (object?)cell.Value2!=null)
                throw new InvalidDataException(
                    "Formula destination was changed during formatting.");
            cell.FormulaR1C1=item.FormulaR1C1;
        }
    }

    // Excel may automatically fill calculated ListObject columns while the
'@ 'real relative Excel formula insertion on verified blank cells'

Swap @'
            if(originallyEmpty)
                KeepBlankDestinationValuesV136(live);
'@ @'
            if(originallyEmpty)
            {
                KeepBlankDestinationValuesV136(live);
                RestoreCapturedFormulasV266(live);
            }
'@ 'keep data blank first, then restore captured formulas only'

Swap @'
                    // No formula is allowed on an originally blank input cell:
                    // Excel calculated-column autofill can silently return 0.
                    if(IsFormula(cell) ||
                       header!=Text((object?)cell.Value2))
                        throw new InvalidDataException(
                            "A blank format-only destination now contains an "+
                            "unexpected value, formula or table header at "+
                            Text(cell.Address)+". No input values may be pasted.");
'@ @'
                    // Formula cells must contain the EXACT captured relative
                    // R1C1 expression, not Excel's displayed calculated Value2.
                    // A formula may legitimately calculate 0; we never insert
                    // literal numeric zero into any non-formula blank cell.
                    var recordedV266=live.FormulaPlanV266.FirstOrDefault(x=>
                        x.Row==s.Row && x.Column==s.Column);
                    if(recordedV266!=null)
                    {
                        if(!DeskSpaceExcelFormulaPlanV266.Matches(recordedV266,
                            IsFormula(cell),Text((object?)cell.FormulaR1C1)))
                            throw new InvalidDataException(
                                "Captured Excel formula readback failed at "+
                                Text(cell.Address));
                    }
                    else if(IsFormula(cell) ||
                            header!=Text((object?)cell.Value2))
                        throw new InvalidDataException(
                            "An unexpected non-formula input or auto-filled "+
                            "formula appeared at "+Text(cell.Address)+
                            ". No input values may be pasted.");
'@ 'read back exact formulas and enforce all other cells blank'

Swap @'
                (originallyEmpty?"Blank input cells stayed blank. ":
'@ @'
                (originallyEmpty?"Blank input cells stayed blank. "+
                    live.FormulaPlanV266.Count+
                    " saved formulas restored and read back. ":
'@ 'report actual formula insertion, not metadata-only capture'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R266: restore exact captured Excel R1C1 formulas on fully blank destination after clearing generated values; verify formulas and nonformula blanks with original backup and rollback retained.'
