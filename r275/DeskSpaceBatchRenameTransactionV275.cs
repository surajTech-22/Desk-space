using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace DeskSpace;

// Bounded, explicit rename transaction. No recursion, overwrite, delete,
// content read, network activity or silent background scanner. An in-session
// undo receipt is returned only for changes that have actually occurred.
internal static class DeskSpaceBatchRenameTransactionV275
{
    internal sealed record Move(string Before,string After,long Length,
        DateTime LastWriteUtc);
    internal sealed record Receipt(string Folder,IReadOnlyList<Move> Moves);
    internal sealed record Outcome(bool Success,string Message,int Changed,
        Receipt? Undo);

    private static string Canonical(string folder)=>
        Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

    private static Mutex Lock(string folder)
    {
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(folder.ToUpperInvariant()));
        return new Mutex(false,@"Local\DeskSpace.BatchRename."+
            Convert.ToHexString(bytes)[..24]);
    }

    private static bool ValidRoot(string path)=>
        Directory.Exists(path) &&
        (new DirectoryInfo(path).Attributes &
            (FileAttributes.ReparsePoint|FileAttributes.System))==0;

    private static bool SafeSource(FileInfo info)=>
        info.Exists &&
        (info.Attributes&(FileAttributes.ReparsePoint|
            FileAttributes.Hidden|FileAttributes.System|
            FileAttributes.ReadOnly))==0;

    private static bool Absent(string path)=>
        !File.Exists(path) && !Directory.Exists(path);

    private static bool Matches(string path,Move move)
    {
        var info=new FileInfo(path);
        return SafeSource(info) && info.Length==move.Length &&
            info.LastWriteTimeUtc==move.LastWriteUtc;
    }

    internal static Outcome Apply(string folder,
        DeskSpaceBatchRenamePreviewV254.Plan expected,
        string prefix,string suffix,CancellationToken cancel)
    {
        if(expected==null||!expected.Eligible ||
            expected.Files.Count==0 ||
            expected.Files.Count>DeskSpaceBatchRenamePreviewV254.MaxFiles)
            return new(false,"A valid preview is required before Apply.",0,null);
        string root;
        try{root=Canonical(folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException
            or PathTooLongException)
        {return new(false,"Folder is invalid.",0,null);}
        using var gate=Lock(root);
        bool owned=false;
        var renamed=new List<Move>();
        try
        {
            try{owned=gate.WaitOne(TimeSpan.FromSeconds(5));}
            catch(AbandonedMutexException){owned=true;}
            if(!owned)return new(false,"Another batch rename is active.",0,null);
            cancel.ThrowIfCancellationRequested();
            if(!ValidRoot(root))
                return new(false,"Folder is missing, linked, or protected.",0,null);
            var actual=DeskSpaceBatchRenamePreviewV254.InspectFolder(
                root,prefix,suffix);
            if(!actual.Eligible ||
               actual.Files.Count!=expected.Files.Count ||
               !actual.Files.Zip(expected.Files,
                   (a,b)=>a.Before==b.Before && a.After==b.After &&
                          !a.Conflict && !b.Conflict).All(match=>match))
                return new(false,"Folder or proposed names changed since preview. "+
                    "Preview again before applying.",0,null);
            var moves=new List<Move>();
            foreach(var row in actual.Files)
            {
                cancel.ThrowIfCancellationRequested();
                var oldPath=Path.Combine(root,row.Before);
                var newPath=Path.Combine(root,row.After);
                var file=new FileInfo(oldPath);
                if(!SafeSource(file) || !Absent(newPath))
                    return new(false,"One of the files is protected, changed, "+
                        "or a destination name is already occupied.",0,null);
                moves.Add(new(row.Before,row.After,file.Length,
                    file.LastWriteTimeUtc));
            }
            foreach(var item in moves)
            {
                cancel.ThrowIfCancellationRequested();
                var oldPath=Path.Combine(root,item.Before);
                var newPath=Path.Combine(root,item.After);
                if(!Matches(oldPath,item)||!Absent(newPath))
                    throw new IOException("A source or destination changed during Apply.");
                File.Move(oldPath,newPath,false); // NEVER overwrite existing files
                renamed.Add(item);
            }
            return new(true,"Renamed "+renamed.Count+
                " files. Undo is available until this window is closed.",
                renamed.Count,new(root,renamed.ToArray()));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            OperationCanceledException or ArgumentException or
            System.Security.SecurityException)
        {
            // Partial failure: revert moves in reverse order without overwrite.
            // A file edited mid-operation is never overwritten by rollback.
            var remaining=new List<Move>(renamed);
            for(int i=renamed.Count-1;i>=0;i--)
            {
                var item=renamed[i];
                var oldPath=Path.Combine(root,item.Before);
                var newPath=Path.Combine(root,item.After);
                try
                {
                    if(!Absent(oldPath)||!Matches(newPath,item))continue;
                    File.Move(newPath,oldPath,false);
                    remaining.Remove(item);
                }
                catch(Exception rollback) when(rollback is IOException or
                    UnauthorizedAccessException or
                    System.Security.SecurityException){}
            }
            return new(false,remaining.Count==0?
                "Apply stopped ("+ex.GetType().Name+
                    "). All completed renames were rolled back.":
                "Apply stopped ("+ex.GetType().Name+"). "+
                    remaining.Count+" names could not be rolled back. "+
                    "Use Undo remaining; do not close this window.",
                remaining.Count,remaining.Count==0?null:
                    new(root,remaining.ToArray()));
        }
        finally{if(owned)gate.ReleaseMutex();}
    }

    internal static Outcome Undo(Receipt? receipt,CancellationToken cancel)
    {
        if(receipt==null || receipt.Moves.Count==0 ||
            receipt.Moves.Count>DeskSpaceBatchRenamePreviewV254.MaxFiles)
            return new(false,"No undoable batch is available.",0,null);
        string root;
        try{root=Canonical(receipt.Folder);}
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException)
        {return new(false,"Saved folder is invalid.",0,receipt);}
        if(!string.Equals(root,receipt.Folder,StringComparison.OrdinalIgnoreCase))
            return new(false,"Saved folder identity changed.",0,receipt);
        using var gate=Lock(root);
        bool owned=false;
        var restored=new List<Move>();
        try
        {
            try{owned=gate.WaitOne(TimeSpan.FromSeconds(5));}
            catch(AbandonedMutexException){owned=true;}
            if(!owned)return new(false,"Another batch rename is active.",0,receipt);
            cancel.ThrowIfCancellationRequested();
            if(!ValidRoot(root))
                return new(false,"Undo folder is no longer safe.",0,receipt);
            foreach(var item in receipt.Moves)
            {
                cancel.ThrowIfCancellationRequested();
                var current=Path.Combine(root,item.After);
                var original=Path.Combine(root,item.Before);
                if(!Matches(current,item)||!Absent(original))
                    return new(false,"Undo blocked: a renamed file was edited, "+
                        "moved, or an original name is now occupied. "+
                        "No files were changed.",0,receipt);
            }
            foreach(var item in receipt.Moves.Reverse())
            {
                cancel.ThrowIfCancellationRequested();
                var current=Path.Combine(root,item.After);
                var original=Path.Combine(root,item.Before);
                if(!Matches(current,item)||!Absent(original))
                    throw new IOException("A file changed during Undo.");
                File.Move(current,original,false);
                restored.Add(item);
            }
            return new(true,"Restored "+restored.Count+
                " original names. File contents were never changed.",
                restored.Count,null);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            OperationCanceledException or System.Security.SecurityException)
        {
            // A partially failed undo must not silently lose its receipt.
            // Roll forward every completed inverse rename if still safe.
            foreach(var item in restored)
            {
                var original=Path.Combine(root,item.Before);
                var current=Path.Combine(root,item.After);
                try
                {
                    if(Matches(original,item)&&Absent(current))
                        File.Move(original,current,false);
                }
                catch(Exception rollback) when(rollback is IOException or
                    UnauthorizedAccessException or
                    System.Security.SecurityException){}
            }
            // Report the true remaining reversible moves, including ones
            // already restored when roll-forward could not be completed.
            var pending=receipt.Moves.Where(item=>
                File.Exists(Path.Combine(root,item.After))).ToArray();
            return new(false,"Undo interrupted ("+ex.GetType().Name+
                "). Verify filenames; only still-renamed files are eligible "+
                "for a later Undo. No files were overwritten.",
                pending.Length,pending.Length==0?null:
                    new(root,pending));
        }
        finally{if(owned)gate.ReleaseMutex();}
    }
}
