$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelV16.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$old,[string]$new)
{
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($old)){throw "R41 Excel UI preview batching missing anchor: $old"}
 $script:s=$script:s.Replace($old,$new)
}
# A 1,800-cell range can contain many per-cell editable controls. Creating
# them all synchronously on the UI dispatcher caused visible stalls even
# after moving the COM read off the UI thread.
$sourceCalls=([regex]::Matches($s,'PresentCandidate\(candidate\);')).Count
if($sourceCalls -ne 3){throw "Expected screenshot/live/link presentation at 3 sites, got $sourceCalls"}
Must 'PresentCandidate(candidate);' 'await PresentCandidateAsync(candidate);'
Must '    private void PresentCandidate(DeskSpaceExcelTemplateV16 candidate)' '    private async System.Threading.Tasks.Task PresentCandidateAsync(DeskSpaceExcelTemplateV16 candidate)'
Must @'
        pendingSelection=candidate;
        selectedCells.Items.Clear();
        foreach(var cell in candidate.Cells)
'@ @'
        pendingSelection=candidate;
        selectedCells.Items.Clear();
        int rendered=0;
        foreach(var cell in candidate.Cells)
'@
Must @'
            selectedCells.Items.Add(checkbox);
        }
        UpdateSelectionCount();
'@ @'
            selectedCells.Items.Add(checkbox);
            // Let wheel, click, typing and window painting run between
            // small batches, rather than freezing on a large Excel range.
            if(++rendered%32==0)
                await System.Windows.Threading.Dispatcher.Yield(
                    System.Windows.Threading.DispatcherPriority.Background);
        }
        UpdateSelectionCount();
'@
Must @'
    private void SaveCheckedCells()
    {
        if(pendingSelection==null)
'@ @'
    private void SaveCheckedCells()
    {
        if(pendingSelection!=null &&
           selectedCells.Items.Count<pendingSelection.Cells.Count)
            throw new InvalidOperationException(
                "Excel range preview is still rendering. Complete the review before saving.");
        if(pendingSelection==null)
'@
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R41: large captured worksheets render in nonblocking UI batches while preserving every reference/value/formula item.'
