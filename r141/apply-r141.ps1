$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker="$r/DeskSpaceExcelFormatApplyV109.cs"
Copy-Item 'r141/DeskSpaceExcelProtectionV141.cs' "$r/DeskSpaceExcelProtectionV141.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$before,[string]$after,[string]$name){
 $s=[IO.File]::ReadAllText($worker).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R141 $($name): expected one code anchor, got $n"}
 [IO.File]::WriteAllText($worker,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap @'
            cell.Locked=feature.Locked;
            cell.FormulaHidden=feature.FormulaHidden;
'@ @'
            // These settings are security metadata. Excel sometimes rejects
            // a write on a table/range even when other formats succeed.
            // Write only when necessary; the readback below reports any
            // protection that Excel refuses to copy without weakening the
            // visual/table/value/formula gates.
            DeskSpaceExcelProtectionV141.Apply(
                cell,feature.Locked,feature.FormulaHidden);
'@ 'safe per-cell protection writer'
Swap @'
        DeskSpaceExcelSheetVisualV101 before=
            DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
        // Original stored input/formulas are never copied or changed.
'@ @'
        DeskSpaceExcelSheetVisualV101 before=
            DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range);
        var originalProtectionV141=(before.FeaturesV106?.Cells ??
            throw new InvalidDataException(
                "Cannot verify destination cell protection baseline."))
            .ToDictionary(x=>(x.Row,x.Column));
        int unavailableProtectionV141=0;
        // Original stored input/formulas are never copied or changed.
'@ 'read original security flags before mutation'
Swap @'
                if(!mergeChildren.Contains((wanted.Row,wanted.Column)) &&
                   (wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.Superscript!=observed.Superscript ||
                   wanted.Subscript!=observed.Subscript ||
                   wanted.TableRole!=observed.TableRole ||
                   wanted.Locked!=observed.Locked ||
                   wanted.FormulaHidden!=observed.FormulaHidden))
                    throw new InvalidDataException("Excel underline, table role or protection mismatch at "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
'@ @'
                if(mergeChildren.Contains((wanted.Row,wanted.Column)))
                    continue;
                if(wanted.UnderlineStyle!=observed.UnderlineStyle ||
                   wanted.Superscript!=observed.Superscript ||
                   wanted.Subscript!=observed.Subscript ||
                   wanted.TableRole!=observed.TableRole)
                    throw new InvalidDataException(
                        "Excel underline, text style or table role mismatch at "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                if(!originalProtectionV141.TryGetValue(
                    (wanted.Row,wanted.Column),out var priorFlags))
                    throw new InvalidDataException(
                        "No original protection baseline for cell "+
                        Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                // A refused COM Locked/FormulaHidden write is an unsupported
                // protection feature, NOT a reason to undo correct visual
                // formatting. It is only allowed when the original security
                // flag is demonstrably unchanged. Unexpected security changes
                // are still a fatal error and trigger the workbook rollback.
                if(wanted.Locked!=observed.Locked)
                {
                    if(priorFlags.Locked!=observed.Locked)
                        throw new InvalidDataException(
                            "Existing Locked protection changed unexpectedly at "+
                            Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                    unavailableProtectionV141++;
                }
                if(wanted.FormulaHidden!=observed.FormulaHidden)
                {
                    if(priorFlags.FormulaHidden!=observed.FormulaHidden)
                        throw new InvalidDataException(
                            "Existing FormulaHidden protection changed unexpectedly at "+
                            Text(live.Range.Cells[wanted.Row+1,wanted.Column+1].Address));
                    unavailableProtectionV141++;
                }
'@ 'strict appearance and baseline-safe protection verification'
Swap @'
                " real Excel tables and exact underline styles. "+
                (originallyEmpty?"Blank input cells stayed blank. ":
                    "Existing values/formulas preserved. ")+
                "Safety copy: "+backup;
'@ @'
                " real Excel tables and exact underline styles. "+
                (originallyEmpty?"Blank input cells stayed blank. ":
                    "Existing values/formulas preserved. ")+
                (unavailableProtectionV141>0
                    ?"IMPORTANT: "+unavailableProtectionV141+
                     " Locked/FormulaHidden settings could not be copied; "+
                     "the destination's original protection settings were "+
                     "left unchanged. All other supported formats verified. "
                    :"Cell-protection readback verified. ")+
                "Safety copy: "+backup;
'@ 'accurate success result for unavailable protection'
Write-Host 'R141: safe Locked/FormulaHidden on unprotected Excel ranges; skip unchanged properties, recover gracefully from Excel COM rejection, retain original protection and visibly report uncopyable security metadata. No source formulas/values copied. Strict table, visual readback, backup and rollback remain.'
