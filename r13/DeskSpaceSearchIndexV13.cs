using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // App shortcuts are available immediately, before the full disk scan.
        try
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
                        Category="Applications",Name=name,OpenTarget=file,Location="Installed app / Start menu"});
                }
        }
        catch(Exception) { /* one inaccessible shortcut must not stop search */ }
        _=Task.Run(BuildIndex);
    }
    private static void BuildIndex()
    {
        try
        {
            var roots=new List<string>();
            foreach(var drive in DriveInfo.GetDrives())
            {
                try {if(drive.IsReady)roots.Add(drive.RootDirectory.FullName);}
                catch(Exception) { /* disconnected or protected drive */ }
            }
            // Enumerate mounted fixed, removable and reachable network volumes.
            // No global file-count cap; inaccessible areas are skipped safely.
            var batch=new List<Entry>(256);
            foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
                foreach(var path in EnumerateTree(root))
                {
                    bool directory;
                    try {directory=Directory.Exists(path);}
                    catch {continue;}
                    batch.Add(new Entry(Path.GetFileName(path),path,directory));
                    if(batch.Count>=256)
                    {
                        lock(Gate){Entries.AddRange(batch);}
                        batch.Clear();
                    }
                }
            if(batch.Count>0)lock(Gate){Entries.AddRange(batch);}
        }
        catch(Exception) { /* retain all successfully indexed paths */ }
        finally {completed=true;}
    }
    public static List<SearchHit> Search(string term,Workspace workspace,CancellationToken cancellation)
    {
        Start();
        var results=new List<SearchHit>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(SearchHit hit)
        {
            if(results.Count<150 && seen.Add(hit.OpenTarget ?? ""))results.Add(hit);
        }
        List<SearchHit> appSnapshot;
        lock(Gate)appSnapshot=Applications.ToList();
        foreach(var app in appSnapshot)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!Matches(app.Name,term))continue;
            if(!term.Contains("uninstall",StringComparison.OrdinalIgnoreCase) &&
                app.Name.StartsWith("Uninstall",StringComparison.OrdinalIgnoreCase))continue;
            Add(app);
        }
        foreach(var (name,uri) in Settings)
            if(Matches(name,term))
                Add(new SearchHit{Category="Settings",Name=name,OpenTarget=uri,Location="Windows Settings"});
        foreach(var note in workspace.Notes)
        {
            cancellation.ThrowIfCancellationRequested();
            if(Matches(note.Title,term)||Matches(note.Content,term))
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
                if(!Matches(item.Name,term))continue;
                if(item.Name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase) ||
                   item.Name.EndsWith(".url",StringComparison.OrdinalIgnoreCase))continue;
                var volume=Path.GetPathRoot(item.Path)??"";
                perVolume.TryGetValue(volume,out var already);
                if(already>=32)continue;
                var before=results.Count;
                Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path});
                if(results.Count>before)perVolume[volume]=already+1;
            }
        }
        return results.OrderBy(h=>h.Category=="Applications"?0:
             h.Category=="Settings"?1:h.Category=="Notes"?2:3)
            .ThenBy(h=>h.Name.StartsWith(term,StringComparison.OrdinalIgnoreCase)?0:1)
            .ThenBy(h=>h.Name,StringComparer.OrdinalIgnoreCase)
            .Take(90).ToList();
    }
}
