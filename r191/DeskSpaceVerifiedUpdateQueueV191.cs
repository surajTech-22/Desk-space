using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DeskSpace;

// A release is staged only AFTER the existing canonical GitHub checksum and
// release metadata have passed Verify. Never install unchecked cached files.
// This queue is deliberately process-local: no editable on-disk manifest can
// instruct a new process to execute an arbitrary installer.
internal static class DeskSpaceVerifiedUpdateQueueV191
{
    internal sealed record Candidate(string Installer,string Folder,string Tag,
        string Sha256,string Channel);
    private static readonly object Sync=new();
    private static Candidate? pending;

    internal static void Stage(string installer,string folder,string tag,
        string checksum,string channel)
    {
        if(!TagMatches(tag,channel) || !Regex.IsMatch(checksum,
            "^[0-9a-fA-F]{64}$") || !Directory.Exists(folder) ||
            !File.Exists(installer) ||
            !string.Equals(Path.GetFullPath(installer),
                Path.Combine(Path.GetFullPath(folder),"DeskSpace_Setup.exe"),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("R191 invalid verified update staging scope");
        lock(Sync)pending=new Candidate(installer,folder,tag,checksum,channel);
    }

    internal static Candidate? Peek(string channel)
    {
        lock(Sync)
        {
            if(pending!=null && pending.Channel!=channel)pending=null;
            return pending;
        }
    }

    internal static void Clear(Candidate candidate)
    {
        lock(Sync)
        {
            if(ReferenceEquals(pending,candidate))pending=null;
        }
    }

    internal static bool TagMatches(string tag,string channel)
    {
        return channel=="tester"?
            Regex.IsMatch(tag,@"^tester-v\d+\.\d+\.\d+$"):
            channel=="stable" && Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$");
    }

    internal static bool IsStillVerified(Candidate candidate)
    {
        try
        {
            if(!TagMatches(candidate.Tag,candidate.Channel) ||
               !Regex.IsMatch(candidate.Sha256,"^[0-9a-fA-F]{64}$") ||
               !string.Equals(Path.GetFullPath(candidate.Installer),
                   Path.Combine(Path.GetFullPath(candidate.Folder),
                       "DeskSpace_Setup.exe"),StringComparison.OrdinalIgnoreCase))
                return false;
            var file=new FileInfo(candidate.Installer);
            if(!file.Exists || file.Length<5_000_000 ||
               (file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            using var data=File.OpenRead(file.FullName);
            byte[] digest=SHA256.HashData(data);
            if(!CryptographicOperations.FixedTimeEquals(digest,
                Convert.FromHexString(candidate.Sha256)))return false;
            data.Position=0;
            return data.ReadByte()==0x4d && data.ReadByte()==0x5a;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
