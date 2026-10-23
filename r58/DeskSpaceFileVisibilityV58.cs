using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// Shared default-visibility policy for the automatic recent-file shelf and
// file Search. This is NOT a file deletion / access-denial policy: explicitly
// selected files may still be handled by other features.
internal static class DeskSpaceFileVisibilityV58
{
    private static readonly HashSet<string> NoiseFolders=new(
        StringComparer.OrdinalIgnoreCase){
        "AppData","ProgramData","Program Files","Program Files (x86)",
        "Windows","Windows.old","$RECYCLE.BIN","System Volume Information",
        "$Windows.~BT","Recovery","PerfLogs",
        ".git",".svn",".hg",".vs",".idea",".vscode",
        ".cache","cache","caches","cached","cache2","code cache",
        "gpucache","shadercache","dawncache","inetcache","webcache",
        "temporary internet files","thumbnails","__pycache__",
        ".pytest_cache",".mypy_cache",".ruff_cache",".gradle",
        "node_modules","bower_components","bin","obj",
        "temp","tmp","crashdumps"
    };
    private static readonly HashSet<string> NoiseFileNames=new(
        StringComparer.OrdinalIgnoreCase){
        "desktop.ini","thumbs.db","ehthumbs.db",".ds_store",
        "iconcache.db","cache.db","pagefile.sys","swapfile.sys",
        "hiberfil.sys","ntuser.dat","ntuser.dat.log1",
        "ntuser.dat.log2","ntuser.ini"
    };
    private static readonly string[] NoiseSuffixes={
        ".tmp",".temp",".part",".partial",".crdownload",".download",
        ".cache",".pyc",".pyo",".swp",".swo",".lock",".lck",
        "-wal","-shm",".db-journal"
    };
    private const FileAttributes ExcludedAttributes=
        FileAttributes.Hidden|FileAttributes.System|FileAttributes.Temporary|
        FileAttributes.ReparsePoint;

    private static bool Inside(string path,string parent)
    {
        var p=parent.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        return path.Equals(p,StringComparison.OrdinalIgnoreCase)||
            path.StartsWith(p+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }

    private static bool InSystemRoot(string full)
    {
        foreach(var folder in new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath()
        })
            if(!string.IsNullOrWhiteSpace(folder)&&Inside(full,Path.GetFullPath(folder)))
                return true;
        return false;
    }

    private static string[] Components(string path) =>
        path.Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},
            StringSplitOptions.RemoveEmptyEntries);

    private static bool NoiseFile(string name)
    {
        if(string.IsNullOrEmpty(name) || name.StartsWith(".",StringComparison.Ordinal) ||
           name.StartsWith("~$",StringComparison.Ordinal) ||
           name.StartsWith("~",StringComparison.Ordinal))
            return true;
        if(NoiseFileNames.Contains(name) ||
           name.StartsWith("thumbcache_",StringComparison.OrdinalIgnoreCase) ||
           name.StartsWith("iconcache_",StringComparison.OrdinalIgnoreCase))
            return true;
        return NoiseSuffixes.Any(e=>name.EndsWith(e,StringComparison.OrdinalIgnoreCase));
    }

    // Filename/segment screening is intentionally format-agnostic: user
    // PDFs, spreadsheets, photos, videos, ZIPs, .json, .log, .bin and
    // extensionless files can all pass.
    public static bool Candidate(string path,string? explicitTestRoot=null,
        bool directory=false)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(path))return false;
            string full=Path.GetFullPath(path);
            string relative=full;
            if(explicitTestRoot!=null)
            {
                var root=Path.GetFullPath(explicitTestRoot);
                if(!Inside(full,root))return false;
                relative=Path.GetRelativePath(root,full);
            }
            else if(InSystemRoot(full))return false;
            var parts=Components(relative);
            if(parts.Length==0)return false;
            // Only skip the volume at the head, never a user's final file.
            for(int i=0;i<parts.Length-(directory?0:1);i++)
                if(NoiseFolders.Contains(parts[i]) ||
                   parts[i].StartsWith(".",StringComparison.Ordinal))
                    return false;
            var name=parts[^1];
            return directory
                ? !(NoiseFolders.Contains(name) ||
                    name.StartsWith(".",StringComparison.Ordinal))
                : !NoiseFile(name);
        }
        catch(Exception ex) when(ex is ArgumentException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {return false;}
    }

    public static bool VisibleAttribute(FileAttributes value)=>
        (value&ExcludedAttributes)==0;

    public static bool Eligible(string path,string? explicitTestRoot=null,
        bool allowDirectory=false)
    {
        try
        {
            bool directory=Directory.Exists(path);
            if(!directory&&!File.Exists(path))return false;
            if(directory&&!allowDirectory)return false;
            if(!Candidate(path,explicitTestRoot,directory))return false;
            var full=Path.GetFullPath(path);
            var root=explicitTestRoot==null?Path.GetPathRoot(full)!:
                Path.GetFullPath(explicitTestRoot);
            // A normal-looking file inside a Hidden/Temporary directory
            // is still hidden from the automatic shelf and Search.
            string? node=full;
            while(node!=null && Inside(node,root))
            {
                if(!VisibleAttribute(File.GetAttributes(node)))return false;
                if(node.Equals(root,StringComparison.OrdinalIgnoreCase))break;
                node=Path.GetDirectoryName(node);
            }
            return true;
        }
        catch(Exception ex) when(ex is ArgumentException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {return false;}
    }

    // No ancestor stat for drive crawling: the crawler itself MUST prune
    // excluded parents before traversing their children.
    public static bool IncludeInCrawl(string path,FileAttributes attributes)
    {
        bool dir=(attributes&FileAttributes.Directory)!=0;
        return VisibleAttribute(attributes)&&Candidate(path,null,dir);
    }
}
