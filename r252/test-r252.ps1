$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBackupReceiptV252.cs')
$engine=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelFormatApplyV109.cs')
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'SHA256.HashData(stream)',
 'FileShare.Read,64*1024',
 'File.Move(temp,receipt,false)',
 'if(!Verify(backup,receipt))',
 'DeskSpaceExcelBackupReceiptV252.Create(backup)',
 'DeskSpaceExcelBackupReceiptV252.Verify(backup,backupReceiptV252)',
 'TestExcelBackupReceiptV252();'
)){
 if(!$s.Contains($needle) -and !$engine.Contains($needle) -and !$test.Contains($needle))
 {throw ('R252 backup preflight/receipt guard missing '+$needle)}
}
if($engine.IndexOf('DeskSpaceExcelBackupReceiptV252.Create(backup)') -gt
   $engine.IndexOf('DeskSpaceExcelSheetVisualV101 before=')){
 throw 'R252 Excel backup integrity must be verified BEFORE capturing or changing destination'
}
Write-Host 'R252 STATIC PASS: backup byte checksum receipt verified before Excel format/formula mutation.'
