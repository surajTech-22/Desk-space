$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$p="$root/DeskSpaceFolderZipV206.cs"
if(!(Test-Path -LiteralPath $p)){throw 'R210 must follow R206 ZIP integration'}
Copy-Item -LiteralPath 'r210/DeskSpaceZipIntegrityV210.cs' -Destination "$root/DeskSpaceZipIntegrityV210.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$reason)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R210 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
using System.IO.Compression;
'@ @'
using System.IO.Compression;
using System.Security.Cryptography;
'@ 'crypto namespace'
Swap @'
        long copied=0;
        int finished=0;
'@ @'
        long copied=0;
        int finished=0;
        var expected=new List<DeskSpaceZipIntegrityV210.Expected>();
'@ 'manifest of copied payloads'
Swap @'
                    using var destinationStream=entry.Open();
'@ @'
                    using var destinationStream=entry.Open();
                    using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
'@ 'streaming digest lifecycle'
Swap @'
                        destinationStream.Write(buffer,0,n);
'@ @'
                        destinationStream.Write(buffer,0,n);
                        digest.AppendData(buffer,0,n);
'@ 'streaming digest write'
Swap @'
                    if(currentBytes!=file.Size)
                        throw new IOException("Source length changed during ZIP.");
                    finished++;
'@ @'
                    if(currentBytes!=file.Size)
                        throw new IOException("Source length changed during ZIP.");
                    expected.Add(new DeskSpaceZipIntegrityV210.Expected(
                        file.Relative,file.Size,digest.GetHashAndReset()));
                    finished++;
'@ 'capture final payload SHA256'
Swap @'
            cancel.ThrowIfCancellationRequested();
            File.Move(partial,output,false);
'@ @'
            // Verify actual decompressed payloads before atomic promotion.
            DeskSpaceZipIntegrityV210.Verify(partial,
                directories.ConvertAll(d=>d.Relative),expected,cancel);
            cancel.ThrowIfCancellationRequested();
            File.Move(partial,output,false);
'@ 'validate ZIP before acceptance'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R210: ZIP SHA-256 payload readback before atomic Deck promotion.'
