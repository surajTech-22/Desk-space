$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$old,[string]$new,[string]$name)
{
    $text=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
    $a=$old.Replace($crlf,$lf);$b=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($text,[regex]::Escape($a))).Count
    if($count -ne 1){throw "R145 $name expected one anchor, got $count"}
    [IO.File]::WriteAllText($p,$text.Replace($a,$b),
        [Text.UTF8Encoding]::new($false))
}

# The format-only route creates genuine Excel tables, and a table may
# silently propagate calculated-column formulas, leaving displayed zeroes.
# Suppress new calculated-column autofill throughout the transaction.
Swap @'
        try
        {
            bool originallyEmpty=live.Review.Gate.State==
                DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
'@ @'
        bool guardedAutoFillV145=false,priorAutoFillV145=true;
        try
        {
            if(live.Visual.FeaturesV106!.Tables.Count>0)
            {
                // Fail BEFORE any sheet mutation if Excel cannot suspend
                // automatic formula fill. Restore original setting in finally.
                priorAutoFillV145=(bool)live.Excel.AutoCorrect.AutoFillFormulasInLists;
                live.Excel.AutoCorrect.AutoFillFormulasInLists=false;
                guardedAutoFillV145=true;
            }
            bool originallyEmpty=live.Review.Gate.State==
                DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty;
'@ 'disable generated table formulas before format-only apply'

Swap @'
                ". Do not save the affected workbook until reviewed.",failure);
        }
    }
}
'@ @'
                ". Do not save the affected workbook until reviewed.",failure);
        }
        finally
        {
            if(guardedAutoFillV145)
            {
                try{
                    live.Excel.AutoCorrect.AutoFillFormulasInLists=priorAutoFillV145;
                }
                catch(System.Runtime.InteropServices.COMException)
                {
                    // Never claim this setting was restored if Excel refuses.
                    // The safety backup and readback remain authoritative.
                }
            }
        }
    }
}
'@ 'restore Excel automatic fill setting after success and failure'

Write-Host 'R145: format-only Excel Table creation now suspends calculated-column auto-fill; the original Excel setting is restored on success/failure, existing blank-cell strict readback and rollback retained.'
