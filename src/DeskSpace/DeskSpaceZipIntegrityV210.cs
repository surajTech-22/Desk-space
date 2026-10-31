using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

namespace DeskSpace;

// R210: verify each decompressed ZIP payload against bytes read while creating
// the archive. This is not deduplication; it prevents a corrupt ZIP from being
// advertised as successfully staged in the shared Deck.
internal static class DeskSpaceZipIntegrityV210
{
    internal readonly record struct Expected(string Name,long Length,byte[] Sha256);

    internal static bool ContentMatches(Stream decoded,long expectedLength,
        byte[] expectedHash,CancellationToken cancel)
    {
        if(expectedLength<0 || expectedHash==null ||
            expectedHash.Length!=32)return false;
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer=new byte[128*1024];
        long total=0;
        while(true)
        {
            cancel.ThrowIfCancellationRequested();
            int n=decoded.Read(buffer,0,buffer.Length);
            if(n==0)break;
            if(total>expectedLength-n)return false;
            total+=n;
            digest.AppendData(buffer,0,n);
        }
        return total==expectedLength &&
            CryptographicOperations.FixedTimeEquals(
                digest.GetHashAndReset(),expectedHash);
    }

    internal static void Verify(string archivePath,
        IReadOnlyList<string> expectedDirectories,
        IReadOnlyList<Expected> expectedFiles,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        using var zip=ZipFile.OpenRead(archivePath);
        if(zip.Entries.Count!=expectedDirectories.Count+expectedFiles.Count)
            throw new InvalidDataException("Archive entry count changed during verification.");
        for(int i=0;i<expectedDirectories.Count;i++)
        {
            cancel.ThrowIfCancellationRequested();
            var entry=zip.Entries[i];
            if(!entry.FullName.Equals(expectedDirectories[i],StringComparison.Ordinal) ||
                !entry.FullName.EndsWith("/",StringComparison.Ordinal) ||
                entry.Length!=0)
                throw new InvalidDataException("ZIP directory layout validation failed.");
        }
        for(int i=0;i<expectedFiles.Count;i++)
        {
            cancel.ThrowIfCancellationRequested();
            var expected=expectedFiles[i];
            var entry=zip.Entries[expectedDirectories.Count+i];
            if(!entry.FullName.Equals(expected.Name,StringComparison.Ordinal) ||
               entry.Length!=expected.Length)
                throw new InvalidDataException("ZIP entry name or size changed.");
            using var stream=entry.Open();
            if(!ContentMatches(stream,expected.Length,expected.Sha256,cancel))
                throw new InvalidDataException("ZIP file integrity verification failed.");
        }
    }
}
