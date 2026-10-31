using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DeskSpace;

// R228: per-screen ONLY Deck geometry; held files and all business data
// remain in their existing shared store. No mirrored offset state.
internal static class DeskSpaceDeckOffsetV228
{
    internal sealed record Offset(int X,int Y);
    private static readonly object Sync=new();
    private static string FilePath=>Path.Combine(WorkspaceStore.Root,
        "deck-per-monitor-v228.json");
    // Both monitor widgets can be hosted by distinct DeskSpace processes.
    // The old lock(Sync) covered only threads in ONE process: competing
    // read-modify-write saves could overwrite the other display's offset.
    // Name the OS mutex by canonical local store, not by widget/device.
    private static string MutexName=>
        @"Local\DeskSpace-DeckOffsetMap-V235-"+
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(FilePath).ToUpperInvariant())))[..24];
    internal static Offset Clamp(Offset shift,int max=4096)=>
        new(Math.Clamp(shift.X,-max,max),Math.Clamp(shift.Y,-max,max));

    // Parses the entire existing saved Deck layout before any mutation.
    // Corrupt, oversized, duplicate-cased or invalid data must never be
    // silently overwritten with a new empty map.
    internal static Dictionary<string,Offset> ParseForWrite(string? json,int byteLength)
    {
        if(byteLength<0 || byteLength>16_384)
            throw new InvalidDataException("Saved Deck positions exceed 16 KiB.");
        if(json==null)
            return new Dictionary<string,Offset>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,Offset>? values;
        try{values=JsonSerializer.Deserialize<Dictionary<string,Offset>>(json);}
        catch(JsonException ex)
        {
            throw new InvalidDataException("Saved Deck positions are not valid JSON.",ex);
        }
        if(values==null || values.Count>128)
            throw new InvalidDataException("Saved Deck position list is invalid or too large.");
        var result=new Dictionary<string,Offset>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in values)
        {
            if(string.IsNullOrWhiteSpace(item.Key) || item.Key.Length>128 ||
               item.Value==null || !result.TryAdd(item.Key,Clamp(item.Value)))
                throw new InvalidDataException("Saved Deck positions contain duplicate or invalid monitor identity.");
        }
        return result;
    }
    internal static Offset Read(string device)
    {
        if(string.IsNullOrWhiteSpace(device))return new(0,0);
        lock(Sync)
        {
            try
            {
                if(!File.Exists(FilePath) || new FileInfo(FilePath).Length>16_384)
                    return new(0,0);
                var map=ParseForWrite(File.ReadAllText(FilePath),
                    checked((int)new FileInfo(FilePath).Length));
                return map.TryGetValue(device,out var found)?
                    Clamp(found):new(0,0);
            }
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException or InvalidDataException){return new(0,0);}
        }
    }
    internal static void Save(string device,Offset value)
    {
        if(string.IsNullOrWhiteSpace(device) || device.Length>128)return;
        lock(Sync)
        {
            using var crossProcess=new Mutex(false,MutexName);
            bool acquired;
            try{acquired=crossProcess.WaitOne(TimeSpan.FromMilliseconds(250));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)
                throw new IOException("DeskSpace Deck offset file is busy; no position was overwritten.");
            try
            {
                Directory.CreateDirectory(WorkspaceStore.Root);
            // The cross-process mutex is already held here (R235).
            // Preserve an existing malformed or oversized file for manual
            // recovery; do NOT overwrite independent positions on error.
            string? previous=null;
            int bytes=0;
            if(File.Exists(FilePath))
            {
                var size=new FileInfo(FilePath).Length;
                if(size>16_384)
                    throw new InvalidDataException("Existing Deck map is oversized; its contents were preserved.");
                bytes=checked((int)size);
                previous=File.ReadAllText(FilePath);
            }
            var map=ParseForWrite(previous,bytes);
            map[device]=Clamp(value);
            var serialized=JsonSerializer.Serialize(map);
            if(System.Text.Encoding.UTF8.GetByteCount(serialized)>16_384)
                throw new InvalidDataException("Updated Deck map exceeds 16 KiB; original kept.");
            var tmp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(tmp,serialized);
                File.Move(tmp,FilePath,true);
            }
                finally{if(File.Exists(tmp))File.Delete(tmp);}
            }
            finally{crossProcess.ReleaseMutex();}
        }
    }
}
