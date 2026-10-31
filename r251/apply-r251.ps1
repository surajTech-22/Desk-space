$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r251/DeskSpaceFormulaMetadataV251.cs' -Destination "$root/DeskSpaceFormulaMetadataV251.cs" -Force
$p="$root/DeskSpaceExcelFormatApplyV109.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$from,[string]$to,[string]$why)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($n -ne 1){throw ("R251 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($from,$to)
}
Swap @'
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
'@ @'
        var decision=DeskSpaceExcelFullRangeGateV107.Evaluate(from,to);
        // Reject a broken source template even when the target is blank.
        // Never rely on the old sourceFormula flag (all source formulas
        // could be absent or contradictory).
        if(order.Any(x=>!DeskSpaceFormulaMetadataV251.Valid(
            x.StoredType,x.FormulaR1C1)))
            problems.Add("Captured formula metadata is incomplete, invalid or "+
                "attached to a non-formula cell. Recapture source.");
'@ 'reject every missing/mislabeled R1C1 formula at preview stage'
Swap @'
    private static void PopulateBlankFormulas(Checked live)
    {
'@ @'
    private static void PopulateBlankFormulas(Checked live)
    {
        if(live.Visual.Cells.Any(x=>!DeskSpaceFormulaMetadataV251.Valid(
            x.StoredType,x.FormulaR1C1)))
            throw new InvalidDataException(
                "Captured formula metadata changed or was incomplete before write.");
'@ 'repeat exact formula check before writing blank cells'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R251: fail-closed source R1C1 formula metadata before Excel format+formula writes.'
