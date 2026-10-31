using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DeskSpace;

// R212: after a staged file is written but before atomic promotion to
// the shared Deck, re-read the staged file and verify all copied bytes.
// No source writes, user data deletion, diagnostics content, or new polling.
internal static class DeskSpaceCopyIntegrityV212
{
    internal static void VerifyWritten(string stagedPartial,long expectedLength,
        byte[] expectedSha256,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if(expectedLength<0 || expectedSha256==null || expectedSha256.Length!=32)
            throw new InvalidDataException("Invalid Deck copy verification manifest.");
        using var file=new FileStream(stagedPartial,FileMode.Open,
            FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer=new byte[128*1024];
        long scanned=0;
        int read;
        while((read=file.Read(buffer,0,buffer.Length))>0)
        {
            cancel.ThrowIfCancellationRequested();
            if(scanned>expectedLength-read)
                throw new InvalidDataException("Copied Deck file grew during verification.");
            scanned+=read;
            hash.AppendData(buffer,0,read);
        }
        cancel.ThrowIfCancellationRequested();
        if(scanned!=expectedLength ||
           !CryptographicOperations.FixedTimeEquals(
               hash.GetHashAndReset(),expectedSha256))
            throw new InvalidDataException(
                "Staged Deck copy does not match its source bytes.");
    }
}
