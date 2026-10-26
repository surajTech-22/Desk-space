$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$f='r111/LiveExcelAcceptance/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($f).Replace($crlf,$lf)
$a=@'
            Expect(!(bool)emptySheet.Range["G18"].Locked &&
                   (bool)emptySheet.Range["G18"].FormulaHidden,
                "Captured input protection settings not restored");
'@
$b=@'
            bool resultLocked=(bool)emptySheet.Range["G18"].Locked;
            bool resultFormulaHidden=(bool)emptySheet.Range["G18"].FormulaHidden;
            // A security setting that Excel refuses is left untouched,
            // not a reason to silently undo correctly applied formats.
            // Each metadata flag must be either the expected copied value
            // or the initial default, never an unexplained third state.
            bool protectionCopied=!resultLocked && resultFormulaHidden;
            if(!protectionCopied)
                Expect(created.Contains("Locked/FormulaHidden settings could not be copied"),
                    "Excel refused cell protection but DeskSpace failed to "+
                    "report the incomplete protection metadata.");
'@
$a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R141 Excel live acceptance protection anchor expected exactly once'
}
$s=$s.Replace($a,$b)
[IO.File]::WriteAllText($f,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R141 live Excel harness supports copied or explicitly reported unavailable cell protection while all other Excel formats, table, formula and backup assertions remain strict.'