$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$zip=[IO.File]::ReadAllText("$root/DeskSpaceFolderZipV206.cs")
$helper=[IO.File]::ReadAllText("$root/DeskSpaceZipIntegrityV210.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @('digest.AppendData(buffer,0,n)',
 'digest.GetHashAndReset()', 'DeskSpaceZipIntegrityV210.Verify(partial,',
 'File.Move(partial,output,false)'))
{
 if(!$zip.Contains($needle)){throw ("R210 ZIP source missing "+$needle)}
}
if($zip.IndexOf('DeskSpaceZipIntegrityV210.Verify(partial,') -ge
   $zip.IndexOf('File.Move(partial,output,false)')){
 throw 'R210 must validate all bytes BEFORE atomic publication'
}
foreach($needle in @('CryptographicOperations.FixedTimeEquals',
 'entry.Open()', 'entry.Length!=expected.Length',
 'expectedDirectories.Count+expectedFiles.Count',
 'cancel.ThrowIfCancellationRequested()',
 'new byte[128*1024]'))
{
 if(!$helper.Contains($needle)){throw ("R210 payload verification missing "+$needle)}
}
if(!$smoke.Contains('TestZipIntegrityV210();')){
 throw 'R210 native checksum, corruption and cancellation tests not registered'
}
if($helper.Contains('File.Delete(') -or $helper.Contains('File.WriteAllText(') -or
   $helper.Contains('Process.Start(') -or $helper.Contains('Clipboard.')){
 throw 'R210 verification must be read-only'
}
Write-Host 'R210 STATIC PASS: bounded SHA-256 ZIP content readback and pre-Deck promotion.'
