using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DeskSpace;

// Save/import of named widget-only profiles. The legacy writer treated
// malformed existing JSON as an empty list and overwrote it. A separate,
// strict mutation read now prevents loss across crashes or multiple widgets.
internal static class DeskSpaceProfileStoreV281
{
    private const int Limit=24;
    private const int MaxBytes=262144;

    private static string MutexName(string file)=>
        @"Local\DeskSpace-WidgetProfiles-V281-"+
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(file).ToUpperInvariant())))[..24];

    internal static List<DeskSpaceProfileV85> ReadForWrite(string path)
    {
        if(!File.Exists(path))return new();
        var length=new FileInfo(path).Length;
        if(length<=0 || length>MaxBytes)
            throw new InvalidDataException(
                "Existing Workspace profile storage is empty or oversized. "+
                "Original file is preserved.");
        List<DeskSpaceProfileV85>? items;
        try
        {
            items=JsonSerializer.Deserialize<List<DeskSpaceProfileV85>>(
                File.ReadAllText(path,Encoding.UTF8));
        }
        catch(JsonException ex)
        {
            throw new InvalidDataException(
                "Existing Workspace profile storage is damaged. "+
                "Original file is preserved.",ex);
        }
        if(items==null || items.Count>Limit)
            throw new InvalidDataException(
                "Existing Workspace profile list is invalid. Original preserved.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in items)
        {
            try
            {
                if(item==null)throw new FormatException("Null profile.");
                DeskSpaceProfilePortabilityV201.Validate(item);
                if(!names.Add(item.Name))
                    throw new FormatException("Duplicate profile name.");
            }
            catch(FormatException ex)
            {
                throw new InvalidDataException(
                    "Existing Workspace profiles have invalid or duplicate "+
                    "records. Nothing was overwritten.",ex);
            }
        }
        return items;
    }

    private static T Mutate<T>(string path,
        Func<List<DeskSpaceProfileV85>,(List<DeskSpaceProfileV85> Rows,T Result)>
            operation,bool backupExisting)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var mutex=new Mutex(false,MutexName(path));
        bool acquired=false;
        try
        {
            try{acquired=mutex.WaitOne(TimeSpan.FromSeconds(3));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)throw new IOException(
                "Another DeskSpace process is saving profiles. "+
                "No profile data was changed.");

            // Strict mutation-read follows the cross-process lock, so
            // a second widget's latest profile can never be lost.
            var current=ReadForWrite(path);
            var (newRows,result)=operation(current);
            if(newRows.Count>Limit)
                throw new InvalidDataException("Workspace profile limit exceeded.");
            foreach(var item in newRows)
                DeskSpaceProfilePortabilityV201.Validate(item);
            var names=newRows.Select(x=>x.Name);
            if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
                newRows.Count)
                throw new InvalidDataException("Duplicate profile names.");
            var json=JsonSerializer.Serialize(newRows);
            if(Encoding.UTF8.GetByteCount(json)>MaxBytes)
                throw new InvalidDataException(
                    "Updated Workspace profiles exceed 256 KiB; existing saved.");

            // No no-op write on a zero-new-profile import.
            if(backupExisting && (result is int count && count==0))return result;
            var folder=Path.GetDirectoryName(Path.GetFullPath(path))!;
            Directory.CreateDirectory(folder);
            var temp=path+"."+Guid.NewGuid().ToString("N")+".partial";
            try
            {
                File.WriteAllText(temp,json,new UTF8Encoding(false));
                if(backupExisting && File.Exists(path))
                {
                    var backup=path+".before-import-"+
                        DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+
                        Guid.NewGuid().ToString("N")+".bak";
                    File.Copy(path,backup,false);
                }
                File.Move(temp,path,true);
            }
            finally
            {
                try{if(File.Exists(temp))File.Delete(temp);}
                catch(IOException){}
                catch(UnauthorizedAccessException){}
            }
            return result;
        }
        finally{if(acquired)mutex.ReleaseMutex();}
    }

    internal static void SaveAt(string path,DeskSpaceProfileV85 profile)
    {
        DeskSpaceProfilePortabilityV201.Validate(profile);
        Mutate(path,rows=>{
            rows.RemoveAll(x=>x.Name.Equals(profile.Name,
                StringComparison.OrdinalIgnoreCase));
            rows.Insert(0,profile);
            if(rows.Count>Limit)rows.RemoveRange(Limit,rows.Count-Limit);
            return (rows,true);
        },false);
    }

    internal static int ImportAt(string path,
        IReadOnlyList<DeskSpaceProfileV85> incoming)
    {
        if(incoming==null || incoming.Count>Limit)
            throw new FormatException("Invalid profile import count.");
        foreach(var p in incoming)DeskSpaceProfilePortabilityV201.Validate(p);
        if(incoming.Select(x=>x.Name).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=incoming.Count)
            throw new FormatException("Duplicate imported profile names.");
        return Mutate(path,rows=>{
            var known=new HashSet<string>(rows.Select(x=>x.Name),
                StringComparer.OrdinalIgnoreCase);
            var fresh=incoming.Where(x=>!known.Contains(x.Name)).ToArray();
            if(rows.Count+fresh.Length>Limit)
                throw new FormatException("Profile limit is 24; no changes made.");
            rows.AddRange(fresh);
            return (rows,fresh.Length);
        },true);
    }
}
