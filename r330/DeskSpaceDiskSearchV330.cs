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
    // Single low-priority catalog reconciler per DeskSpace process.
    internal static readonly SemaphoreSlim ScanGateV368=new(1,1);
    // Queue each path only once; retain the newest state for that path.
    // A burst of duplicate watcher notifications must not generate duplicate SQL.
    private static readonly ConcurrentQueue<string> PendingChanges=new();
    private static readonly ConcurrentDictionary<string,bool> PendingStates=
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,long> LastRecoveryScanTicks=
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,byte> DirtyVolumes=
        new(StringComparer.OrdinalIgnoreCase);
    private static Timer? updateTimer;
    private static Action<string>? rescan;
    private static int pendingCount,flushing,initialized;
    private static long epochSequence=DateTime.UtcNow.Ticks;

    // Computing an exclusion path on every filesystem event must not itself
    // issue Directory.CreateDirectory against the disk.
    private static readonly string DatabaseFile=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","Search","filenames-v336.sqlite");

    // R335: exclude the filename index's OWN SQLite, WAL, SHM and journal
    // files from both scanner and watcher. Otherwise indexing writes raise
    // more watcher events, which write again and create a feedback loop.
    internal static bool IsOwnIndexPathV335(string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        try
        {
            var directory=Path.GetDirectoryName(DatabaseFile);
            if(string.IsNullOrEmpty(directory))return false;
            var prefix=Path.TrimEndingDirectorySeparator(directory)+
                Path.DirectorySeparatorChar;
            var absolute=Path.GetFullPath(path);
            return absolute.StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException){return false;}
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
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
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
                  name,content='entries',content_rowid='id',tokenize='trigram');
                CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN
                  INSERT INTO names_fts(rowid,name) VALUES(new.id,new.name);
                END;
                CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN
                  INSERT INTO names_fts(names_fts,rowid,name)
                    VALUES('delete',old.id,old.name);
                END;
                CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries
                    WHEN old.name<>new.name BEGIN
                  INSERT INTO names_fts(names_fts,rowid,name)
                    VALUES('delete',old.id,old.name);
                  INSERT INTO names_fts(rowid,name) VALUES(new.id,new.name);
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
            if(IsOwnIndexPathV335(path))return;
            while(true)
            {
                // Coalesce repeated notifications without losing a newer
                // rename/create/delete state while a flush is in progress.
                if(PendingStates.TryGetValue(path,out var previous))
                {
                    if(PendingStates.TryUpdate(path,exists,previous))return;
                    continue;
                }
                if(!PendingStates.TryAdd(path,exists))continue;
                if(Interlocked.Increment(ref pendingCount)>MaximumPending)
                {
                    Interlocked.Decrement(ref pendingCount);
                    PendingStates.TryRemove(path,out _);
                    string? root=Path.GetPathRoot(path);
                    if(!string.IsNullOrEmpty(root))DirtyVolumes[root]=0;
                    return;
                }
                PendingChanges.Enqueue(path);
                return;
            }
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
            while(batch.Count<MaximumPending&&PendingChanges.TryDequeue(out var path))
            {
                if(!PendingStates.TryRemove(path,out var exists))continue;
                Interlocked.Decrement(ref pendingCount);
                batch.Add(new Pending(path,exists));
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
                     name=excluded.name,folder=excluded.folder,volume=excluded.volume,scan_epoch=0
                    WHERE entries.scan_epoch<>0 OR entries.name<>excluded.name
                       OR entries.folder<>excluded.folder OR entries.volume<>excluded.volume;";
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
            foreach(var eventPath in PendingChanges)
            {
                string? root=Path.GetPathRoot(eventPath);
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
            {
                if(rescan==null)break;
                long now=Environment.TickCount64;
                // Overflow recovery must not become a permanent full-drive
                // rescan loop under sustained filesystem-event pressure.
                if(LastRecoveryScanTicks.TryGetValue(root,out var last) &&
                   now-last<TimeSpan.FromMinutes(2).TotalMilliseconds)continue;
                if(!DirtyVolumes.TryRemove(root,out _))continue;
                LastRecoveryScanTicks[root]=now;
                try{rescan(root);}
                catch(Exception){DirtyVolumes[root]=0;}
            }
        }
    }

    // R368: keep the unchanged catalog rows untouched during full
    // reconciliation. The previous scan_epoch strategy rewrote every
    // existing row, generating huge SQLite WAL/disk I/O on each startup.
    // A temporary compact row-id set tracks which records were seen. Only
    // new/changed rows modify the durable database; only safely completed
    // scans may delete missing rows. Live watcher rows (epoch 0) are kept.
    internal sealed class FullScanSessionV368 : IDisposable
    {
        private readonly SqliteConnection db;
        private readonly SqliteCommand addOrUpdate;
        private readonly SqliteCommand markSeen;
        private readonly string root;
        private readonly long epoch;
        private int batches;
        private bool finished;

        internal FullScanSessionV368(string root)
        {
            this.root=root;
            epoch=Interlocked.Increment(ref epochSequence);
            db=Connect();
            try
            {
                using(var create=db.CreateCommand())
                {
                    create.CommandText=@"CREATE TEMP TABLE seen_v368(
                        id INTEGER PRIMARY KEY);";
                    create.ExecuteNonQuery();
                }
                addOrUpdate=db.CreateCommand();
                addOrUpdate.CommandText=@"INSERT INTO entries(
                    path,name,folder,volume,scan_epoch)
                    VALUES($path,$name,$folder,$root,$epoch)
                    ON CONFLICT(path) DO UPDATE SET
                        name=excluded.name,folder=excluded.folder,
                        volume=excluded.volume
                    WHERE entries.name<>excluded.name
                       OR entries.folder<>excluded.folder
                       OR entries.volume<>excluded.volume;";
                addOrUpdate.Parameters.Add("$path",SqliteType.Text);
                addOrUpdate.Parameters.Add("$name",SqliteType.Text);
                addOrUpdate.Parameters.Add("$folder",SqliteType.Integer);
                addOrUpdate.Parameters.AddWithValue("$root",root);
                addOrUpdate.Parameters.AddWithValue("$epoch",epoch);
                markSeen=db.CreateCommand();
                markSeen.CommandText=@"INSERT OR IGNORE INTO temp.seen_v368(id)
                    SELECT id FROM entries WHERE path=$path;";
                markSeen.Parameters.Add("$path",SqliteType.Text);
            }
            catch{db.Dispose();throw;}
        }
        internal void WriteBatch(IReadOnlyList<Item> batch)
        {
            if(finished)throw new InvalidOperationException("Scan already complete.");
            if(batch.Count==0)return;
            using var tx=db.BeginTransaction();
            addOrUpdate.Transaction=tx;
            markSeen.Transaction=tx;
            var path=addOrUpdate.Parameters["$path"];
            var name=addOrUpdate.Parameters["$name"];
            var folder=addOrUpdate.Parameters["$folder"];
            var seenPath=markSeen.Parameters["$path"];
            foreach(var item in batch)
            {
                path.Value=item.Path;
                name.Value=item.Name;
                folder.Value=item.Folder?1:0;
                seenPath.Value=item.Path;
                addOrUpdate.ExecuteNonQuery();
                markSeen.ExecuteNonQuery();
            }
            tx.Commit();
            // Keep all-drive coverage and live updates, but let other
            // processes and the UI breathe during background reconciliation.
            if((++batches&1)==0)Thread.Sleep(12);
        }
        internal void Complete()
        {
            if(finished)throw new InvalidOperationException("Scan already complete.");
            using(var cmd=db.CreateCommand())
            {
                cmd.CommandTimeout=120;
                cmd.CommandText=@"DELETE FROM entries
                    WHERE volume=$root AND scan_epoch<>0
                    AND NOT EXISTS(
                      SELECT 1 FROM temp.seen_v368 s WHERE s.id=entries.id);";
                cmd.Parameters.AddWithValue("$root",root);
                cmd.ExecuteNonQuery();
            }
            using(var checkpoint=db.CreateCommand())
            {
                checkpoint.CommandText="PRAGMA wal_checkpoint(PASSIVE);";
                checkpoint.ExecuteNonQuery();
            }
            finished=true;
        }
        public void Dispose()
        {
            addOrUpdate?.Dispose();
            markSeen?.Dispose();
            db.Dispose();
        }
    }

    internal static FullScanSessionV368 BeginLowWriteScanV368(string root)=>
        new(root);

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
            // Path trigrams previously caused multi-gigabyte cache growth.
            // Normal filename queries use a small name-only FTS; explicit
            // path queries use the ordinary path column instead.
            command.CommandText=useTrigrams && !searchPath ?
                @"SELECT e.name,e.path,e.folder,e.volume FROM names_fts f
                  JOIN entries e ON e.id=f.rowid
                  WHERE f.name LIKE $search
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