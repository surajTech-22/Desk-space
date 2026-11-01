$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$copy=[IO.File]::ReadAllText("$root/DeskSpaceFolderCopyV164.cs")
$readback=[IO.File]::ReadAllText("$root/DeskSpaceCopyIntegrityV212.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @('using System.Security.Cryptography;',
 'digest.AppendData(buffer,0,n);',
 'DeskSpaceCopyIntegrityV212.VerifyWritten(outputPath,copiedFile,',
 'original.LastWriteTimeUtc!=expectedModified',
 'written!=totalBytes || completed!=totalFiles',
 'Directory.Move(partial,destination);',
 'else File.Move(partial,destination);'))
{
 if(!$copy.Contains($needle)){throw ('R212 staged copy missing '+$needle)}
}
if($copy.IndexOf('DeskSpaceCopyIntegrityV212.VerifyWritten(') -gt
   $copy.IndexOf('if(folder)Directory.Move(partial,destination);')){
 throw 'R212 content verification must precede any atomic promotion'
}
foreach($needle in @('CryptographicOperations.FixedTimeEquals',
 'hash.AppendData(buffer,0,read)',
 'cancel.ThrowIfCancellationRequested()',
 'FileShare.Read','read!=0'))
{
 if($needle -eq 'read!=0'){continue}
 if(!$readback.Contains($needle)){throw ('R212 staged readback missing '+$needle)}
}
if(!$smoke.Contains('TestCopyIntegrityV212();')){
 throw 'R212 native byte-integrity regression missing'
}
if($readback.Contains('File.Delete(') -or
   $readback.Contains('File.Move(') -or
   $readback.Contains('File.WriteAll')){
 throw 'R212 readback must not modify staged or original files'
}
Write-Host 'R212 STATIC PASS: full copy SHA-256 validation, source unchanged and atomic commit order.'
