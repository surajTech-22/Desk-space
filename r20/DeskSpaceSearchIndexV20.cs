using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// One process-wide, progressive index. It crawls every ready mounted drive,
// instead of scanning just Documents/Desktop/Downloads for every keystroke.
// Only names/paths are indexed; no file content or credential vault is read.
internal static class DeskSpaceSearchIndexV13
{
    private sealed record Entry(string Name, string Path, bool Folder);
    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = new();
    private static readonly ConcurrentDictionary<string,long> VolumeCounts=new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,bool> VolumeComplete=new(StringComparer.OrdinalIgnoreCase);
    public static string Coverage
    {
        get
        {
            var volumes=VolumeCounts.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)
                .Select(v=>v.Key.TrimEnd('\\')+" "+v.Value.ToString("N0")+
                   (VolumeComplete.TryGetValue(v.Key,out var ready)&&ready?" ✓":" …"));
            return string.Join(" · ",volumes);
        }
    }
    private static readonly List<SearchHit> Applications = new();
    private static bool started;
    private static volatile bool completed;
    public static bool IsIndexing => started && !completed;
    private static readonly (string Name,string Uri)[] Settings =
    {
        ("Display", "ms-settings:display"),
        ("Graphics settings", "ms-settings:display-advancedgraphics"),
        ("Night light", "ms-settings:nightlight"),
        ("Bluetooth and devices", "ms-settings:bluetooth"),
        ("Printers and scanners", "ms-settings:printers"),
        ("Mouse", "ms-settings:mousetouchpad"),
        ("Touchpad", "ms-settings:devices-touchpad"),
        ("Keyboard", "ms-settings:typing"),
        ("Sound", "ms-settings:sound"),
        ("Volume mixer", "ms-settings:apps-volume"),
        ("Microphone", "ms-settings:privacy-microphone"),
        ("Camera", "ms-settings:privacy-webcam"),
        ("Notifications", "ms-settings:notifications"),
        ("Focus", "ms-settings:quietmomentshome"),
        ("Power and battery", "ms-settings:powersleep"),
        ("Storage", "ms-settings:storagesense"),
        ("Storage sense", "ms-settings:storagepolicies"),
        ("Apps and installed apps", "ms-settings:appsfeatures"),
        ("Default apps", "ms-settings:defaultapps"),
        ("Startup apps", "ms-settings:startupapps"),
        ("Optional features", "ms-settings:optionalfeatures"),
        ("Accounts", "ms-settings:accounts"),
        ("Sign-in options", "ms-settings:signinoptions"),
        ("Windows Hello", "ms-settings:signinoptions"),
        ("Email and accounts", "ms-settings:emailandaccounts"),
        ("Your info", "ms-settings:yourinfo"),
        ("Time and language", "ms-settings:dateandtime"),
        ("Date and time", "ms-settings:dateandtime"),
        ("Language and region", "ms-settings:regionlanguage"),
        ("Accessibility", "ms-settings:easeofaccess"),
        ("Magnifier", "ms-settings:easeofaccess-magnifier"),
        ("Privacy and security", "ms-settings:privacy"),
        ("Windows security", "ms-settings:windowsdefender"),
        ("Windows update", "ms-settings:windowsupdate"),
        ("Advanced Windows update", "ms-settings:windowsupdate-options"),
        ("Network and internet", "ms-settings:network"),
        ("Wi-Fi", "ms-settings:network-wifi"),
        ("Ethernet", "ms-settings:network-ethernet"),
        ("VPN", "ms-settings:network-vpn"),
        ("Proxy", "ms-settings:network-proxy"),
        ("Mobile hotspot", "ms-settings:network-mobilehotspot"),
        ("Taskbar", "ms-settings:taskbar"),
        ("Personalization", "ms-settings:personalization"),
        ("Themes", "ms-settings:themes"),
        ("Background", "ms-settings:personalization-background"),
        ("Lock screen", "ms-settings:lockscreen"),
        ("Recovery", "ms-settings:recovery"),
        ("System information", "ms-settings:about"),
        ("Control Panel", "control.exe"),
        ("Task Manager", "taskmgr.exe"),
        ("Device Manager", "devmgmt.msc"),
        ("Services", "services.msc"),
        ("Registry Editor", "regedit.exe")
    };
    private static bool Matches(string source,string search)
        => source.Contains(search,StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<string> MenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }
    private static IEnumerable<string> EnumerateTree(string root)
    {
        if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))yield break;
        var stack=new Stack<string>();stack.Push(root);
        while(stack.Count>0)
        {
            var directory=stack.Pop();
            string[] nodes;
            try { nodes=Directory.GetFileSystemEntries(directory); }
            catch(Exception e) when(e is UnauthorizedAccessException or IOException or System.Security.SecurityException or ArgumentException)
            { continue; }
            foreach(var path in nodes)
            {
                FileAttributes flags;
                try { flags=File.GetAttributes(path); }
                catch(Exception e) when(e is UnauthorizedAccessException or IOException or System.Security.SecurityException or ArgumentException)
                { continue; }
                if((flags&FileAttributes.ReparsePoint)!=0)continue;
                if((flags&FileAttributes.Directory)!=0)stack.Push(path);
                yield return path;
            }
        }
    }
    private static void Start()
    {
        lock(Gate)
        {
            if(started)return;
            started=true;
        }
        // Do not block typing/UI on Start-menu enumeration or a large C: drive.
        // App discovery and drive indexing must start independently, otherwise
        // a slow Start Menu delays the entire system crawl.
        _=Task.Run(()=>{try{BuildApplications();}catch{}});
        _=Task.Run(BuildIndex);
    }
    private static void BuildApplications()
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in MenuRoots())
            foreach(var file in EnumerateTree(root))
            {
                if(!file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase) &&
                   !file.EndsWith(".url",StringComparison.OrdinalIgnoreCase))continue;
                if(!seen.Add(file))continue;
                var name=Path.GetFileNameWithoutExtension(file);
                lock(Gate)Applications.Add(new SearchHit{
                    Category="Applications",Name=name,OpenTarget=file,Location="Windows Start menu"});
            }
        // Installed conventional desktop applications can be absent from Start.
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
            foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
            {
                try{
                    using var root=RegistryKey.OpenBaseKey(hive,view);
                    using var paths=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                    if(paths==null)continue;
                    foreach(var name in paths.GetSubKeyNames())
                    {
                        try{
                            using var app=paths.OpenSubKey(name);
                            var path=app?.GetValue(null) as string;
                            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))continue;
                            var title=Path.GetFileNameWithoutExtension(name);
                            lock(Gate)Applications.Add(new SearchHit{
                                Category="Applications",Name=title,OpenTarget=path,
                                Location="Installed desktop application"});
                        }catch(Exception){}
                    }
                }catch(Exception){}
            }
    }
    private static void ScanVolume(string root)
    {
        var batch=new List<Entry>(256);
        VolumeCounts[root]=0;
        VolumeComplete[root]=false;
        try{
            foreach(var path in EnumerateTree(root))
            {
                bool folder;
                try{folder=Directory.Exists(path);}
                catch{continue;}
                batch.Add(new Entry(Path.GetFileName(path),path,folder));
                if(batch.Count<256)continue;
                lock(Gate)Entries.AddRange(batch);
                VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
                batch.Clear();
            }
        }catch(Exception){}
        if(batch.Count>0)
        {
            lock(Gate)Entries.AddRange(batch);
            VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
        }
        VolumeComplete[root]=true;
    }
    private static void BuildIndex()
    {
        try{
            var roots=new List<string>();
            foreach(var drive in DriveInfo.GetDrives())
            {
                try{if(drive.IsReady)roots.Add(drive.RootDirectory.FullName);}
                catch{}
            }
            // Different drives are indexed in parallel; a deep C: tree no
            // longer delays discovering entries on D:, E: or mapped shares.
            Task.WaitAll(roots.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root=>Task.Run(()=>ScanVolume(root))).ToArray());
        }catch(Exception){}
        finally{completed=true;}
    }
    public static List<SearchHit> Search(string term,Workspace workspace,CancellationToken cancellation)
    {
        Start();
        // Drive letters and This PC are first-class searchable locations.
        // Every comparison is explicitly OrdinalIgnoreCase, including C:\\ vs c:\\.
        var query=term.Trim();
        var driveRoots=DriveInfo.GetDrives()
            .Where(d=>{try{return d.IsReady;}catch{return false;}})
            .Select(d=>d.RootDirectory.FullName)
            .ToList();
        bool showVolumes=query.Equals("This PC",StringComparison.OrdinalIgnoreCase)
            ||query.Equals("drives",StringComparison.OrdinalIgnoreCase)
            ||query.Equals("all drives",StringComparison.OrdinalIgnoreCase);
        string? driveFilter=null;
        string fileQuery=query;
        if(query.StartsWith("drive:",StringComparison.OrdinalIgnoreCase) &&
           query.Length>=7 && char.IsLetter(query[6]))
        {
            driveFilter=char.ToUpperInvariant(query[6])+":\\";
            fileQuery=query.Substring(7).TrimStart(':','\\','/',' ');
        }
        else if(query.Length>=2 && char.IsLetter(query[0]) && query[1]==':')
        {
            driveFilter=char.ToUpperInvariant(query[0])+":\\";
            fileQuery=query.Substring(2).TrimStart('\\','/',' ');
        }
        if(driveFilter!=null && fileQuery.Length==0)showVolumes=true;
        var results=new List<SearchHit>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(SearchHit hit)
        {
            if(results.Count<400 && seen.Add(hit.OpenTarget ?? ""))results.Add(hit);
        }
        if(showVolumes)
        {
            foreach(var root in driveRoots)
            {
                if(driveFilter!=null && !root.Equals(driveFilter,StringComparison.OrdinalIgnoreCase))continue;
                Add(new SearchHit{Category="Files",Name=root,
                    OpenTarget=root,Location="Ready drive · "+(VolumeCounts.TryGetValue(root,out var n)?
                    n.ToString("N0")+" indexed names":"waiting for scan")});
            }
            return results;
        }
        bool scoped=driveFilter!=null;
        List<SearchHit> appSnapshot;
        lock(Gate)appSnapshot=Applications.ToList();
        // Query Windows' own index first. This is immediate on indexed disks,
        // while the slower full-drive fallback continues discovering files.
        // "content:term" searches indexed document contents, not just filenames.
        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);
        foreach(var hit in indexed)
            if(!scoped || hit.OpenTarget.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))
                Add(hit);
        // Three separate shortcuts to Winman CA ERP should not consume three
        // result rows. Search is about applications, not shortcut duplicates.
        foreach(var app in appSnapshot
            .Where(a=>!scoped && Matches(a.Name,fileQuery))
            .Where(a=>term.Contains("uninstall",StringComparison.OrdinalIgnoreCase)
                || !a.Name.StartsWith("Uninstall",StringComparison.OrdinalIgnoreCase))
            .OrderBy(a=>a.OpenTarget.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)?0:1)
            .GroupBy(a=>a.Name.Trim(),StringComparer.OrdinalIgnoreCase)
            .Select(g=>g.First()))
        {
            cancellation.ThrowIfCancellationRequested();
            Add(app);
        }
        foreach(var (name,uri) in Settings)
            if(!scoped && Matches(name,fileQuery))
                Add(new SearchHit{Category="Settings",Name=name,OpenTarget=uri,Location="Windows Settings"});
        foreach(var note in workspace.Notes)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!scoped && (Matches(note.Title,fileQuery)||Matches(note.Content,fileQuery)))
                Add(new SearchHit{Category="Notes",Name=note.Title,
                    OpenTarget="note:"+note.Id,NoteText=note.Content,Location="DeskSpace notes"});
        }
        lock(Gate)
        {
            var scanned=0;
            // Protect cross-volume coverage: a crowded C: result set must not
            // prevent matches from D:, E:, removable or network volumes.
            var perVolume=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in Entries)
            {
                if((++scanned&4095)==0)cancellation.ThrowIfCancellationRequested();
                if(fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))continue;
                if(scoped&&!item.Path.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))continue;
                if(!Matches(item.Name,fileQuery) && !Matches(item.Path,fileQuery))continue;
                if(item.Name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase) ||
                   item.Name.EndsWith(".url",StringComparison.OrdinalIgnoreCase))continue;
                var volume=Path.GetPathRoot(item.Path)??"";
                perVolume.TryGetValue(volume,out var already);
                if(already>=48)continue;
                var before=results.Count;
                Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path});
                if(results.Count>before)perVolume[volume]=already+1;
            }
        }
        return results.OrderBy(h=>h.Category=="Applications"?0:
             h.Category=="Settings"?1:h.Category=="Notes"?2:3)
            .ThenBy(h=>h.Name.Equals(fileQuery,StringComparison.OrdinalIgnoreCase)?0:
                h.Name.StartsWith(fileQuery,StringComparison.OrdinalIgnoreCase)?1:2)
            .ThenBy(h=>h.Name,StringComparer.OrdinalIgnoreCase)
            .Take(160).ToList();
    }
}
