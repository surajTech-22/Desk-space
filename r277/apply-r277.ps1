$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$before=[IO.File]::ReadAllText("$root/DeskSpaceBatchRenamePreviewV254.cs")
foreach($needed in @('Content="Apply approved names"','DeskSpaceBatchRenameTransactionV275.Apply(',
    'Content="Undo this batch"','DeskSpaceBatchRenameTransactionV275.Undo(')){
 if(!$before.Contains($needed)){throw ('R277 expected R275 UI marker missing: '+$needed)}
}
Copy-Item 'r277/DeskSpaceBatchRenameRecoveryV277.cs' "$root/DeskSpaceBatchRenameRecoveryV277.cs" -Force
Copy-Item 'r277/DeskSpaceBatchRenamePreviewV277.cs' "$root/DeskSpaceBatchRenamePreviewV254.cs" -Force
$crlf=[string][char]13+[char]10;$lf=[string][char]10
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ('R277 '+$label+': expected 1 anchor, got '+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),
    [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceFeatureHubV199.cs" @'
        Add("Batch rename preview",
            "Preview prefix/suffix filename changes and collision errors. Never renames files.",
            ()=>DeskSpaceBatchRenamePreviewV254.Show(this));
'@ @'
        Add("Batch rename and recovery",
            "Review and apply bounded filename changes, undo a batch, or safely recover after interruption.",
            ()=>DeskSpaceBatchRenamePreviewV254.Show(this));
'@ 'Feature launcher'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        new("previewfile","Preview file or folder",
            "Choose a local file or folder and inspect it without opening an external app","Files"),
'@ @'
        new("previewfile","Preview file or folder",
            "Choose a local file or folder and inspect it without opening an external app","Files"),
        new("batchrename","Batch rename and recovery",
            "Review, apply, undo or recover an interrupted batch rename","Files"),
'@ 'Feature launcher'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        switch(command.Id)
        {
            case "previewfile":
'@ @'
        switch(command.Id)
        {
            case "batchrename":
                DeskSpaceBatchRenamePreviewV254.Show(owner);
                break;
            case "previewfile":
'@ 'Feature launcher'
Write-Host 'R277: durable write-ahead journal, manual recovery UI, and action palette access built from canonical source.'