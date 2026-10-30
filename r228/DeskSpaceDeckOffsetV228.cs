using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DeskSpace;

// R228: per-screen ONLY Deck geometry; held files and all business data
// remain in their existing shared store. No mirrored offset state.
internal static class DeskSpaceDeckOffsetV228
{
    internal sealed record Offset(int X,int Y);
    private static readonly object Sync=new();
    private static string FilePath=>Path.Combine(WorkspaceStore.Root,
        "deck-per-monitor-v228.json");
    internal static Offset Clamp(Offset shift,int max=4096)=>
        new(Math.Clamp(shift.X,-max,max),Math.Clamp(shift.Y,-max,max));
    internal static Offset Read(string device)
    {
        if(string.IsNullOrWhiteSpace(device))return new(0,0);
        lock(Sync)
        {
            try
            {
                if(!File.Exists(FilePath) || new FileInfo(FilePath).Length>16_384)
                    return new(0,0);
                var map=JsonSerializer.Deserialize<Dictionary<string,Offset>>(
                    File.ReadAllText(FilePath));
                return map!=null&&map.TryGetValue(device,out var found)?
                    Clamp(found):new(0,0);
            }
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException){return new(0,0);}
        }
    }
    internal static void Save(string device,Offset value)
    {
        if(string.IsNullOrWhiteSpace(device) || device.Length>128)return;
        lock(Sync)
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            Dictionary<string,Offset> map=new(StringComparer.OrdinalIgnoreCase);
            try
            {
                if(File.Exists(FilePath) && new FileInfo(FilePath).Length<=16_384)
                    map=JsonSerializer.Deserialize<Dictionary<string,Offset>>(
                        File.ReadAllText(FilePath))??map;
            }
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException){}
            map[device]=Clamp(value);
            var tmp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(tmp,JsonSerializer.Serialize(map));
                File.Move(tmp,FilePath,true);
            }
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
    }
}
