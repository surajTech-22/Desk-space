using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskSpace;

// Windows already persists recently accessed document shortcuts in Recent.
// Only surface documents whose registered file-handler executable matches a
// pinned application. Never run a recent .lnk or arbitrary executable.
internal static class DeskSpacePinnedRecentV122
{
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]
    private static extern int AssocQueryString(
        int flags,int str,string assoc,string? extra,
        StringBuilder? output,ref uint length);
    private static readonly Dictionary<string,(DateTime Until,string[] Paths)> Cache=
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate=new();
    private static readonly string[] Unsafe={".exe",".bat",".cmd",".ps1",
        ".vbs",".js",".wsf",".scr",".msi",".lnk",".url",".com",".hta"};

    internal static bool IsSafeDocument(string path)
    {
        try
        {
            if(!File.Exists(path) || Unsafe.Contains(
                Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))
                return false;
            if(path.Contains("\0"))return false;
            return Path.IsPathFullyQualified(path);
        }
        catch{return false;}
    }
    private static string ResolveShortcut(string path)
    {
        object? shell=null;
        try
        {
            var type=Type.GetTypeFromProgID("WScript.Shell");
            if(type==null)return "";
            shell=Activator.CreateInstance(type);
            if(shell==null)return "";
            dynamic win=shell;
            var shortcut=win.CreateShortcut(path);
            return Convert.ToString((object?)shortcut.TargetPath)??"";
        }
        catch{return "";}
        finally{if(shell!=null&&Marshal.IsComObject(shell))
            Marshal.FinalReleaseComObject(shell);}
    }
    private static string AppExe(string path)
    {
        if(!File.Exists(path))return "";
        string resolved=Path.GetExtension(path).Equals(".lnk",
            StringComparison.OrdinalIgnoreCase)?ResolveShortcut(path):path;
        try{return Path.GetFullPath(resolved);}
        catch{return "";}
    }
    private static string DefaultHandler(string extension)
    {
        try
        {
            uint size=0;
            _=AssocQueryString(0,2,extension,null,null,ref size);
            if(size<2||size>32767)return "";
            var builder=new StringBuilder((int)size);
            if(AssocQueryString(0,2,extension,null,builder,ref size)!=0)
                return "";
            return builder.ToString();
        }
        catch{return "";}
    }
    internal static bool FileBelongsToApp(string pinnedApp,string recentDocument)
    {
        if(!IsSafeDocument(recentDocument))return false;
        string handler=DefaultHandler(Path.GetExtension(recentDocument));
        string exe=AppExe(pinnedApp);
        if(handler.Length==0||exe.Length==0)return false;
        try{return string.Equals(Path.GetFullPath(handler),
            Path.GetFullPath(exe),StringComparison.OrdinalIgnoreCase);}
        catch{return false;}
    }
    internal static IReadOnlyList<string> FilesFor(string pinnedApp,int limit=5)
    {
        if(limit<1)return Array.Empty<string>();
        limit=Math.Min(limit,8);
        lock(Gate)
        {
            if(Cache.TryGetValue(pinnedApp,out var cached) &&
               cached.Until>DateTime.UtcNow)
                return cached.Paths.Take(limit).ToArray();
        }
        var found=new List<string>();
        try
        {
            string recent=Environment.GetFolderPath(
                Environment.SpecialFolder.Recent);
            if(Directory.Exists(recent))
            {
                foreach(var shortcut in new DirectoryInfo(recent)
                    .EnumerateFiles("*.lnk")
                    .OrderByDescending(x=>x.LastWriteTimeUtc).Take(180))
                {
                    string target=ResolveShortcut(shortcut.FullName);
                    if(!FileBelongsToApp(pinnedApp,target) ||
                       found.Contains(target,StringComparer.OrdinalIgnoreCase))
                        continue;
                    found.Add(target);
                    if(found.Count>=8)break;
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
        lock(Gate)Cache[pinnedApp]=(DateTime.UtcNow.AddSeconds(45),
            found.ToArray());
        return found.Take(limit).ToArray();
    }
    internal static bool OpenDocumentForPinnedApp(string pinnedApp,string path)
    {
        if(!FileBelongsToApp(pinnedApp,path))return false;
        string exe=AppExe(pinnedApp);
        var start=new ProcessStartInfo(exe){UseShellExecute=true};
        start.ArgumentList.Add(path);
        Process.Start(start);
        return true;
    }
}
