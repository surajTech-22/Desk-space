using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace DeskSpace;

// User-initiated read-only staged-file fingerprinting. Never hashes clipboard,
// source documents outside the private staging root, passwords, notes or Excel.
// Hashes are transient, stay in memory, and NEVER auto-delete or merge files.
internal static class DeskSpaceContentDuplicatesV186
{
    internal sealed record Group(string Kind,string[] Names);
    internal sealed record Result(int Checked,int Skipped,Group[] Groups);
    private const int MaxItems=25;
    private const int MaxEntriesPerFolder=4096;
    private const long MaxItemBytes=256L*1024*1024;
    private const long MaxScanBytes=1024L*1024*1024;
    private const int BufferSize=64*1024;
    private static string Shelf=>Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","TemporaryShelf")).TrimEnd(
            Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+
            Path.DirectorySeparatorChar;

    private static bool InShelf(string path)
    {
        try
        {
            string full=Path.GetFullPath(path);
            return full.StartsWith(Shelf,StringComparison.OrdinalIgnoreCase);
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException
            or PathTooLongException){return false;}
    }

    private static bool IsSafeEntry(string path)
    {
        if(!InShelf(path))return false;
        try
        {
            // Check ALL path components between staging root and candidate.
            // In particular never traverse a junction/Windows symlink.
            string root=Shelf.TrimEnd(Path.DirectorySeparatorChar);
            string full=Path.GetFullPath(path);
            string relative=Path.GetRelativePath(root,full);
            if(relative=="." || relative.StartsWith("..",StringComparison.Ordinal))
                return false;
            string parent=root;
            foreach(var segment in relative.Split(Path.DirectorySeparatorChar))
            {
                if(segment.Length==0 || segment=="." || segment=="..")
                    return false;
                parent=Path.Combine(parent,segment);
                var attrs=File.GetAttributes(parent);
                if((attrs&FileAttributes.ReparsePoint)!=0)return false;
            }
            return true;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException){return false;}
    }

    private static byte[] FingerprintFile(string file,ref long remaining,
        CancellationToken ct)
    {
        var info=new FileInfo(file);
        if(info.Length>MaxItemBytes || info.Length>remaining)
            throw new InvalidDataException("Staged file exceeds safe scan budget.");
        // Restrict the scan to completed, readable local files only.
        using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,
            FileShare.Read,BufferSize,FileOptions.SequentialScan);
        using var hasher=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer=new byte[BufferSize];
        int read;
        long consumed=0;
        while((read=stream.Read(buffer,0,buffer.Length))>0)
        {
            ct.ThrowIfCancellationRequested();
            consumed+=read;
            if(consumed>MaxItemBytes || consumed>remaining)
                throw new InvalidDataException("Staged item grew during scan.");
            hasher.AppendData(buffer,0,read);
        }
        // Refuse a file that was modified while it was being fingerprinted.
        info.Refresh();
        if(consumed!=info.Length)
            throw new IOException("Staged item changed during inspection.");
        remaining-=consumed;
        return hasher.GetHashAndReset();
    }

    private static byte[] FingerprintFolder(string folder,ref long remaining,
        CancellationToken ct)
    {
        var stack=new Stack<string>();
        var entries=new List<(string Path,string Relative,bool Folder)>();
        stack.Push(folder);
        while(stack.Count>0)
        {
            ct.ThrowIfCancellationRequested();
            var dir=stack.Pop();
            if(!IsSafeEntry(dir))
                throw new InvalidDataException("Folder contains an unsafe link.");
            foreach(var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                ct.ThrowIfCancellationRequested();
                if(!IsSafeEntry(entry))
                    throw new InvalidDataException("Folder contains an unsafe link.");
                var rel=Path.GetRelativePath(folder,entry);
                bool directory=Directory.Exists(entry);
                entries.Add((entry,rel,directory));
                if(entries.Count>MaxEntriesPerFolder)
                    throw new InvalidDataException("Too many folder entries.");
                if(directory)stack.Push(entry);
            }
        }
        entries.Sort((a,b)=>StringComparer.Ordinal.Compare(
            a.Relative.Replace('\\','/'),b.Relative.Replace('\\','/')));
        using var hasher=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // Prefix with a domain separator; files and folder trees can never
        // collide merely because their data bytes happen to be identical.
        hasher.AppendData(Encoding.UTF8.GetBytes("DeskSpace-Folder-V186\n"));
        long total=0;
        foreach(var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var rel=entry.Relative.Replace('\\','/');
            if(entry.Folder)
            {
                hasher.AppendData(Encoding.UTF8.GetBytes("D:"+rel+"\n"));
                continue;
            }
            var size=new FileInfo(entry.Path).Length;
            total+=size;
            if(total>MaxItemBytes || total>remaining)
                throw new InvalidDataException("Folder exceeds safe scan budget.");
            hasher.AppendData(Encoding.UTF8.GetBytes("F:"+rel+":"+size+"\n"));
            var fileRemaining=remaining;
            var contents=FingerprintFile(entry.Path,ref fileRemaining,ct);
            remaining=fileRemaining;
            hasher.AppendData(contents);
        }
        return hasher.GetHashAndReset();
    }

    internal static Result Check(IEnumerable<string> stagedPaths,
        CancellationToken cancellation)
    {
        if(stagedPaths==null)throw new ArgumentNullException(nameof(stagedPaths));
        var unique=stagedPaths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxItems+1).ToArray();
        int checkedCount=0,skipped=0;
        var found=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        long remaining=MaxScanBytes;
        for(int i=0;i<unique.Length;i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if(i>=MaxItems){skipped++;continue;}
            var path=unique[i];
            if(!IsSafeEntry(path)){skipped++;continue;}
            try
            {
                bool folder=Directory.Exists(path);
                if(!folder && !File.Exists(path)){skipped++;continue;}
                byte[] digest=folder
                    ?FingerprintFolder(path,ref remaining,cancellation)
                    :FingerprintFile(path,ref remaining,cancellation);
                string kind=folder?"Folder":"File";
                string key=kind+":"+Convert.ToHexString(digest);
                if(!found.TryGetValue(key,out var group))
                    found[key]=group=new List<string>();
                group.Add(Path.GetFileName(path));
                checkedCount++;
            }
            catch(OperationCanceledException){throw;}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException
                or InvalidDataException or ArgumentException or NotSupportedException)
            {
                skipped++; // One invalid/large item cannot stop other matches.
            }
        }
        var groups=found.Where(kv=>kv.Value.Count>1)
            .OrderBy(kv=>kv.Key,StringComparer.Ordinal)
            .Select(kv=>new Group(kv.Key.StartsWith("Folder:",StringComparison.Ordinal)
                ?"Folder":"File",kv.Value.ToArray())).ToArray();
        return new Result(checkedCount,skipped,groups);
    }
}
