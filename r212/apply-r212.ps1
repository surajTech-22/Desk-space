$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$p="$root/DeskSpaceFolderCopyV164.cs"
if(!(Test-Path -LiteralPath $p)){throw 'R212 requires the existing staging copy engine'}
Copy-Item -LiteralPath 'r212/DeskSpaceCopyIntegrityV212.cs' -Destination "$root/DeskSpaceCopyIntegrityV212.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R212 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap 'using System.IO;' @'
using System.IO;
using System.Security.Cryptography;
'@ 'crypto import'
Swap @'
        void TransferFile(string inputPath,string outputPath)
        {
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using(var input=new FileStream(inputPath,FileMode.Open,
'@ @'
        void TransferFile(string inputPath,string outputPath)
        {
            cancellation.ThrowIfCancellationRequested();
            var original=new FileInfo(inputPath);
            if((original.Attributes&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Copy source changed to a linked file.");
            long expectedLength=original.Length;
            DateTime expectedModified=original.LastWriteTimeUtc;
            long copiedFile=0;
            using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using(var input=new FileStream(inputPath,FileMode.Open,
'@ 'per-file expected metadata and digest'
Swap @'
                    cancellation.ThrowIfCancellationRequested();
                    output.Write(buffer,0,n);
                    checked{written+=n;}
'@ @'
                    cancellation.ThrowIfCancellationRequested();
                    if(copiedFile>expectedLength-n)
                        throw new IOException("Source file grew while staging.");
                    output.Write(buffer,0,n);
                    copiedFile+=n;
                    digest.AppendData(buffer,0,n);
                    checked{written+=n;}
'@ 'hash the actual source bytes as they are copied'
Swap @'
            cancellation.ThrowIfCancellationRequested();
            checked{completed++;}
'@ @'
            cancellation.ThrowIfCancellationRequested();
            original.Refresh();
            if(copiedFile!=expectedLength || original.Length!=expectedLength ||
                original.LastWriteTimeUtc!=expectedModified)
                throw new IOException("Source changed while staging a Deck file.");
            DeskSpaceCopyIntegrityV212.VerifyWritten(outputPath,copiedFile,
                digest.GetHashAndReset(),cancellation);
            cancellation.ThrowIfCancellationRequested();
            checked{completed++;}
'@ 'verify staged bytes before promotion'
Swap @'
            cancellation.ThrowIfCancellationRequested();
            onProgress?.Invoke(written,totalBytes,completed,totalFiles);
'@ @'
            cancellation.ThrowIfCancellationRequested();
            if(written!=totalBytes || completed!=totalFiles)
                throw new IOException("Folder contents changed while staging.");
            onProgress?.Invoke(written,totalBytes,completed,totalFiles);
'@ 'match initial folder estimate before atomic rename'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R212: Deck staged copies receive per-file SHA-256 readback and size/mtime/total checks before atomic promotion.'
