$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker="$r/DeskSpaceExcelFormatApplyV109.cs"
Copy-Item 'r136/DeskSpaceExcelBlankFormatPolicyV136.cs' "$r/DeskSpaceExcelBlankFormatPolicyV136.cs" -Force
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R136 $($label) expected one source anchor, got $n"}
 [IO.File]::WriteAllText($file,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap $worker @'
        bool sourceFormula=false;
'@ @'
        // Formula definitions remain saved in the FORMAT library.
        // A formatting-only transaction NEVER copies formulas to blank cells.
'@ 'no source-formula population rule'
Swap $worker @'
            if(source.FormulaR1C1.Length>0)sourceFormula=true;
'@ @'
            // Do not inspect source formula text for blank-destination values.
'@ 'remove source formula counter'
Swap $worker @'
        // A wholly empty destination may receive *recorded formulas* as part
        // of its reusable Excel structure. Existing populated data/formulas
        // are NEVER overwritten. This is declarative cell state, not macros.
        if(sourceFormula && decision.State==DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty &&
           order.Any(x=>x.StoredType=="Formula" && x.FormulaR1C1.Length==0))
            problems.Add("One or more source formulas lack their exact R1C1 definition.");
'@ @'
        // Formula expressions are kept in the captured template but not
        // copied into a blank destination: formatting-only must not show 0.
        // Existing populated destination values/formulas stay protected.
'@ 'blank destination no longer needs stored formulas to be executable'
Swap $worker @'
                      " Excel-generated neutral table column names. "+
                      "Saved formula definitions ("+order.Count(x=>
                          x.StoredType=="Formula")+
                      ") remain in the format library; NO formulas or "+
                      "transaction values are pasted into blank input cells."
'@ @'
                      " structural table headers. All "+order.Count(x=>
                          x.StoredType=="Formula")+
                      " saved formulas remain in the library, but will NOT "+
                      "be pasted to empty cells; all blank input cells stay blank."
'@ 'correct format paste status to describe actual operation'
Swap $worker @'
    // These formulas are relative R1C1 expressions saved per source cell.
    // They are inserted only into a wholly empty destination, not into
    // the user's existing inputs or formulas.
    private static void PopulateBlankFormulas(Checked live)
    {
        foreach(var source in live.Visual.Cells.Where(c=>
            c.StoredType=="Formula"))
        {
            if(string.IsNullOrWhiteSpace(source.FormulaR1C1))
                throw new InvalidDataException(
                    "A captured formula lacks an R1C1 expression.");
            live.Range.Cells[source.Row+1,source.Column+1].
                FormulaR1C1=source.FormulaR1C1;
        }
    }
'@ @'
    // Excel may automatically fill calculated ListObject columns while the
    // genuine Table is being created. On a verified ENTIRELY EMPTY target,
    // remove ONLY Excel-generated contents outside required table headers.
    // This intentionally does NOT run on partially/full-populated targets.
    private static void KeepBlankDestinationValuesV136(Checked live)
    {
        var tables=live.Visual.FeaturesV106!.Tables;
        var mergedChildren=new HashSet<(int,int)>();
        foreach(var anchor in live.Visual.Cells.Where(c=>
            c.MergeRows>1 || c.MergeColumns>1))
            for(int r=anchor.Row;r<anchor.Row+anchor.MergeRows;r++)
            for(int c=anchor.Column;c<anchor.Column+anchor.MergeColumns;c++)
                if(r!=anchor.Row || c!=anchor.Column)
                    mergedChildren.Add((r,c));
        for(int r=0;r<live.Visual.Rows;r++)
        for(int c=0;c<live.Visual.Columns;c++)
        {
            // Excel requires nonempty unique header identifiers for genuine
            // ListObjects. No source transaction/body values are permitted.
            string header=DeskSpaceExcelBlankFormatPolicyV136.
                AllowedStructuralHeader(r,c,tables);
            if(header.Length>0 || mergedChildren.Contains((r,c)))continue;
            dynamic cell=live.Range.Cells[r+1,c+1];
            if(IsFormula(cell) || (object?)cell.Value2!=null)
                cell.ClearContents();
        }
    }
'@ 'replace formula injection with removal of Excel-generated blank-body contents'
Swap $worker @'
            if(originallyEmpty)
                PopulateBlankFormulas(live);
'@ @'
            if(originallyEmpty)
                KeepBlankDestinationValuesV136(live);
'@ 'keep blank sheet inputs blank'
Swap $worker @'
                    string formula=s.StoredType=="Formula"?s.FormulaR1C1:"";
                    string header="";
                    foreach(var t in live.Visual.FeaturesV106!.Tables)
                        if(t.HasHeaders && s.Row==t.Row &&
                           s.Column>=t.Column &&
                           s.Column<t.Column+t.ColumnCount)
                            header=StructuralHeaderV123(s.Column-t.Column);
                    if(formula!=(IsFormula(cell)?Text(cell.FormulaR1C1):""))
                        throw new InvalidDataException(
                            "Recorded Excel formula could not be reconstructed.");
                    if(formula.Length==0 &&
                       header!=Text((object?)cell.Value2))
                        throw new InvalidDataException(
                            "Formatting changed an unexpected blank cell or table header.");
'@ @'
                    string header=DeskSpaceExcelBlankFormatPolicyV136.
                        AllowedStructuralHeader(s.Row,s.Column,
                            live.Visual.FeaturesV106!.Tables);
                    // No formula is allowed on an originally blank input cell:
                    // Excel calculated-column autofill can silently return 0.
                    if(IsFormula(cell) ||
                       header!=Text((object?)cell.Value2))
                        throw new InvalidDataException(
                            "A blank format-only destination now contains an "+
                            "unexpected value, formula or table header at "+
                            Text(cell.Address)+". No input values may be pasted.");
'@ 'verify blank destination truly blank instead of reconstructing formulas'
Swap $worker @'
            // Only a blank original may have our new table headers/formulas
            // erased during rollback; then restore all captured formats.
'@ @'
            // Only a blank original may have our new table headers or
            // Excel-generated contents erased during rollback.
'@ 'clarify rollback scope'
Swap $worker @'
                " real Excel tables and exact underline styles. Existing values preserved. "+
'@ @'
                " real Excel tables and exact underline styles. "+
                (originallyEmpty?"Blank input cells stayed blank. ":
                    "Existing values/formulas preserved. ")+
'@ 'report result content correctly'
Write-Host 'R136: real Excel tables and formats retained, captured formulas saved only as metadata; blank destination does not receive formulas/zeros/transaction values. Required structural table headers remain. Existing-data gate and rollback unchanged.'
