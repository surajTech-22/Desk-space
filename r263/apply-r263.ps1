$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r263/DeskSpaceExcelSuggestionReviewV263.cs' -Destination "$root/DeskSpaceExcelSuggestionReviewV263.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R263 "+$why+" needs 1 anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                proposal,item,false);
'@ @'
            // The user approved a particular mapping and destination.
            // Excel or the saved mapping may change during the dialog.
            // A second, read-only COM preview MUST match every cell before
            // any write or backup workflow is entered.
            var currentMappingV263=DeskSpaceExcelStoreV16.Load()
                .FirstOrDefault(x=>x.Id==id);
            if(currentMappingV263==null)
                throw new InvalidOperationException(
                    "The mapping changed after review. Refresh suggestions.");
            var currentProposalV263=await DeskSpaceExcelWorkerV41.PreviewAsync(
                currentMappingV263,false,false);
            if(!DeskSpaceExcelSuggestionReviewV263.SameTarget(
                proposal,currentProposalV263))
                throw new InvalidOperationException(
                    "Excel or the destination changed during review. Preview again.");
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                currentProposalV263,currentMappingV263,false);
'@ 'recheck proposed workbook, sheet, all target cells and source values'
Swap @'
                "Excel suggestion blocked; nothing was auto-pasted: "+
                ex.Message,"DeskSpace Excel",
'@ @'
                "Excel suggestion blocked. Recheck the active workbook, "+
                "mapping and selected cells before trying again ("+
                ex.GetType().Name+"). Nothing was automatically pasted.",
                "DeskSpace Excel",
'@ 'redact workbook paths and COM exception text from blocked suggestions'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R263: Excel suggestions re-preview every target cell and saved mapping after confirmation; block changed workbook without writing.'
