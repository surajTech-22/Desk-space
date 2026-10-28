$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
foreach($required in @(
 'R173 stage: StageFilesAsync entry',
 'R173 stage: before initial RefreshViews',
 'R173 stage: after initial RefreshViews',
 'R173 stage: worker before marker lease',
 'R173 stage: worker lease acquired',
 'R173 stage: worker Copy complete',
 'R173 COPY FAILED: ',
 'lastCopyError=ex.GetType().Name;',
 'lastCopyError=null;',
 'Original preserved.',
 'if(held.Count==0 && !busy && lastCopyError==null)'
)){
 if(!$s.Contains($required)){throw "R173 integration missing: $required"}
}
if($s.Contains('MessageBox.Show("Could not hold the file: "+ex.Message,')){
 throw 'R173 blocking copy error dialog still present'
}
Write-Host 'R173 PASS: no modal Copy errors; shared error presentation and staged-file checkpoints present.'
