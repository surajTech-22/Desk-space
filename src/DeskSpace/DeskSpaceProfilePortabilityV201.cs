using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace DeskSpace;

// R201: portable appearance/pin profiles only. There are NO workspace,
// reminder, credential, note, clipboard or Excel objects in this format.
// Nothing is applied to a live widget by importing profiles.
internal static class DeskSpaceProfilePortabilityV201
{
    internal const string Schema="deskspace-widget-profiles-v1";
    private sealed class Document
    {
        public string Format {get;set;}=Schema;
        public List<DeskSpaceProfileV85> Profiles {get;set;}=new();
    }

    internal static IReadOnlyList<DeskSpaceProfileV85> Parse(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>262144)
            throw new FormatException("Profile file is empty or exceeds 256 KiB.");
        using var parsed=JsonDocument.Parse(json,new JsonDocumentOptions
        {
            MaxDepth=8,AllowTrailingCommas=false,
            CommentHandling=JsonCommentHandling.Disallow
        });
        var root=parsed.RootElement;
        if(root.ValueKind!=JsonValueKind.Object ||
           !root.TryGetProperty("Format",out var fmt) ||
           fmt.ValueKind!=JsonValueKind.String ||
           fmt.GetString()!=Schema ||
           !root.TryGetProperty("Profiles",out var entries) ||
           entries.ValueKind!=JsonValueKind.Array ||
           entries.GetArrayLength()>24 ||
           root.EnumerateObject().Any(x=>x.Name is not ("Format" or "Profiles")))
            throw new FormatException("Unsupported widget-only profile format.");
        var models=new List<DeskSpaceProfileV85>();
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in entries.EnumerateArray())
        {
            if(entry.ValueKind!=JsonValueKind.Object ||
               entry.EnumerateObject().Any(x=>x.Name is not (
                "Name" or "CenterMode" or "ShowWeather" or "WeatherCity" or "PinnedApps")))
                throw new FormatException("Unsupported profile property.");
            var profile=entry.Deserialize<DeskSpaceProfileV85>()
                ?? throw new FormatException("Invalid profile.");
            Validate(profile);
            if(!names.Add(profile.Name))
                throw new FormatException("Duplicate profile name.");
            models.Add(profile);
        }
        return models;
    }

    internal static void Validate(DeskSpaceProfileV85 item)
    {
        if(item.Name==null || item.Name.Length<1 || item.Name.Length>48 ||
           item.Name.Any(char.IsControl) ||
           string.IsNullOrWhiteSpace(item.Name) ||
           item.CenterMode==null || item.CenterMode.Length>64 ||
           item.CenterMode.Any(char.IsControl) ||
           item.WeatherCity==null || item.WeatherCity.Length>120 ||
           item.WeatherCity.Any(char.IsControl) ||
           item.PinnedApps==null || item.PinnedApps.Count>40)
            throw new FormatException("Invalid profile name or field limits.");
        foreach(var path in item.PinnedApps)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Length>2048||
               path.Any(char.IsControl)||!Path.IsPathFullyQualified(path))
                throw new FormatException("Only bounded absolute pinned-app paths are supported.");
        }
    }
    internal static string ExportJson()
    {
        var profiles=DeskSpaceWorkspaceProfilesV85.Load().ToList();
        foreach(var p in profiles)Validate(p);
        return JsonSerializer.Serialize(new Document{Profiles=profiles},
            new JsonSerializerOptions{WriteIndented=true});
    }
    internal static void ExportWithDialog(Window owner)
    {
        if(MessageBox.Show(owner,
           "Export widget profile names, appearance settings and pinned "+
           "application paths? Paths may reveal your Windows username. "+
           "No passwords, notes, Excel data or reminders will be exported.",
           "Export DeskSpace widget profiles",MessageBoxButton.YesNo,
           MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        var dialog=new SaveFileDialog
        {
            Title="Export DeskSpace widget profiles",Filter="JSON files|*.json",
            FileName="DeskSpace-Widget-Profiles.json",AddExtension=true,
            OverwritePrompt=true
        };
        if(dialog.ShowDialog(owner)!=true)return;
        try
        {
            var payload=ExportJson();
            var target=dialog.FileName;
            string tmp=target+"."+Guid.NewGuid().ToString("N")+".partial";
            try
            {
                File.WriteAllText(tmp,payload,new UTF8Encoding(false));
                File.Move(tmp,target,true);
            }
            finally{if(File.Exists(tmp))File.Delete(tmp);}
            MessageBox.Show(owner,"Widget profiles exported. Review before sharing.",
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or FormatException or ArgumentException)
        {
            MessageBox.Show(owner,"Export not completed: "+ex.GetType().Name,
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    internal static void ImportWithDialog(Window owner)
    {
        var dialog=new OpenFileDialog
        {
            Title="Select DeskSpace widget profile JSON",
            Filter="JSON files|*.json",Multiselect=false,CheckFileExists=true
        };
        if(dialog.ShowDialog(owner)!=true)return;
        try
        {
            var info=new FileInfo(dialog.FileName);
            if(info.Length<=0||info.Length>262144)
                throw new FormatException("The selected profile file is too large.");
            var profiles=Parse(File.ReadAllText(info.FullName,Encoding.UTF8));
            var current=DeskSpaceWorkspaceProfilesV85.Load();
            var existing=new HashSet<string>(current.Select(x=>x.Name),
                StringComparer.OrdinalIgnoreCase);
            int newCount=profiles.Count(x=>!existing.Contains(x.Name));
            if(MessageBox.Show(owner,
                $"Import {newCount} new widget profiles? "+
                "Existing names will NOT be overwritten; no saved profile "+
                "will be applied or launched automatically.",
                "Import DeskSpace widget profiles",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            int added=DeskSpaceWorkspaceProfilesV85.ImportNewProfilesV201(profiles);
            MessageBox.Show(owner,$"Imported {added} new profiles. "+
                "Existing widget settings and data were not changed.",
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or FormatException or JsonException or ArgumentException)
        {
            MessageBox.Show(owner,"Import rejected: "+ex.GetType().Name,
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
}
