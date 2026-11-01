using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DeskSpace;

// P0: one monitor's widget coordinates must never replace another's.
// Shares the existing v154 storage format, but serializes read-modify-write
// across processes and refuses to overwrite malformed existing records.
internal static class DeskSpaceMonitorPositionStoreV278
{
    internal sealed record Position(string Device,int X,int Y);
    private const int MaxPositions=64;
    private const int MaxBytes=16384;
    private static string FilePath=>Path.Combine(WorkspaceStore.Root,
        "widget-independent-displays-v154.json");

    private static string MutexName(string path)=>
        @"Local\DeskSpace-MonitorPositions-V278-"+
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(path).ToUpperInvariant())))[..24];

    private static bool GoodDevice(string? name)=>
        !string.IsNullOrWhiteSpace(name) && name.Length<=128 &&
        !name.Any(char.IsControl);

    internal static IReadOnlyList<Position> ParseForWrite(
        string? json,long bytes)
    {
        if(bytes<0||bytes>MaxBytes)
            throw new InvalidDataException("Monitor position map exceeds 16 KiB.");
        if(json==null)return Array.Empty<Position>();
        List<Position>? rows;
        try{rows=JsonSerializer.Deserialize<List<Position>>(json);}
        catch(JsonException ex)
        {throw new InvalidDataException("Monitor position map is invalid JSON.",ex);}
        if(rows==null||rows.Count>MaxPositions)
            throw new InvalidDataException("Monitor position map is empty or too large.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows)
        {
            if(row==null || !GoodDevice(row.Device) ||
               !seen.Add(row.Device) ||
               Math.Abs((long)row.X)>1000000 ||
               Math.Abs((long)row.Y)>1000000)
                throw new InvalidDataException(
                    "Monitor positions contain invalid or duplicate identities.");
        }
        return rows;
    }

    internal static Position? ReadAt(string path,string device)
    {
        if(!GoodDevice(device))return null;
        try
        {
            if(!File.Exists(path))return null;
            var length=new FileInfo(path).Length;
            var rows=ParseForWrite(File.ReadAllText(path),length);
            return rows.FirstOrDefault(row=>string.Equals(
                row.Device,device,StringComparison.OrdinalIgnoreCase));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or NotSupportedException)
        {
            // Never invent another monitor's position for this display.
            System.Diagnostics.Debug.WriteLine(
                "DeskSpace monitor position read refused: "+ex.GetType().Name);
            return null;
        }
    }

    internal static Position? Read(string device)=>ReadAt(FilePath,device);

    internal static void SaveAt(string path,string device,int x,int y)
    {
        if(!GoodDevice(device) ||
           Math.Abs((long)x)>1000000 || Math.Abs((long)y)>1000000)
            throw new ArgumentException("Invalid monitor identity or coordinate.");
        using var gate=new Mutex(false,MutexName(path));
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(2));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)
                throw new IOException("Monitor position store is busy; data preserved.");
            var existing=File.Exists(path);
            IReadOnlyList<Position> rows=existing?
                ParseForWrite(File.ReadAllText(path),
                    new FileInfo(path).Length):
                Array.Empty<Position>();
            // Keep unrelated monitors byte-for-byte equivalent in position.
            var next=rows.Where(row=>!string.Equals(
                row.Device,device,StringComparison.OrdinalIgnoreCase)).ToList();
            if(next.Count>=MaxPositions)
                throw new InvalidDataException("Monitor position limit reached.");
            next.Add(new Position(device,x,y));
            var content=JsonSerializer.Serialize(next);
            if(Encoding.UTF8.GetByteCount(content)>MaxBytes)
                throw new InvalidDataException("Monitor position map is too large.");
            var folder=Path.GetDirectoryName(Path.GetFullPath(path))!;
            Directory.CreateDirectory(folder);
            var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,content,new UTF8Encoding(false));
                File.Move(temp,path,true);
            }
            finally
            {
                try{if(File.Exists(temp))File.Delete(temp);}
                catch(IOException){}
                catch(UnauthorizedAccessException){}
            }
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }

    internal static void Save(string device,int x,int y)=>
        SaveAt(FilePath,device,x,y);
}
