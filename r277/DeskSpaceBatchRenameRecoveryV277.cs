using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DeskSpace;

// Durable WRITE-AHEAD recovery for user-approved R275 batch rename.
// Never automatically renames or undoes on startup. Journal contains only
// local file names, metadata and the selected folder; no file contents.
internal static class DeskSpaceBatchRenameRecoveryV277
{
    internal sealed record Journal(int Version,string Folder,DateTime CreatedUtc,
        IReadOnlyList<DeskSpaceBatchRenameTransactionV275.Move> Moves);
    internal sealed record Review(bool Found,bool CanRecover,int Renamed,
        int Untouched,string Message,
        DeskSpaceBatchRenameTransactionV275.Receipt? Undo);

    private const int MaxJournalBytes=131072;
    private static readonly string DirectoryPath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","BatchRename","Recovery");

    private static string Canonical(string folder)=>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private static string Key(string canonical)=>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            canonical.ToUpperInvariant())));

    private static string JournalPath(string root)=>
        Path.Combine(DirectoryPath,Key(root)+".json");

    private static Mutex Lock(string root)=>
        new(false,@"Local\DeskSpace.BatchRename."+Key(root)[..24]);

    private static bool SafeName(string? name)=>name is {Length:>0 and <=255} &&
        name!="." && name!=".." && Path.GetFileName(name)==name &&
        !name.EndsWith(' ') && !name.EndsWith('.') &&
        !name.Any(ch=>char.IsControl(ch)||"<>:\"/\\|?*".Contains(ch));

    private static bool SafeFile(string path,
        DeskSpaceBatchRenameTransactionV275.Move item)
    {
        var file=new FileInfo(path);
        return file.Exists &&
            (file.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|
              FileAttributes.System|FileAttributes.ReadOnly))==0 &&
            file.Length==item.Length &&
            file.LastWriteTimeUtc==item.LastWriteUtc;
    }

    private static bool Missing(string path)=>
        !File.Exists(path) && !System.IO.Directory.Exists(path);

    private static Journal? Read(string root,out bool corrupt)
    {
        var path=JournalPath(root);
        corrupt=false;
        if(!File.Exists(path))return null;
        try
        {
            var file=new FileInfo(path);
            if(file.Length is <=0 or >MaxJournalBytes)
                throw new InvalidDataException("Unexpected recovery record size.");
            var record=JsonSerializer.Deserialize<Journal>(
                File.ReadAllText(path,Encoding.UTF8));
            if(record==null || record.Version!=1 ||
               !string.Equals(record.Folder,root,StringComparison.OrdinalIgnoreCase) ||
               record.Moves==null || record.Moves.Count is <1 or >128 ||
               record.CreatedUtc.Kind!=DateTimeKind.Utc)
                throw new InvalidDataException("Unexpected recovery record structure.");
            var before=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var after=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in record.Moves)
            {
                if(item==null || !SafeName(item.Before)||!SafeName(item.After) ||
                   item.Length<0 || item.LastWriteUtc.Kind!=DateTimeKind.Utc ||
                   string.Equals(item.Before,item.After,StringComparison.OrdinalIgnoreCase) ||
                   !before.Add(item.Before)||!after.Add(item.After))
                    throw new InvalidDataException("Unexpected recovery file names.");
            }
            if(after.Overlaps(before))
                throw new InvalidDataException("Recovery destinations overlap sources.");
            return record;
        }
        catch(Exception ex) when(ex is JsonException or IOException or
            UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            corrupt=true;
            return null;
        }
    }

    private static void Write(Journal journal)
    {
        System.IO.Directory.CreateDirectory(DirectoryPath);
        var destination=JournalPath(journal.Folder);
        // Flush the write-ahead record before the first filesystem move.
        // A unique adjacent temp file avoids partially written JSON.
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(journal));
            if(bytes.Length>MaxJournalBytes)
                throw new InvalidDataException("Recovery record exceeds limit.");
            using(var output=new FileStream(temporary,FileMode.CreateNew,
                FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            {
                output.Write(bytes);
                output.Flush(flushToDisk:true);
            }
            File.Move(temporary,destination,true);
        }
        finally
        {
            try{if(File.Exists(temporary))File.Delete(temporary);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

    private static void Clear(string root)
    {
        var path=JournalPath(root);
        if(File.Exists(path))File.Delete(path);
    }

    // Read-only inspection of a specifically chosen folder. Do not mutate
    // on launch, in the tray, or while showing this review.
    internal static Review Inspect(string folder)
    {
        string root;
        try{root=Canonical(folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException or
            PathTooLongException)
        {return new(false,false,0,0,"Choose a valid folder.",null);}
        if(!System.IO.Directory.Exists(root))
            return new(false,false,0,0,"Selected folder is missing.",null);
        bool corrupt;
        Journal? record;
        try{record=Read(root,out corrupt);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException)
        {return new(true,false,0,0,"Recovery record cannot be accessed.",null);}
        if(corrupt)
            return new(true,false,0,0,
                "Recovery record is damaged. No changes will be made.",null);
        if(record==null)
            return new(false,false,0,0,
                "No previous rename recovery record for this folder.",null);
        var rootInfo=new DirectoryInfo(root);
        if((rootInfo.Attributes&(FileAttributes.ReparsePoint|
             FileAttributes.System))!=0)
            return new(true,false,0,0,
                "Linked or protected folder: recovery blocked.",null);
        int renamed=0,untouched=0;
        var undone=new List<DeskSpaceBatchRenameTransactionV275.Move>();
        foreach(var move in record.Moves)
        {
            var before=Path.Combine(root,move.Before);
            var after=Path.Combine(root,move.After);
            bool atBefore=SafeFile(before,move);
            bool atAfter=SafeFile(after,move);
            if(atBefore && Missing(after))untouched++;
            else if(atAfter && Missing(before))
            {
                renamed++;
                undone.Add(move);
            }
            else
                return new(true,false,renamed,untouched,
                    "Recovery blocked: file changed, disappeared, or both names "+
                    "exist. Existing files will not be overwritten.",null);
        }
        return new(true,true,renamed,untouched,
            renamed==0?
                "No renamed files remain; this record can be cleared by "+
                "an explicit recovery action.":
                renamed+" renamed files can be safely restored and "+
                untouched+" files are already at their original names.",
            new(root,undone.ToArray()));
    }

    // This is the ONLY entry point the UI uses for applying a batch. It
    // writes the durable intent before calling the previous verified engine.
    internal static DeskSpaceBatchRenameTransactionV275.Outcome Apply(
        string folder,DeskSpaceBatchRenamePreviewV254.Plan expected,
        string prefix,string suffix,CancellationToken cancel)
    {
        var failure=new DeskSpaceBatchRenameTransactionV275.Outcome(
            false,"Could not safely start batch rename.",0,null);
        string root;
        try{root=Canonical(folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException
            or PathTooLongException){return failure;}
        using var gate=Lock(root);
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(5));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)return failure with{Message="Another rename is in progress."};
            cancel.ThrowIfCancellationRequested();
            if(Inspect(root).Found)
                return failure with{Message="A prior rename record exists. "+
                    "Review its recovery before applying a new batch."};
            if(expected==null||!expected.Eligible||
               expected.Files.Count is <1 or >128 ||
               !System.IO.Directory.Exists(root))
                return failure with{Message="Preview again before applying."};
            var folderInfo=new DirectoryInfo(root);
            if((folderInfo.Attributes&(FileAttributes.ReparsePoint|
                FileAttributes.System))!=0)
                return failure with{Message="Linked/system folder is blocked."};
            var moves=new List<DeskSpaceBatchRenameTransactionV275.Move>();
            foreach(var item in expected.Files)
            {
                cancel.ThrowIfCancellationRequested();
                if(item.Conflict||!SafeName(item.Before)||!SafeName(item.After))
                    return failure with{Message="Unsafe proposed filename."};
                var src=Path.Combine(root,item.Before);
                var dst=Path.Combine(root,item.After);
                var info=new FileInfo(src);
                if(!info.Exists||
                   (info.Attributes&(FileAttributes.ReparsePoint|
                       FileAttributes.Hidden|FileAttributes.System|
                       FileAttributes.ReadOnly))!=0 || !Missing(dst))
                    return failure with{Message="File changed or target occupied."};
                moves.Add(new(item.Before,item.After,info.Length,
                    info.LastWriteTimeUtc));
            }
            // No file gets moved unless the complete record is durable.
            Write(new(1,root,DateTime.UtcNow,moves));
            var result=DeskSpaceBatchRenameTransactionV275.Apply(
                root,expected,prefix,suffix,cancel);
            if(!result.Success && result.Undo==null)
            {
                // The previous engine reports all changes rolled back.
                // Keep the record if a post-failure inspection disagrees.
                var state=Inspect(root);
                if(state.CanRecover && state.Renamed==0)Clear(root);
            }
            return result with{Message=result.Success?
                result.Message+" Crash recovery is retained locally.":
                result.Message};
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or
            OperationCanceledException or System.Security.SecurityException)
        {
            return failure with{Message="Rename did not complete ("+
                ex.GetType().Name+"). Review recovery before retrying."};
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }

    // Explicit undo after a successful Apply; never discard recovery on a
    // failure or if any user file may still be renamed.
    internal static DeskSpaceBatchRenameTransactionV275.Outcome Undo(
        DeskSpaceBatchRenameTransactionV275.Receipt receipt,
        CancellationToken cancel)
    {
        var result=DeskSpaceBatchRenameTransactionV275.Undo(receipt,cancel);
        if(result.Success)
        {
            try
            {
                var root=Canonical(receipt.Folder);
                var state=Inspect(root);
                if(state.CanRecover && state.Renamed==0)Clear(root);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or System.Security.SecurityException){}
        }
        return result;
    }

    // Explicitly accept the fully completed rename and remove its journal.
    // This does NOT touch the renamed files; partial/interrupted batches
    // must be recovered first, never silently accepted.
    internal static DeskSpaceBatchRenameTransactionV275.Outcome Keep(
        string folder,CancellationToken cancel)
    {
        var failure=new DeskSpaceBatchRenameTransactionV275.Outcome(
            false,"Only a fully completed reviewed batch can be kept.",0,null);
        string root;
        try{root=Canonical(folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException
            or PathTooLongException){return failure;}
        using var gate=Lock(root);
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(5));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)return failure with{Message="Another batch is in progress."};
            cancel.ThrowIfCancellationRequested();
            var state=Inspect(root);
            if(!state.Found||!state.CanRecover||state.Renamed==0||
               state.Untouched!=0)
                return failure with{Message="Recovery is not fully complete "+
                    "or files have changed. Review the batch again."};
            Clear(root);
            return new(true,"Kept "+state.Renamed+
                " renamed files; local undo record cleared.",0,null);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or
            OperationCanceledException or System.Security.SecurityException)
        {
            return failure with{Message="Could not clear the recovery record ("+
                ex.GetType().Name+"). Filenames were left unchanged."};
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }

    // Explicit user-approved recovery after selecting a folder. Can undo
    // a batch interrupted midway through APPLY or UNDO, or clear a durable
    // record if every file already has its original name.
    internal static DeskSpaceBatchRenameTransactionV275.Outcome Recover(
        string folder,CancellationToken cancel)
    {
        var failure=new DeskSpaceBatchRenameTransactionV275.Outcome(
            false,"Recovery unavailable. No files changed.",0,null);
        string root;
        try{root=Canonical(folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException
            or PathTooLongException){return failure;}
        using var gate=Lock(root);
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(5));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)return failure with{Message="Another rename is running."};
            cancel.ThrowIfCancellationRequested();
            var state=Inspect(root);
            if(!state.Found || !state.CanRecover)
                return failure with{Message=state.Message};
            if(state.Renamed==0)
            {
                Clear(root);
                return new(true,"Original names are intact; recovery record cleared.",
                    0,null);
            }
            var result=DeskSpaceBatchRenameTransactionV275.Undo(
                state.Undo,cancel);
            if(result.Success)
            {
                // The same per-folder OS mutex protects the whole recovery
                // while the old engine verifies and restores every name.
                var after=Inspect(root);
                if(after.CanRecover && after.Renamed==0)
                {
                    Clear(root);
                    return result with{Message="Restored "+state.Renamed+
                        " filenames; recovery journal cleared."};
                }
                return failure with{Message="Some names could not be verified. "+
                    "Recovery record retained; inspect before retrying."};
            }
            return result with{Message=result.Message+
                " Recovery record retained for another review."};
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidDataException or
            OperationCanceledException or System.Security.SecurityException)
        {return failure with{Message="Recovery stopped ("+
            ex.GetType().Name+"). Record retained; no overwrites."};}
        finally{if(acquired)gate.ReleaseMutex();}
    }
}
