using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DeskSpace;

// R211: read-only content deduplication for manually staged files.
// Never hashes an external recent-file suggestion without a user drop,
// never persists hashes or source data, and never deletes any file.
internal static class DeskSpaceContentDuplicateV211
{
    internal const long MaxFingerprintBytes=128L*1024*1024;
    private const int BufferSize=128*1024;

    private static byte[]? Fingerprint(string path,long expectedSize,
        DateTime expectedWrite,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        try
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                return null;
            using var stream=new FileStream(path,FileMode.Open,
                FileAccess.Read,FileShare.Read,BufferSize,FileOptions.SequentialScan);
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer=new byte[BufferSize];
            long scanned=0;
            int n;
            while((n=stream.Read(buffer,0,buffer.Length))>0)
            {
                cancel.ThrowIfCancellationRequested();
                if(scanned>expectedSize-n)return null;
                scanned+=n;
                hash.AppendData(buffer,0,n);
            }
            cancel.ThrowIfCancellationRequested();
            var after=new FileInfo(path);
            if(scanned!=expectedSize || after.Length!=expectedSize ||
                after.LastWriteTimeUtc!=expectedWrite)return null;
            return hash.GetHashAndReset();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            // A concurrently saved or locked file may still be staged using
            // the existing copy rules; do not treat it as a verified match.
            return null;
        }
    }

    internal static string? FindMatching(string source,
        IReadOnlyList<string> stagedFiles,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if(stagedFiles==null || stagedFiles.Count==0)return null;
        try
        {
            source=Path.GetFullPath(source);
            if(!File.Exists(source))return null;
            var original=new FileInfo(source);
            if(original.Length>MaxFingerprintBytes ||
                (original.Attributes&FileAttributes.ReparsePoint)!=0)return null;
            var size=original.Length;
            // Check size before hashing; candidates are a snapshot of the
            // completed shared Deck, never a *.partial copy.
            var matches=new List<(string Path,DateTime Modified)>();
            foreach(var candidate in stagedFiles)
            {
                cancel.ThrowIfCancellationRequested();
                if(string.IsNullOrWhiteSpace(candidate))continue;
                string canonical=Path.GetFullPath(candidate);
                if(canonical.Equals(source,StringComparison.OrdinalIgnoreCase) ||
                   !File.Exists(canonical) ||
                   canonical.EndsWith(".partial",StringComparison.OrdinalIgnoreCase))
                    continue;
                var info=new FileInfo(canonical);
                if(info.Length==size &&
                   (info.Attributes&FileAttributes.ReparsePoint)==0)
                    matches.Add((canonical,info.LastWriteTimeUtc));
            }
            if(matches.Count==0)return null;
            var reference=Fingerprint(source,size,original.LastWriteTimeUtc,cancel);
            if(reference==null)return null;
            foreach(var candidate in matches)
            {
                cancel.ThrowIfCancellationRequested();
                var digest=Fingerprint(candidate.Path,size,candidate.Modified,cancel);
                if(digest!=null &&
                    CryptographicOperations.FixedTimeEquals(reference,digest))
                    return candidate.Path;
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            // Unknown filesystems and missing files must not prevent normal
            // staged-file copying or delete the original.
        }
        return null;
    }
}
