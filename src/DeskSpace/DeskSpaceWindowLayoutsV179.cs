using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Rectangle=System.Drawing.Rectangle;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Opt-in layout snapshots. Only process names and SHA256 hashes of window
// titles are saved, never filenames, raw titles, or window contents.
// A restore moves only already-running ordinary windows, and never focuses
// them, launches applications, or alters the user's actual documents.
internal static class DeskSpaceWindowLayoutsV179
{
    internal sealed record WindowEntry(
        string Process,string TitleHash,int Instance,string Monitor,
        double X,double Y,double W,double H);
    internal sealed record Profile(string Name,List<WindowEntry> Windows);
    private static readonly Dictionary<Window,Window> Open=new();
    private static readonly string Storage=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","WindowLayouts","profiles-v1.json");
    private const uint SWP_NOZORDER=0x0004;
    private const uint SWP_NOACTIVATE=0x0010;
    private const uint SWP_NOOWNERZORDER=0x0200;
    private const uint SWP_ASYNCWINDOWPOS=0x4000; // avoid blocked foreign UI thread
    private delegate bool EnumWindowsCallback(IntPtr hwnd,IntPtr unused);
    [DllImport("user32.dll")] private static extern bool EnumWindows(
        EnumWindowsCallback callback,IntPtr unused);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(
        IntPtr hwnd,out uint id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd,StringBuilder buffer,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,
        int x,int y,int width,int height,uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left,Top,Right,Bottom;
    }
    private sealed record Candidate(IntPtr Handle,string Process,
        string Hash,string Monitor,Rectangle Bounds);

    internal static bool ValidName(string? name)=>name is {Length: >=1 and <=40} &&
        name.All(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_') &&
        name==name.Trim();
    private static string TitleHash(string title)
    {
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(title));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    internal static WindowEntry MakeEntry(string process,string hash,int instance,
        string monitor,Rectangle bounds,Rectangle work)
    {
        if(work.Width<=0||work.Height<=0)throw new ArgumentOutOfRangeException(nameof(work));
        return new WindowEntry(process,hash,instance,monitor,
            (double)(bounds.Left-work.Left)/work.Width,
            (double)(bounds.Top-work.Top)/work.Height,
            (double)bounds.Width/work.Width,
            (double)bounds.Height/work.Height);
    }
    internal static Rectangle RestoreBounds(WindowEntry entry,Rectangle work)
    {
        if(work.Width<240||work.Height<160)
            throw new ArgumentOutOfRangeException(nameof(work));
        if(!double.IsFinite(entry.X)||!double.IsFinite(entry.Y)||
            !double.IsFinite(entry.W)||!double.IsFinite(entry.H)||
            entry.W<=0||entry.H<=0)
            throw new ArgumentException("Invalid saved window coordinates.");
        int width=Math.Clamp((int)Math.Round(entry.W*work.Width),240,work.Width);
        int height=Math.Clamp((int)Math.Round(entry.H*work.Height),160,work.Height);
        int x=Math.Clamp(work.Left+(int)Math.Round(entry.X*work.Width),
            work.Left,work.Right-width);
        int y=Math.Clamp(work.Top+(int)Math.Round(entry.Y*work.Height),
            work.Top,work.Bottom-height);
        return new Rectangle(x,y,width,height);
    }
    private static List<Candidate> VisibleWindows()
    {
        var found=new List<Candidate>();
        int own=Environment.ProcessId;
        EnumWindowsCallback cb=(hwnd,_)=>{
            if(found.Count>=96)return false;
            if(!IsWindowVisible(hwnd)||IsIconic(hwnd))return true;
            int length=GetWindowTextLength(hwnd);
            if(length is <1 or >2048)return true;
            var title=new StringBuilder(length+1);
            if(GetWindowText(hwnd,title,title.Capacity)<1)return true;
            GetWindowThreadProcessId(hwnd,out uint pid);
            if(pid==0||pid==own)return true;
            if(!GetWindowRect(hwnd,out var rect))return true;
            var area=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
            if(area.Width<240||area.Height<160)return true;
            try
            {
                using var process=Process.GetProcessById((int)pid);
                var name=process.ProcessName;
                // Do not move the taskbar, Explorer desktop or OS UI.
                if(name.Equals("explorer",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("dwm",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("ApplicationFrameHost",StringComparison.OrdinalIgnoreCase))
                    return true;
                var monitor=Forms.Screen.FromHandle(hwnd);
                if(!monitor.WorkingArea.IntersectsWith(area))return true;
                found.Add(new Candidate(hwnd,name,TitleHash(title.ToString()),
                    monitor.DeviceName,area));
            }
            catch(Exception ex)when(ex is ArgumentException or
                InvalidOperationException or Win32Exception){}
            return true;
        };
        EnumWindows(cb,IntPtr.Zero);
        return found;
    }
    internal static Profile Capture(string name)
    {
        if(!ValidName(name))throw new ArgumentException(
            "Use 1-40 letters, numbers, spaces, hyphens or underscores.");
        var candidates=VisibleWindows();
        var counters=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var entries=new List<WindowEntry>();
        foreach(var c in candidates.Take(32))
        {
            var group=c.Process+"|"+c.Hash;
            counters.TryGetValue(group,out int n);
            counters[group]=n+1;
            var monitor=Forms.Screen.AllScreens.FirstOrDefault(s=>
                s.DeviceName==c.Monitor)??Forms.Screen.PrimaryScreen;
            if(monitor==null)continue;
            entries.Add(MakeEntry(c.Process,c.Hash,n,c.Monitor,
                c.Bounds,monitor.WorkingArea));
        }
        return new Profile(name,entries);
    }
    private static List<Profile> Load()
    {
        try
        {
            if(!File.Exists(Storage))return new();
            var file=new FileInfo(Storage);
            if(file.Length>1024*1024)return new();
            var result=JsonSerializer.Deserialize<List<Profile>>(
                File.ReadAllText(Storage))??new();
            return result.Where(ValidProfile).Take(12).ToList();
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException)
        {return new();}
    }
    private static void Save(Profile profile)
    {
        if(!ValidProfile(profile))
            throw new ArgumentException("Invalid window layout profile.");
        var profiles=Load();
        profiles.RemoveAll(p=>p.Name.Equals(profile.Name,
            StringComparison.OrdinalIgnoreCase));
        profiles.Add(profile);
        if(profiles.Count>12)profiles=profiles.TakeLast(12).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Storage)!);
        var temporary=Storage+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(profiles),
                new UTF8Encoding(false));
            File.Move(temporary,Storage,true);
        }
        finally
        {
            try{if(File.Exists(temporary))File.Delete(temporary);}
            catch(IOException){}
        }
    }
    internal static (int Restored,int Skipped) Restore(Profile profile)
    {
        if(!ValidProfile(profile))
            return (0,profile?.Windows?.Count ?? 0);
        var screens=Forms.Screen.AllScreens;
        var candidates=VisibleWindows();
        var consumed=new HashSet<IntPtr>();
        int restored=0,skipped=0;
        foreach(var saved in profile.Windows)
        {
            // Never arbitrarily select an unrelated window when the title
            // has changed; only restore exact anonymized title matches.
            var matches=candidates.Where(c=>
                c.Process.Equals(saved.Process,StringComparison.OrdinalIgnoreCase) &&
                c.Hash==saved.TitleHash).ToArray();
            if(saved.Instance<0 || saved.Instance>=matches.Length){skipped++;continue;}
            var c=matches[saved.Instance];
            if(consumed.Contains(c.Handle)){skipped++;continue;}
            consumed.Add(c.Handle);
            // Do not silently send a window to a different monitor if a
            // display was unplugged or its device identifier changed.
            var screen=screens.FirstOrDefault(s=>
                s.DeviceName.Equals(saved.Monitor,
                    StringComparison.OrdinalIgnoreCase));
            if(screen==null){skipped++;continue;}
            try
            {
                var rect=RestoreBounds(saved,screen.WorkingArea);
                if(SetWindowPos(c.Handle,IntPtr.Zero,rect.Left,rect.Top,
                    rect.Width,rect.Height,
                    SWP_NOZORDER|SWP_NOACTIVATE|SWP_NOOWNERZORDER|
                    SWP_ASYNCWINDOWPOS))
                    restored++;
                else skipped++;
            }
            catch(ArgumentException){skipped++;}
        }
        return (restored,skipped);
    }
    // Portable export is opt-in and intentionally excludes real window titles,
    // document paths and workspace content. Import is validated before any save.
    internal static string ExportProfile(Profile profile)
    {
        if(!ValidProfile(profile))
            throw new ArgumentException("Invalid layout profile.");
        return JsonSerializer.Serialize(profile,new JsonSerializerOptions{
            WriteIndented=true});
    }
    internal static Profile ImportProfile(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>131072)
            throw new ArgumentException("Invalid profile size.");
        Profile? profile;
        try{profile=JsonSerializer.Deserialize<Profile>(json);}
        catch(JsonException){throw new ArgumentException("Invalid profile JSON.");}
        if(profile==null||!ValidProfile(profile))
            throw new ArgumentException("Profile coordinates or labels are invalid.");
        return profile;
    }
    internal static bool ValidProfile(Profile? profile)
    {
        if(profile==null||!ValidName(profile.Name)||
           profile.Windows==null||profile.Windows.Count>32)return false;
        return profile.Windows.All(e=>
            e!=null &&
            e.Process is {Length: >=1 and <=128} &&
            e.Process.All(c=>char.IsLetterOrDigit(c)||c=='_'||
                c=='-'||c=='.'||c==' ') &&
            e.TitleHash is {Length: 64} &&
            e.TitleHash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            e.Instance is >=0 and <=31 &&
            e.Monitor is {Length: >=1 and <=128} &&
            e.Monitor.All(c=>!char.IsControl(c)) &&
            double.IsFinite(e.X)&&double.IsFinite(e.Y) &&
            double.IsFinite(e.W)&&double.IsFinite(e.H) &&
            e.X is >=-10 and <=10 && e.Y is >=-10 and <=10 &&
            e.W is >0 and <=10 && e.H is >0 and <=10);
    }
    internal static bool DeleteProfile(string name)
    {
        if(!ValidName(name))return false;
        var current=Load();
        if(current.RemoveAll(p=>p.Name.Equals(name,
            StringComparison.OrdinalIgnoreCase))==0)return false;
        Persist(current);
        return true;
    }
    private static void Persist(List<Profile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Storage)!);
        var tmp=Storage+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,JsonSerializer.Serialize(profiles),
                new UTF8Encoding(false));
            File.Move(tmp,Storage,true);
        }
        finally
        {
            try{if(File.Exists(tmp))File.Delete(tmp);}
            catch(IOException){}
        }
    }    // No windows are moved during preview. A missing or renamed monitor is
    // reported separately, never silently remapped to the primary display.
    internal sealed record RestorePreview(
        int Eligible,int MissingWindows,int MissingMonitors);
    internal static DeskSpaceWindowRestorePlanV257.Plan DetailedPreviewV257(Profile profile)
    {
        if(!ValidProfile(profile))
            return new(0,0,
                Array.Empty<DeskSpaceWindowRestorePlanV257.Row>());
        var live=VisibleWindows().Select(w=>
            new DeskSpaceWindowRestorePlanV257.Live(w.Process,w.Hash)).ToArray();
        var screens=Forms.Screen.AllScreens
            .GroupBy(screen=>screen.DeviceName,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.First().WorkingArea,
                StringComparer.OrdinalIgnoreCase);
        return DeskSpaceWindowRestorePlanV257.Build(profile.Windows,live,screens);
    }
    internal static RestorePreview Preview(Profile profile)
    {
        if(!ValidProfile(profile))
            return new(0,profile?.Windows?.Count ?? 0,0);
        var screens=Forms.Screen.AllScreens;
        var candidates=VisibleWindows();
        var consumed=new HashSet<IntPtr>();
        int eligible=0,missingWindows=0,missingMonitors=0;
        foreach(var saved in profile.Windows)
        {
            var matches=candidates.Where(c=>
                c.Process.Equals(saved.Process,StringComparison.OrdinalIgnoreCase) &&
                c.Hash==saved.TitleHash).ToArray();
            if(saved.Instance<0 || saved.Instance>=matches.Length ||
                !consumed.Add(matches[saved.Instance].Handle))
            {
                missingWindows++;
                continue;
            }
            if(!screens.Any(s=>s.DeviceName.Equals(saved.Monitor,
                StringComparison.OrdinalIgnoreCase)))
            {
                missingMonitors++;
                continue;
            }
            eligible++;
        }
        return new(eligible,missingWindows,missingMonitors);
    }    internal static Window Create(Window owner)
    {
        var title=new TextBlock{
            Text="Window layout profiles",FontSize=19,
            FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,
            Margin=new Thickness(0,0,0,8)};
        var hint=new TextBlock{
            Text="Capture and restore positions of visible application windows. "+
                "No documents are read, no apps are launched and no titles "+
                "are stored. Restores only matching running windows, "+
                "without activating them.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gainsboro,
            FontSize=11,Margin=new Thickness(0,0,0,14)};
        var name=new TextBox{
            Text="Default",MinHeight=28,Margin=new Thickness(0,0,0,8)};
        var pick=new ComboBox{MinHeight=28,Margin=new Thickness(0,0,0,8)};
        var status=new TextBlock{
            Text="No changes have been made.",
            Foreground=Brushes.Gainsboro,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,12,0,0)};
        var previewRowsV257=new StackPanel{Margin=new Thickness(0,9,0,0)};
        string? previewedV257=null;
        string? proofV257=null;
        string SignatureV257(Profile profile,
            DeskSpaceWindowRestorePlanV257.Plan plan)
        {
            string projection=JsonSerializer.Serialize(profile)+"|"+
                string.Join("|",plan.Rows.Select(row=>
                    row.Process+"|"+row.Monitor+"|"+row.Ready+"|"+
                    row.Bounds.X+","+row.Bounds.Y+","+
                    row.Bounds.Width+","+row.Bounds.Height+"|"+row.Reason));
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(projection)));
        }
        void Refresh()
        {
            pick.ItemsSource=Load().Select(p=>p.Name).ToArray();
            if(pick.Items.Count>0)pick.SelectedIndex=0;
        }
        var capture=new Button{Content="Capture current layout",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,6,0)};
        capture.Click+=(_,_)=>{
            try
            {
                var profile=Capture(name.Text);
                if(Load().Any(p=>p.Name.Equals(profile.Name,
                    StringComparison.OrdinalIgnoreCase)) &&
                   MessageBox.Show("Replace the existing profile "+profile.Name+"?",
                        "DeskSpace window layouts",MessageBoxButton.YesNo,
                        MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                if(profile.Windows.Count==0)
                {
                    status.Text="No eligible visible application windows.";
                    return;
                }
                Save(profile);Refresh();
                pick.SelectedItem=profile.Name;
                status.Text="Saved "+profile.Windows.Count+
                    " window positions. Existing documents not changed.";
            }
            catch(Exception ex)when(ex is ArgumentException or
                IOException or UnauthorizedAccessException)
            {status.Text="Capture failed ("+ex.GetType().Name+").";}
        };
        var restore=new Button{Content="Restore selected",
            Padding=new Thickness(9,6,9,6)};
        restore.Click+=(_,_)=>{
            // Review the exact saved profile and the current monitor/window
            // geometry before allowing the existing Yes/No confirmation.
            if(pick.SelectedItem is not string selectedV257 ||
                !string.Equals(previewedV257,selectedV257,
                    StringComparison.Ordinal) || proofV257==null)
            {
                status.Text="Preview the selected profile before restoring.";
                return;
            }
            var savedV257=Load().FirstOrDefault(p=>
                p.Name==selectedV257);
            if(savedV257==null)return;
            var latestV257=DetailedPreviewV257(savedV257);
            if(!string.Equals(proofV257,SignatureV257(savedV257,latestV257),
                StringComparison.Ordinal))
            {
                previewedV257=null;proofV257=null;
                status.Text="Windows or monitors changed. Preview again.";
                return;
            }
            if(latestV257.Ready==0)
            {
                status.Text="No matching windows available. Nothing moved.";
                return;
            }
            // Consent is single use, including if the confirmation is denied.
            previewedV257=null;proofV257=null;
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var before=Preview(profile);
            if(before.Eligible==0)
            {
                status.Text="Nothing to restore: "+before.MissingWindows+
                    " missing windows; "+before.MissingMonitors+" unavailable monitors.";
                return;
            }
            if(MessageBox.Show(
                "Request position restore for "+before.Eligible+
                " matching running windows? "+before.MissingWindows+
                " unmatched and "+before.MissingMonitors+
                " on disconnected monitors will be skipped. "+
                "Windows will not be activated.",
                "DeskSpace window layouts",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var (done,skipped)=Restore(profile);
            status.Text="Restore requested for "+done+
                " windows; skipped "+skipped+
                ". The operating system completes moves asynchronously.";
        };
        var export=new Button{Content="Export selected...",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        export.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var dialog=new Microsoft.Win32.SaveFileDialog{
                Title="Export non-sensitive window layout",
                FileName="DeskSpace-layout-"+selected+".json",
                Filter="JSON layout (*.json)|*.json",DefaultExt=".json",
                AddExtension=true,OverwritePrompt=true,
                InitialDirectory=Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),"Downloads")
            };
            if(dialog.ShowDialog()==true)
                try{
                    File.WriteAllText(dialog.FileName,ExportProfile(profile),
                        new UTF8Encoding(false));
                    status.Text="Exported positions and hashed titles only.";
                }
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
                {status.Text="Export failed ("+ex.GetType().Name+").";}
        };
        var import=new Button{Content="Import...",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        import.Click+=(_,_)=>{
            var dialog=new Microsoft.Win32.OpenFileDialog{
                Title="Import window layout",Filter="JSON layout (*.json)|*.json",
                CheckFileExists=true,Multiselect=false};
            if(dialog.ShowDialog()!=true)return;
            try
            {
                if(new FileInfo(dialog.FileName).Length>131072)
                    throw new ArgumentException("File too large.");
                var profile=ImportProfile(File.ReadAllText(dialog.FileName));
                if(Load().Any(p=>p.Name.Equals(profile.Name,
                    StringComparison.OrdinalIgnoreCase)) &&
                    MessageBox.Show("Replace the saved layout "+profile.Name+"?",
                        "DeskSpace layouts",MessageBoxButton.YesNo,
                        MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                Save(profile);Refresh();pick.SelectedItem=profile.Name;
                status.Text="Imported "+profile.Windows.Count+
                    " window positions; none were moved.";
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or ArgumentException)
            {status.Text="Import rejected ("+ex.GetType().Name+").";}
        };
        var remove=new Button{Content="Delete",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        remove.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            if(MessageBox.Show("Delete the saved layout "+selected+"?",
                "DeskSpace layouts",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            DeleteProfile(selected);Refresh();
            status.Text="Saved layout removed; application windows unchanged.";
        };
        var preview=new Button{Content="Preview restore",
            Padding=new Thickness(8,6,8,6),
            Margin=new Thickness(6,0,0,0)};
        preview.Click+=(_,_)=>{
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            var planV257=DetailedPreviewV257(profile);
            previewedV257=selected;
            proofV257=SignatureV257(profile,planV257);
            previewRowsV257.Children.Clear();
            foreach(var row in planV257.Rows)
            {
                string location=row.Ready?
                    "("+row.Bounds.X+","+row.Bounds.Y+") "+
                    row.Bounds.Width+" x "+row.Bounds.Height:
                    row.Reason;
                previewRowsV257.Children.Add(new TextBlock{
                    Text=row.Process+" · "+row.Monitor+" · "+location,
                    Foreground=row.Ready?Brushes.Gainsboro:Brushes.Orange,
                    TextWrapping=TextWrapping.Wrap,
                    Margin=new Thickness(0,0,0,5)});
            }
            status.Text="Preview only: "+planV257.Ready+" matching; "+
                planV257.Skipped+" unavailable. No windows moved.";
        };
        var buttons=new WrapPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(preview);
        buttons.Children.Add(restore);
        buttons.Children.Add(export);buttons.Children.Add(import);
        buttons.Children.Add(remove);
        var layout=new StackPanel{Margin=new Thickness(18)};
        layout.Children.Add(title);layout.Children.Add(hint);
        layout.Children.Add(new TextBlock{Text="New or existing profile name",
            Foreground=Brushes.White});
        layout.Children.Add(name);
        layout.Children.Add(new TextBlock{Text="Saved profiles",
            Foreground=Brushes.White});
        layout.Children.Add(pick);layout.Children.Add(buttons);
        layout.Children.Add(status);
        layout.Children.Add(previewRowsV257);
        var window=new Window{
            Title="DeskSpace · Window layouts",
            Width=600,Height=415,MinWidth=475,MinHeight=340,
            ShowInTaskbar=true,
            Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(24,33,60)),
            Content=new ScrollViewer{Content=layout,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto},
            WindowStartupLocation=WindowStartupLocation.CenterScreen};
        if(owner.IsVisible)
        {
            window.Owner=owner;
            window.WindowStartupLocation=WindowStartupLocation.CenterOwner;
        }
        Refresh();
        return window;
    }
    internal static void Show(Window owner)
    {
        if(Open.TryGetValue(owner,out var prior))
        {
            if(prior.IsLoaded){prior.Activate();return;}
            Open.Remove(owner);
        }
        var window=Create(owner);Open[owner]=window;
        window.Closed+=(_,_)=>Open.Remove(owner);
        window.Show();
    }
}
