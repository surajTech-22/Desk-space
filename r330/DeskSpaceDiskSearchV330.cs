using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace DeskSpace;

// R330: The full Windows filename catalog must NEVER remain in managed RAM.
// SQLite uses on-disk FTS5 trigram search; each scan writes <=384 paths per
// transaction. This index is a rebuildable local cache, not user documents.
internal static class DeskSpaceDiskSearchV330
{
    internal readonly record struct Item(string Name,string Path,bool Folder,string Volume);
    private readonly record struct Pending(string Path,bool Exists);
    internal const int BatchSize=384;
    internal const int MaximumPending=4096;
    internal const int MaximumReturned=640;
    private static readonly object SetupGate=new();
    private static readonly ConcurrentQueue<Pending> PendingChanges=new();
    private static readonly ConcurrentDictionary<string,byte> DirtyVolumes=
        new(StringComparer.OrdinalIgnoreCase);
    private static Timer? updateTimer;
    private static Action<string>? rescan;
    private static int pendingCount,flushing,initialized;
    private static long epochSequence=DateTime.UtcNow.Ticks;

    private static string DatabaseFile
    {
        get
        {
            var baseFolder=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace","Search");
            Directory.CreateDirectory(baseFolder);
            return Path.Combine(baseFolder,"filenames-v330.sqlite");
        }
    }

    private static SqliteConnection Connect()
    {
        Initialize();
        var builder=new SqliteConnectionStringBuilder{
            DataSource=DatabaseFile,Mode=SqliteOpenMode.ReadWriteCreate,
            Cache=SqliteCacheMode.Private,Pooling=false
        };
        var connection=new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragmas=connection.CreateCommand();
        // A busy writer must not hold live search for 10 seconds per volume.
        pragmas.CommandText="PRAGMA busy_timeout=250; PRAGMA cache_size=-2048; PRAGMA temp_store=FILE;";
        pragmas.ExecuteNonQuery();
        return connection;
    }
    private static void Initialize()
    {
        if(Volatile.Read(ref initialized)!=0)return;
        lock(SetupGate)
        {
            if(initialized!=0)return;
            string file=DatabaseFile;
            using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{
                DataSource=file,Mode=SqliteOpenMode.ReadWriteCreate,
                Pooling=false
            }.ToString());
            connection.Open();
            using var command=connection.CreateCommand();
            command.CommandText=@"PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA cache_size=-2048;
                CREATE TABLE IF NOT EXISTS entries(
                  id INTEGER PRIMARY KEY,
                  path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                  name TEXT NOT NULL,
                  folder INTEGER NOT NULL,
                  volume TEXT NOT NULL,
                  scan_epoch INTEGER NOT NULL DEFAULT 0);
                CREATE INDEX IF NOT EXISTS idx_entries_volume ON entries(volume);
                CREATE VIRTUAL TABLE IF NOT EXISTS names_fts USING fts5(
                  name,path,content='entries',content_rowid='id',tokenize='trigram');
                CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN
                  INSERT INTO names_fts(rowid,name,path) VALUES(new.id,new.name,new.path);
                END;
                CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN
                  INSERT INTO names_fts(names_fts,rowid,name,path)
                    VALUES('delete',old.id,old.name,old.path);
                END;
                CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries BEGIN
                  INSERT INTO names_fts(names_fts,rowid,name,path)
                    VALUES('delete',old.id,old.name,old.path);
                  INSERT INTO names_fts(rowid,name,path) VALUES(new.id,new.name,new.path);
                END;";
            command.ExecuteNonQuery();
            Volatile.Write(ref initialized,1);
        }
    }

    internal static void EnableUpdates(Action<string> scanVolume)
    {
        lock(SetupGate)
        {
            rescan=scanVolume;
            updateTimer ??=new Timer(_=>FlushPending(),null,
                TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(2));
        }
    }

    internal static void Enqueue(string path,bool exists)
    {
        try
        {
            path=Path.GetFullPath(path);
            if(Interlocked.Increment(ref pendingCount)>MaximumPending)
            {
                Interlocked.Decrement(ref pendingCount);
                string? root=Path.GetPathRoot(path);
                if(!string.IsNullOrEmpty(root))DirtyVolumes[root]=0;
                return; // never let event bursts cause an unbounded heap
            }
            PendingChanges.Enqueue(new Pending(path,exists));
        }
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException
            or PathTooLongException){}
    }

    private static void FlushPending()
    {
        if(Interlocked.Exchange(ref flushing,1)!=0)return;
        try
        {
            var batch=new List<Pending>(MaximumPending);
            while(batch.Count<MaximumPending&&PendingChanges.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref pendingCount);
                batch.Add(item);
            }
            if(batch.Count!=0)
            {
                using var connection=Connect();
                using var tx=connection.BeginTransaction();
                using var upsert=connection.CreateCommand();
                upsert.Transaction=tx;
                upsert.CommandText=@"INSERT INTO entries(path,name,folder,volume,scan_epoch)
                    VALUES($path,$name,$folder,$volume,0)
                    ON CONFLICT(path) DO UPDATE SET
                     name=excluded.name,folder=excluded.folder,volume=excluded.volume,scan_epoch=0;";
                var p=upsert.Parameters.Add("$path",SqliteType.Text);
                var n=upsert.Parameters.Add("$name",SqliteType.Text);
                var f=upsert.Parameters.Add("$folder",SqliteType.Integer);
                var v=upsert.Parameters.Add("$volume",SqliteType.Text);
                using var remove=connection.CreateCommand();
                remove.Transaction=tx;
                remove.CommandText="DELETE FROM entries WHERE path=$path;";
                var rp=remove.Parameters.Add("$path",SqliteType.Text);
                foreach(var item in batch)
                {
                    bool isDirectory=item.Exists && Directory.Exists(item.Path);
                    bool isFile=!isDirectory&&item.Exists&&File.Exists(item.Path);
                    if(isFile||isDirectory)
                    {
                        p.Value=item.Path;n.Value=Path.GetFileName(item.Path);
                        f.Value=isDirectory?1:0;
                        v.Value=Path.GetPathRoot(item.Path)??"";
                        upsert.ExecuteNonQuery();
                    }
                    else
                    {
                        rp.Value=item.Path;
                        remove.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }
        catch(Exception ex) when(ex is SqliteException or IOException
            or UnauthorizedAccessException or InvalidOperationException)
        {
            // Not all events can be replayed after a transient database error.
            // Recover all affected volumes with a fresh bounded rescan.
            foreach(var eventItem in PendingChanges)
            {
                string? root=Path.GetPathRoot(eventItem.Path);
                if(!string.IsNullOrEmpty(root))DirtyVolumes[root]=0;
            }
            // A failure after dequeuing a transaction must not silently
            // lose events that were already removed from PendingChanges.
            // Recover every currently mounted volume conservatively.
            foreach(var drive in DriveInfo.GetDrives())
            {
                try{if(drive.IsReady)DirtyVolumes[drive.RootDirectory.FullName]=0;}
                catch(Exception){}
            }
        }
        finally
        {
            Interlocked.Exchange(ref flushing,0);
            foreach(var root in DirtyVolumes.Keys)
                if(DirtyVolumes.TryRemove(root,out _) && rescan!=null)
                {
                    try{rescan(root);}catch(Exception){}
                }
        }
    }

    internal static long BeginScan()
    {
        Initialize();
        return Interlocked.Increment(ref epochSequence);
    }

    internal static void WriteBatch(string root,long epoch,
        IReadOnlyList<Item> batch)
    {
        if(batch.Count==0)return;
        using var connection=Connect();
        using var tx=connection.BeginTransaction();
        using var command=connection.CreateCommand();
        command.Transaction=tx;
        command.CommandText=@"INSERT INTO entries(path,name,folder,volume,scan_epoch)
            VALUES($path,$name,$folder,$volume,$epoch)
            ON CONFLICT(path) DO UPDATE SET name=excluded.name,
               folder=excluded.folder,volume=excluded.volume,
               scan_epoch=excluded.scan_epoch;";
        var p=command.Parameters.Add("$path",SqliteType.Text);
        var n=command.Parameters.Add("$name",SqliteType.Text);
        var f=command.Parameters.Add("$folder",SqliteType.Integer);
        var v=command.Parameters.Add("$volume",SqliteType.Text);
        var e=command.Parameters.Add("$epoch",SqliteType.Integer);
        e.Value=epoch;v.Value=root;
        foreach(var item in batch)
        {
            p.Value=item.Path;n.Value=item.Name;
            f.Value=item.Folder?1:0;
            command.ExecuteNonQuery();
        }
        tx.Commit();
    }

    internal static void CompleteScan(string root,long epoch)
    {
        using var connection=Connect();
        // Remove only records NOT visited by the completed scan.
        // A failed or cancelled scan retains the previous viable index.
        using var command=connection.CreateCommand();
        command.CommandText=@"DELETE FROM entries
            WHERE volume=$root AND scan_epoch<>$epoch AND scan_epoch<>0;";
        command.Parameters.AddWithValue("$root",root);
        command.Parameters.AddWithValue("$epoch",epoch);
        command.ExecuteNonQuery();
        using var checkpoint=connection.CreateCommand();
        checkpoint.CommandText="PRAGMA wal_checkpoint(PASSIVE);";
        checkpoint.ExecuteNonQuery();
    }

    internal static IReadOnlyList<Item> Query(string? term,string? drive,
        CancellationToken cancellation,int max=MaximumReturned,bool? folders=null)
    {
        cancellation.ThrowIfCancellationRequested();
        if(SimulateReadFailureForTestV332)
            throw new IOException("R332 synthetic search-index read failure");
        max=Math.Clamp(max,1,MaximumReturned);
        var result=new List<Item>(Math.Min(max,64));
        using var connection=Connect();
        using var command=connection.CreateCommand();
        command.CommandTimeout=1;
        string search=(term??"").Trim();
        if(search.Length!=0)
        {
            // FTS5 trigram accelerates arbitrary filename and path substrings
            // of 3+ characters; shorter search terms use the normal table.
            var useTrigrams=search.Length>=3;
            // Ordinary queries search filenames only. An explicit slash
            // or drive fragment permits searching the complete path.
            bool searchPath=search.IndexOfAny(new[]{'\\','/',':'})>=0;
            command.CommandText=useTrigrams?
                @"SELECT e.name,e.path,e.folder,e.volume FROM names_fts f
                  JOIN entries e ON e.id=f.rowid
                  WHERE (f.name LIKE $search OR ($path=1 AND f.path LIKE $search))
                    AND ($drive='' OR e.volume=$drive)
                    AND ($folders IS NULL OR e.folder=$folders)
                  LIMIT $max;":
                @"SELECT name,path,folder,volume FROM entries
                  WHERE (name LIKE $search OR ($path=1 AND path LIKE $search))
                    AND ($drive='' OR volume=$drive)
                    AND ($folders IS NULL OR folder=$folders)
                  LIMIT $max;";
            command.Parameters.AddWithValue("$search","%"+search+"%");
            command.Parameters.AddWithValue("$path",searchPath?1:0);
        }
        else
        {
            command.CommandText=@"SELECT name,path,folder,volume FROM entries
                WHERE ($drive='' OR volume=$drive)
                  AND ($folders IS NULL OR folder=$folders)
                  ORDER BY folder ASC LIMIT $max;";
        }
        command.Parameters.AddWithValue("$drive",drive??"");
        command.Parameters.AddWithValue("$folders",folders.HasValue?
            (object)(folders.Value?1:0):DBNull.Value);
        command.Parameters.AddWithValue("$max",max);
        using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            result.Add(new Item(reader.GetString(0),reader.GetString(1),
                reader.GetInt64(2)!=0,reader.GetString(3)));
        }
        return result;
    }

    internal static long Count(string? volume=null)
    {
        using var connection=Connect();
        using var command=connection.CreateCommand();
        command.CommandText=volume==null?
            "SELECT count(*) FROM entries;":
            "SELECT count(*) FROM entries WHERE volume=$volume;";
        if(volume!=null)command.Parameters.AddWithValue("$volume",volume);
        return (long)(command.ExecuteScalar()??0L);
    }

    // Test hook does NOT modify the user's catalog: environment is scoped
    // to the throwaway test account via LOCALAPPDATA.
    internal static bool SimulateReadFailureForTestV332;
    internal static int PendingCount=>Volatile.Read(ref pendingCount);
    // Health counter only: never store the user's queries or paths.
    private static long queryFailures;
    internal static long QueryFailures=>Interlocked.Read(ref queryFailures);
    internal static void NoteQueryFailure()=>Interlocked.Increment(ref queryFailures);
}