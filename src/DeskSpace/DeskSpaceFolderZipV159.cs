using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DeskSpace;

// ZIP generation is isolated from the Deck. The original folder is read-only.
// Keep the containing folder and empty subfolders in the archive; never follow
// symlinks/junctions. Write to a .partial file in the staging directory so an
// interrupted operation can never appear as a finished held-file item.
internal static class DeskSpaceFolderZipV159
{
    internal static string FolderName(string source)
    {
        var trimmed=source.TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name=Path.GetFileName(trimmed).TrimEnd(':');
        return string.IsNullOrWhiteSpace(name)?"Folder":name;
    }

    internal static string ZipName(string source)=>FolderName(source)+".zip";

    internal static void CreateFromFolder(string source,string destination)
    {
        source=Path.GetFullPath(source);
        if(!Directory.Exists(source))
            throw new DirectoryNotFoundException("Folder is no longer available: "+source);
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Cannot ZIP a symbolic link or junction folder.");

        var parent=Path.GetDirectoryName(destination)
            ??throw new IOException("ZIP destination has no parent directory.");
        Directory.CreateDirectory(parent);
        var partial=destination+".partial";
        if(File.Exists(destination)||File.Exists(partial))
            throw new IOException("A staged ZIP with this name already exists.");

        try
        {
            using(var target=new FileStream(partial,FileMode.CreateNew,
                FileAccess.Write,FileShare.None))
            using(var archive=new ZipArchive(target,ZipArchiveMode.Create,
                leaveOpen:false))
            {
                var root=FolderName(source)+"/";
                var pending=new Stack<(string Directory,string ZipPrefix)>();
                pending.Push((source,root));
                while(pending.Count>0)
                {
                    var (directory,prefix)=pending.Pop();
                    // Create explicit directory entries so empty folders survive.
                    archive.CreateEntry(prefix,CompressionLevel.NoCompression);
                    foreach(var file in Directory.EnumerateFiles(directory))
                    {
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        archive.CreateEntryFromFile(file,
                            prefix+Path.GetFileName(file),CompressionLevel.Optimal);
                    }
                    foreach(var child in Directory.EnumerateDirectories(directory))
                    {
                        if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        pending.Push((child,prefix+Path.GetFileName(child)+"/"));
                    }
                }
            }
            // Read back the written central directory before offering it to
            // Windows clipboard/Deck; no partially written file can be staged.
            using(var verified=ZipFile.OpenRead(partial))
            {
                if(verified.Entries.Count==0 ||
                   !string.Equals(verified.Entries[0].FullName,
                        FolderName(source)+"/",StringComparison.Ordinal))
                    throw new InvalidDataException("The staged ZIP failed validation.");
            }
            File.Move(partial,destination);
        }
        catch
        {
            try{if(File.Exists(partial))File.Delete(partial);}
            catch(IOException){}catch(UnauthorizedAccessException){}
            throw;
        }
    }
}
