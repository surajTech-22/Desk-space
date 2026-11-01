using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Reads actual Windows Recent Document shortcuts, NOT arbitrary private
// app files. It opens only a document whose registered default application
// matches the selected pinned executable. Never opens or reads the document
// itself during discovery; fallback launches the original pinned program.
internal static class DeskSpacePinnedRecentV122
{
    private sealed class Record
    {
        public string Application {get;set;}="";
        public string LatestDocument {get;set;}="";
        public DateTime SeenUtc {get;set;}
    }
    private static readonly string Cache=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","PinnedRecentPaths.json");
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,EntryPoint="AssocQueryStringW")]
    private static extern int AssocQueryString(uint flags,uint query,
        string association,string? extra,StringBuilder? buffer,ref uint chars);
    internal static bool SameExecutableV122(string handler,string pinned)
    {
        if(string.IsNullOrWhiteSpace(handler)||string.IsNullOrWhiteSpace(pinned))
            return false;
        try {
            string h=Path.GetFileNameWithoutExtension(handler.Trim().Trim('"'));
            string p=Path.GetFileNameWithoutExtension(pinned.Trim().Trim('"'));
            return h.Length>1&&p.Length>1&&
                string.Equals(h,p,StringComparison.OrdinalIgnoreCase);
        }catch{return false;}
    }
    private static string ShortcutTarget(string path)
    {
        try {
            var type=Type.GetTypeFromProgID("WScript.Shell");
            if(type==null)return "";
            dynamic wsh=Activator.CreateInstance(type)!;
            dynamic link=wsh.CreateShortcut(path);
            return (string)link.TargetPath;
        }catch{return "";}
    }
    private static string AssociatedExe(string document)
    {
        string ext=Path.GetExtension(document);
        if(ext.Length<2)return "";
        try {
            uint length=0;
            AssocQueryString(0,2,ext,null,null,ref length);
            if(length==0 || length>2048)return "";
            var buffer=new StringBuilder((int)length);
            return AssocQueryString(0,2,ext,null,buffer,ref length)==0?
                buffer.ToString():"";
        }catch{return "";}
    }
    private static string PinnedExe(string path)
    {
        if(Path.GetExtension(path).Equals(".lnk",
            StringComparison.OrdinalIgnoreCase))
            return ShortcutTarget(path);
        return path;
    }
    internal static string PickMatchedV122(string pinnedExe,
        IEnumerable<(string Path,DateTime LastUsed,string Executable)> documents)
    {
        return documents.Where(x=>
                File.Exists(x.Path) &&
                SameExecutableV122(x.Executable,pinnedExe))
            .OrderByDescending(x=>x.LastUsed)
            .Select(x=>x.Path).FirstOrDefault()??"";
    }
    // Explicit picker queries only this pinned app on demand. It does not
    // update or read the persistent single-latest-document cache.
    internal static IReadOnlyList<DeskSpacePinnedDocumentChoiceV262.Candidate>
        CandidatesV262(string pinned)
    {
        if(!File.Exists(pinned))return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
        string executable=PinnedExe(pinned);
        if(!File.Exists(executable) ||
            !Path.GetExtension(executable).Equals(".exe",
                StringComparison.OrdinalIgnoreCase))
            return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
        try
        {
            var folder=Environment.GetFolderPath(
                Environment.SpecialFolder.Recent);
            if(!Directory.Exists(folder))
                return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
            var entries=new List<DeskSpacePinnedDocumentChoiceV262.Candidate>();
            foreach(var shortcut in Directory.EnumerateFiles(folder,"*.lnk")
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(70))
            {
                string target=ShortcutTarget(shortcut);
                if(!DeskSpacePinnedDocumentChoiceV262.SafeDocument(target))
                    continue;
                entries.Add(new(target,File.GetLastWriteTimeUtc(shortcut),
                    AssociatedExe(target)));
            }
            return DeskSpacePinnedDocumentChoiceV262.Filter(executable,entries);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or ArgumentException)
        {return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();}
    }

    internal static bool CanLaunchRecentV262(string pinned,string document)
    {
        if(!File.Exists(pinned) ||
            !DeskSpacePinnedDocumentChoiceV262.SafeDocument(document))
            return false;
        string executable=PinnedExe(pinned);
        return File.Exists(executable) &&
            DeskSpacePinnedRecentV122.SameExecutableV122(
                AssociatedExe(document),executable);
    }

    private static List<Record> ReadCache()
    {
        try {
            if(!File.Exists(Cache))return new();
            return JsonSerializer.Deserialize<List<Record>>(
                File.ReadAllText(Cache))??new();
        }catch(IOException){return new();}
        catch(System.Text.Json.JsonException){return new();}
    }
    private static void Remember(string app,string document)
    {
        try {
            var list=ReadCache();
            list.RemoveAll(x=>string.Equals(x.Application,app,
                StringComparison.OrdinalIgnoreCase));
            list.Add(new Record{Application=app,
                LatestDocument=document,SeenUtc=DateTime.UtcNow});
            list=list.OrderByDescending(x=>x.SeenUtc).Take(30).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(Cache)!);
            string temp=Cache+".tmp";
            File.WriteAllText(temp,JsonSerializer.Serialize(list));
            File.Move(temp,Cache,true);
        }catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
    // A user click on a pinned app is the only occasion this is called.
    // Unlike an app watcher, there is no continuously scanning file content.
    internal static bool OpenLatestOrApp(string pinned)
    {
        if(!File.Exists(pinned))return false;
        string exe=PinnedExe(pinned);
        if(!File.Exists(exe))
        {
            Process.Start(new ProcessStartInfo(pinned){UseShellExecute=true});
            return true;
        }
        string recentFolder=Environment.GetFolderPath(
            Environment.SpecialFolder.Recent);
        string selected="";
        if(Directory.Exists(recentFolder))
        {
            var candidates=Directory.EnumerateFiles(recentFolder,"*.lnk")
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(70);
            var docs=new List<(string Path,DateTime LastUsed,string Executable)>();
            foreach(var shortcut in candidates)
            {
                string file=ShortcutTarget(shortcut);
                if(!File.Exists(file) ||
                   file.Equals(exe,StringComparison.OrdinalIgnoreCase))
                    continue;
                docs.Add((file,File.GetLastWriteTimeUtc(shortcut),
                    AssociatedExe(file)));
            }
            selected=PickMatchedV122(exe,docs);
        }
        if(selected.Length==0)
        {
            var remembered=ReadCache().FirstOrDefault(x=>
                string.Equals(x.Application,pinned,
                    StringComparison.OrdinalIgnoreCase));
            if(remembered!=null && File.Exists(remembered.LatestDocument) &&
               SameExecutableV122(AssociatedExe(remembered.LatestDocument),exe))
                selected=remembered.LatestDocument;
        }
        if(selected.Length>0)
        {
            Remember(pinned,selected);
            Process.Start(new ProcessStartInfo(selected){UseShellExecute=true});
        }
        else
            Process.Start(new ProcessStartInfo(pinned){UseShellExecute=true});
        return true;
    }
}
