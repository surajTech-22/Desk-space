using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DeskSpace;

// Explicit user-saved queries only. No passive query history, cloud sync,
// credentials, or secondary index. Uses the existing universal search engine.
internal sealed class DeskSpaceSavedSearchesV339
{
    internal sealed record Preset(string Name,string Query);
    private readonly string file;
    private readonly object gate=new();
    internal const int MaximumPresets=30;
    internal DeskSpaceSavedSearchesV339(string? testPath=null)
    {
        file=testPath??Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","Search","presets-v339.json");
    }
    internal IReadOnlyList<Preset> Load()
    {
        lock(gate)
        {
            if(!File.Exists(file))return Array.Empty<Preset>();
            try
            {
                var found=JsonSerializer.Deserialize<List<Preset>>(
                    File.ReadAllText(file))??new();
                return found.Where(x=>Valid(x.Name,x.Query))
                    .GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase)
                    .Select(g=>g.First()).Take(MaximumPresets).ToArray();
            }
            catch(JsonException ex)
            {
                throw new InvalidDataException("Saved searches file has invalid JSON; original preserved.",ex);
            }
        }
    }
    private static bool Valid(string? name,string? query)=>
        !string.IsNullOrWhiteSpace(name)&&name.Trim().Length<=70 &&
        !string.IsNullOrWhiteSpace(query)&&query.Trim().Length<=256 &&
        name.IndexOfAny(new[]{'\r','\n','\0'})<0 &&
        query.IndexOf('\0')<0;
    internal void Save(string name,string query)
    {
        name=(name??"").Trim();query=(query??"").Trim();
        if(!Valid(name,query))
            throw new ArgumentException("Give a name (1–70 characters) and query (1–256 characters).");
        lock(gate)
        {
            var list=Load().ToList();
            var existing=list.FindIndex(x=>string.Equals(x.Name,name,
                StringComparison.OrdinalIgnoreCase));
            if(existing>=0)list[existing]=new Preset(name,query);
            else if(list.Count>=MaximumPresets)
                throw new InvalidOperationException("Maximum of 30 saved searches reached.");
            else list.Add(new Preset(name,query));
            Write(list);
        }
    }
    internal bool Delete(string name)
    {
        lock(gate)
        {
            var list=Load().ToList();
            int removed=list.RemoveAll(x=>string.Equals(x.Name,name,
                StringComparison.OrdinalIgnoreCase));
            if(removed>0)Write(list);
            return removed>0;
        }
    }
    private void Write(List<Preset> values)
    {
        var folder=Path.GetDirectoryName(file)
            ??throw new InvalidOperationException("No preset folder.");
        Directory.CreateDirectory(folder);
        string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(values,
                new JsonSerializerOptions{WriteIndented=true}));
            File.Move(temporary,file,true);
        }
        finally
        {
            if(File.Exists(temporary))File.Delete(temporary);
        }
    }
}
