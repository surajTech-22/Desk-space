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
            return result.Where(p=>p!=null&&ValidName(p.Name)&&
                p.Windows!=null&&p.Windows.Count<=32).Take(12).ToList();
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException)
        {return new();}
    }
    private static void Save(Profile profile)
    {
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
            var screen=screens.FirstOrDefault(s=>
                s.DeviceName==saved.Monitor)??Forms.Screen.PrimaryScreen;
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
    internal static Window Create(Window owner)
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
            if(pick.SelectedItem is not string selected)return;
            var profile=Load().FirstOrDefault(p=>p.Name==selected);
            if(profile==null)return;
            if(MessageBox.Show(
                "Restore positions of up to "+profile.Windows.Count+
                " already-running windows? Windows will not be activated.",
                "DeskSpace window layouts",MessageBoxButton.YesNo,
                MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var (done,skipped)=Restore(profile);
            status.Text="Restored "+done+" windows; skipped "+skipped+
                " unmatched or unavailable windows.";
        };
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(capture);buttons.Children.Add(restore);
        var layout=new StackPanel{Margin=new Thickness(18)};
        layout.Children.Add(title);layout.Children.Add(hint);
        layout.Children.Add(new TextBlock{Text="New or existing profile name",
            Foreground=Brushes.White});
        layout.Children.Add(name);
        layout.Children.Add(new TextBlock{Text="Saved profiles",
            Foreground=Brushes.White});
        layout.Children.Add(pick);layout.Children.Add(buttons);
        layout.Children.Add(status);
        var window=new Window{
            Title="DeskSpace · Window layouts",
            Width=485,Height=370,MinWidth=395,MinHeight=330,
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
