using System;
using System.IO;
using System.Text.Json;

namespace DeskSpace;

// A reminder still owns its exact sound/action in ReminderItem. These
// small per-user preferences only supply defaults to the *next* reminder.
internal static class DeskSpaceReminderPreferencesV81
{
    private sealed class SoundOptions
    {
        public string Name {get;set;}="Exclamation";
        public string File {get;set;}="";
    }
    private static readonly string Destination=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskSpace","reminder-sound-preferences.json");

    internal static (string Name,string File) Load()
    {
        try
        {
            if(!System.IO.File.Exists(Destination))
                return ("Exclamation","");
            var value=JsonSerializer.Deserialize<SoundOptions>(
                System.IO.File.ReadAllText(Destination));
            if(value==null)return ("Exclamation","");
            string name=value.Name is "Exclamation" or "Beep" or "Asterisk"
                or "Hand" or "None" or "Custom WAV"
                ?value.Name:"Exclamation";
            string path=string.IsNullOrWhiteSpace(value.File)?"":value.File;
            return (name,path);
        }
        catch{return ("Exclamation","");}
    }

    internal static bool Save(string name,string? wav,out string error)
    {
        error="";
        try
        {
            var folder=Path.GetDirectoryName(Destination)!;
            Directory.CreateDirectory(folder);
            var json=JsonSerializer.Serialize(new SoundOptions{
                Name=name,File=wav??""
            });
            var temp=Destination+".tmp";
            System.IO.File.WriteAllText(temp,json);
            System.IO.File.Move(temp,Destination,true);
            return true;
        }
        catch(Exception e){error=e.Message;return false;}
    }

    internal static string SoundPath=>Destination;
}

// Generates labels from chosen action targets, never shows raw paths in the
// picker and never requires a task name when an action is selected.
internal static class DeskSpaceReminderPresentationV81
{
    internal static bool NeedsTitle(string? action)
        =>string.IsNullOrWhiteSpace(action)||action=="None";

    internal static string TargetName(string? action,string? target,
        string? searchName=null)
    {
        if(action=="Open URL")
        {
            if(DeskSpaceReminderBrowserV80.TryUrl(target,out var normalized))
                return new Uri(normalized).Host;
            return "";
        }
        if(action is not ("Open application" or "Open file"))
            return "";
        if(string.IsNullOrWhiteSpace(target))return "";
        string filename=Path.GetFileName(target.TrimEnd(
            Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
        if(action=="Open file")return filename;
        string name=string.IsNullOrWhiteSpace(searchName)?
            Path.GetFileNameWithoutExtension(filename):searchName.Trim();
        if(name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))
            name=Path.GetFileNameWithoutExtension(name);
        return name;
    }

    internal static string ActionLabel(string? action,string? target,
        string? searchName=null)
    {
        string name=TargetName(action,target,searchName);
        return string.IsNullOrEmpty(name)?"":action+" · "+name;
    }

    internal static string ResolveTitle(string? title,string? action,
        string? target,string? searchName=null)
    {
        if(!NeedsTitle(action))
            return ActionLabel(action,target,searchName);
        return title?.Trim()??"";
    }
}
