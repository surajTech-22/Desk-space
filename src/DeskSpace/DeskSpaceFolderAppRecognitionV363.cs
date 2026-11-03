using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace DeskSpace;

// R363: Folder identity is inferred conservatively from TOP-LEVEL filenames.
// There is no universal "application belonging to a folder" in Windows.
// Never read document contents, descend into a tree, execute a file found
// inside the dragged folder, or launch anything merely because of a drag.
internal static class DeskSpaceFolderAppRecognitionV363
{
    internal sealed record Suggestion(
        string Folder, string Target, string AppId, string AppName,
        string FolderKind, string Evidence);

    private static readonly HashSet<string> DocumentExtensions=
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",".xls",".xlsm",".docx",".doc",".pptx",".ppt",
            ".pdf",".psd",".blend",".dwg",".odt",".ods",".odp"
        };

    // No filesystem recursion; the 256-entry limit bounds drag processing
    // even on a folder containing millions of files.
    internal static IReadOnlyList<Suggestion> Recognize(string folder)
    {
        if(string.IsNullOrWhiteSpace(folder))return Array.Empty<Suggestion>();
        try
        {
            var full=Path.GetFullPath(folder);
            if(!Directory.Exists(full) ||
               (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)
                return Array.Empty<Suggestion>();

            var files=new List<string>();
            var dirs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in Directory.EnumerateFileSystemEntries(full)
                .Take(257))
            {
                if(files.Count+dirs.Count>=256)break;
                FileAttributes attributes;
                try{attributes=File.GetAttributes(entry);}
                catch(IOException){continue;}
                catch(UnauthorizedAccessException){continue;}
                if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                var name=Path.GetFileName(entry);
                if((attributes&FileAttributes.Directory)!=0)
                    dirs.Add(name);
                else files.Add(name);
            }
            bool Has(string name)=>files.Contains(name,StringComparer.OrdinalIgnoreCase);
            bool Dir(string name)=>dirs.Contains(name);
            string? FileMatching(params string[] extensions)=>
                files.Where(x=>extensions.Any(ext=>x.EndsWith(ext,
                    StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)
                     .FirstOrDefault();
            var result=new List<Suggestion>();
            void Add(string app,string name,string kind,string evidence,string target)=>
                result.Add(new(full,target,app,name,kind,evidence));

            // Tally and Winman data folders take precedence over generic
            // documents. These are live databases, not importable files.
            var vendor=DeskSpaceAccountingFileRecognitionV364
                .RecognizeVendorFolder(full,files);
            var solution=FileMatching(".sln",".slnx");
            if(vendor.Count>0)
            {
                result.AddRange(vendor);
            }
            else if(solution!=null)
            {
                Add("VisualStudio","Visual Studio","Visual Studio solution",
                    solution,Path.Combine(full,solution));
                Add("Code","Visual Studio Code","Visual Studio solution",
                    solution,full);
            }
            else if((Has("settings.gradle")||Has("settings.gradle.kts"))&&
                    (Has("gradlew")||Has("gradlew.bat")||
                     Has("build.gradle")||Has("build.gradle.kts")))
            {
                Add("AndroidStudio","Android Studio","Android/Gradle project",
                    "settings.gradle",full);
                Add("Code","Visual Studio Code","Android/Gradle project",
                    "settings.gradle",full);
            }
            else if(FileMatching(".Rproj") is string rproject)
            {
                Add("RStudio","RStudio","R project",rproject,
                    Path.Combine(full,rproject));
                Add("Code","Visual Studio Code","R project",rproject,full);
            }
            else if(FileMatching(".code-workspace") is string workspace)
            {
                Add("Code","Visual Studio Code","VS Code workspace",workspace,
                    Path.Combine(full,workspace));
            }
            else if(Has("pyproject.toml")||Has("requirements.txt")||
                    Has("Pipfile")||Has("setup.py"))
            {
                if(Dir(".idea"))
                    Add("PyCharm","PyCharm","Python project",
                        "Python config + .idea",full);
                Add("Code","Visual Studio Code","Python project",
                    "Python project manifest",full);
            }
            else if(Has("package.json")||Has("tsconfig.json")||Dir(".vscode"))
            {
                Add("Code","Visual Studio Code","Code project",
                    Has("package.json")?"package.json":
                    Has("tsconfig.json")?"tsconfig.json":".vscode",full);
            }
            else if(FileMatching(".csproj",".fsproj",".vbproj") is string project)
            {
                Add("VisualStudio","Visual Studio",".NET project",project,
                    Path.Combine(full,project));
                Add("Code","Visual Studio Code",".NET project",project,full);
            }
            else
            {
                // A single recognized document has a meaningful target. For
                // multiple documents we do NOT guess which one to open.
                var docs=files.Where(x=>DocumentExtensions.Contains(
                    Path.GetExtension(x))).ToArray();
                if(docs.Length==1 && files.Count==1)
                {
                    var file=docs[0];
                    var ext=Path.GetExtension(file).ToLowerInvariant();
                    Add("Default","Windows default application",
                        "Document "+ext,file,Path.Combine(full,file));
                }
                else if(files.Count==1 &&
                    DeskSpaceAccountingFileRecognitionV364.SafeAssociation(files[0]))
                {
                    var file=files[0];
                    Add("Default","Windows default application",
                        "Registered file type "+Path.GetExtension(file),
                        file,Path.Combine(full,file));
                }
            }
            // Explorer is always a safe fallback. It opens the FOLDER,
            // never a copied source, script or arbitrary executable.
            Add("Explorer","File Explorer",
                result.Count>0?result[0].FolderKind:"Ordinary folder",
                result.Count>0?result[0].Evidence:"No specific app signature",
                full);
            return result;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Array.Empty<Suggestion>();
        }
    }

    // A file drop is classified independently of folders. We never infer
    // Tally or Winman from a generic XML/JSON (which may require importing
    // through the vendor application, not double-clicking).
    internal static IReadOnlyList<Suggestion> RecognizePath(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return Array.Empty<Suggestion>();
        try
        {
            var full=Path.GetFullPath(path);
            if(Directory.Exists(full))return Recognize(full);
            if(!File.Exists(full) ||
               (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)
                return Array.Empty<Suggestion>();
            var folder=Path.GetDirectoryName(full);
            if(folder==null)return Array.Empty<Suggestion>();
            var results=new List<Suggestion>();
            results.AddRange(DeskSpaceAccountingFileRecognitionV364
                .RecognizeVendorFile(full));
            if(results.Count==0 &&
                DeskSpaceAccountingFileRecognitionV364.SafeAssociation(full))
                results.Add(new(folder,full,"Default",
                    "Windows default application",
                    "File "+Path.GetExtension(full),
                    Path.GetFileName(full)));
            results.Add(new(folder,folder,"Explorer","File Explorer",
                results.Count>0?results[0].FolderKind:"Unrecognized file",
                Path.GetFileName(full)));
            return results;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Array.Empty<Suggestion>();
        }
    }

    private static string? ExactExecutable(string path,string expected)
    {
        try
        {
            if(Path.GetFileName(path).Equals(expected,StringComparison.OrdinalIgnoreCase)
               && File.Exists(path) &&
               (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)
                return Path.GetFullPath(path);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException){}
        return null;
    }

    private static string? FromAppPaths(string name)
    {
        // Only signed-in Windows app registrations for a KNOWN executable
        // filename. Never resolve through PATH, the dropped folder or cwd.
        const string key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})
        {
            try
            {
                using var entry=hive.OpenSubKey(key+name,false);
                if(entry?.GetValue(null) is string path &&
                   ExactExecutable(path.Trim('"'),name) is string exe)
                    return exe;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException){}
        }
        return null;
    }

    private static IEnumerable<string> KnownLocations(string appId)
    {
        var program=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var x86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        switch(appId)
        {
            case "Code":
                yield return Path.Combine(local,"Programs","Microsoft VS Code","Code.exe");
                yield return Path.Combine(program,"Microsoft VS Code","Code.exe");
                yield return Path.Combine(x86,"Microsoft VS Code","Code.exe");
                break;
            case "VisualStudio":
                foreach(var parent in new[]{program,x86})
                foreach(var year in new[]{"2026","2022","2019"})
                foreach(var edition in new[]{"Enterprise","Professional","Community"})
                    yield return Path.Combine(parent,"Microsoft Visual Studio",year,
                        edition,"Common7","IDE","devenv.exe");
                break;
            case "AndroidStudio":
                yield return Path.Combine(program,"Android","Android Studio","bin","studio64.exe");
                yield return Path.Combine(local,"Programs","Android Studio","bin","studio64.exe");
                break;
            case "PyCharm":
                yield return Path.Combine(program,"JetBrains","PyCharm","bin","pycharm64.exe");
                yield return Path.Combine(local,"Programs","PyCharm","bin","pycharm64.exe");
                break;
            case "RStudio":
                yield return Path.Combine(program,"RStudio","rstudio.exe");
                yield return Path.Combine(program,"RStudio","bin","rstudio.exe");
                break;
        }
    }

    internal static string? FindInstalledApp(string appId)
    {
        if(DeskSpaceAccountingFileRecognitionV364.IsVendor(appId))
            return DeskSpaceAccountingFileRecognitionV364.FindInstalledVendor(appId);
        string? name=appId switch
        {
            "Code"=>"Code.exe",
            "VisualStudio"=>"devenv.exe",
            "AndroidStudio"=>"studio64.exe",
            "PyCharm"=>"pycharm64.exe",
            "RStudio"=>"rstudio.exe",
            _=>null
        };
        if(name==null)return null;
        if(FromAppPaths(name) is string registered)return registered;
        foreach(var path in KnownLocations(appId))
            if(ExactExecutable(path,name) is string installed)return installed;
        return null;
    }

    internal static bool IsAvailable(Suggestion item,
        Func<string,string?>? resolver=null)
    {
        if(item.AppId is "Explorer" or "Default")return true;
        return (resolver??FindInstalledApp)(item.AppId)!=null;
    }

    // This is invoked ONLY by an explicit button click in the existing Deck.
    // No files are rewritten, no executable from the dragged folder is run,
    // no arbitrary command line is accepted, and targets are revalidated.
    internal static bool TryOpen(Suggestion item,out string? error,
        Func<string,string?>? resolver=null,
        Func<ProcessStartInfo,Process?>? processLauncher=null)
    {
        error=null;
        try
        {
            var folder=Path.GetFullPath(item.Folder);
            var target=Path.GetFullPath(item.Target);
            if(!Directory.Exists(folder) ||
               (File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Folder is no longer available.");
            bool isFolder=string.Equals(folder,target,
                StringComparison.OrdinalIgnoreCase);
            if(!isFolder)
            {
                if(!target.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar)+
                    Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
                   !File.Exists(target) ||
                   (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("The recognized project file changed.");
            }
            var launcher=processLauncher??Process.Start;
            ProcessStartInfo info;
            if(item.AppId=="Explorer")
            {
                if(!isFolder)throw new IOException("Explorer requires a folder.");
                var system=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var explorer=ExactExecutable(Path.Combine(system,"explorer.exe"),
                    "explorer.exe");
                if(explorer==null)throw new IOException("Windows Explorer unavailable.");
                info=new ProcessStartInfo(explorer){UseShellExecute=false};
                info.ArgumentList.Add(folder);
            }
            else if(item.AppId=="Default")
            {
                if(isFolder ||
                   !DeskSpaceAccountingFileRecognitionV364.SafeAssociation(target))
                    throw new IOException("Unsafe or unsupported file association.");
                info=new ProcessStartInfo(target){UseShellExecute=true};
            }
            else if(DeskSpaceAccountingFileRecognitionV364.IsVendor(item.AppId))
            {
                // Verify both the original source and its vendor signature
                // again at click time; no stale or forged shell command.
                var signatures=isFolder?Recognize(folder):RecognizePath(target);
                if(!signatures.Any(x=>x.AppId==item.AppId))
                    throw new IOException("The vendor file signature changed.");
                if(!DeskSpaceAccountingFileRecognitionV364.TryBuildVendorStart(
                    item,resolver??FindInstalledApp,out info) || info==null)
                    throw new IOException("The required Tally or Winman application is not installed.");
            }
            else
            {
                var exe=(resolver??FindInstalledApp)(item.AppId);
                if(exe==null ||
                   ExactExecutable(exe,Path.GetFileName(exe))==null ||
                   !new[]{"Code","VisualStudio","AndroidStudio","PyCharm","RStudio"}
                      .Contains(item.AppId,StringComparer.Ordinal))
                    throw new IOException("The required application is not installed.");
                info=new ProcessStartInfo(exe){
                    UseShellExecute=false,WorkingDirectory=folder
                };
                info.ArgumentList.Add(target);
            }
            launcher(info);
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException or
            ArgumentException or NotSupportedException)
        {
            error=ex.Message;
            return false;
        }
    }
}
