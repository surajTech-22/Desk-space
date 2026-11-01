using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DeskSpace;

// The default browser is chosen by Windows, NEVER by DeskSpace. We only
// pre-open an actual tab if that default browser already has a live window;
// otherwise we leave it closed until due time, as requested.
// Windows can reject foreground restoration; it is best effort.
internal static class DeskSpaceReminderBrowserV80
{
    internal const int PreopenSeconds=12;
    private sealed record Prepared(IntPtr BrowserWindow,DateTime Due);
    private static readonly Dictionary<Guid,Prepared> prepared=new();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window,int command);

    internal static bool TryUrl(string? text,out string result)
    {
        result="";
        if(string.IsNullOrWhiteSpace(text)||text.Length>2048 ||
           text.Any(char.IsControl))return false;
        string value=text.Trim();
        if(!value.Contains("://",StringComparison.Ordinal) &&
           value.Contains('.') &&
           !value.Contains(' ') &&
           !value.StartsWith("javascript:",StringComparison.OrdinalIgnoreCase) &&
           !value.StartsWith("file:",StringComparison.OrdinalIgnoreCase))
            value="https://"+value;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) ||
           uri.Scheme is not ("http" or "https") ||
           string.IsNullOrEmpty(uri.Host) ||
           !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        result=uri.AbsoluteUri;
        return true;
    }
    internal static bool ShouldPrepare(ReminderItem r,DateTime now)
        =>!r.Done && r.LastAlert==null &&
          r.AlertAction=="Open URL" && TryUrl(r.AlertTarget,out _) &&
          r.Due>now && r.Due-now<=TimeSpan.FromSeconds(PreopenSeconds);

    private static string? DefaultBrowserProcess()
    {
        try
        {
            using var key=Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            string value=(key?.GetValue("ProgId") as string??"").ToLowerInvariant();
            if(value.Contains("chrome")||value.Contains("chromium"))return "chrome";
            if(value.Contains("edge")||value.Contains("microsoftedge"))return "msedge";
            if(value.Contains("firefox"))return "firefox";
            if(value.Contains("brave"))return "brave";
            if(value.Contains("vivaldi"))return "vivaldi";
            if(value.Contains("opera"))return "opera";
        }
        catch(Exception){}
        return null;
    }
    private static IntPtr LiveBrowserWindow()
    {
        string? name=DefaultBrowserProcess();
        if(name==null)return IntPtr.Zero;
        try
        {
            foreach(var process in Process.GetProcessesByName(name))
            {
                try
                {
                    var handle=process.MainWindowHandle;
                    if(handle!=IntPtr.Zero && IsWindow(handle))return handle;
                }
                finally{process.Dispose();}
            }
        }catch(Exception){}
        return IntPtr.Zero;
    }
    private static void OpenDefault(string url)
    {
        Process.Start(new ProcessStartInfo{
            FileName=url,UseShellExecute=true
        });
    }
    private static async void RestoreFocus(IntPtr window)
    {
        await Task.Delay(450);
        if(window!=IntPtr.Zero && IsWindow(window))
            SetForegroundWindow(window);
    }
    internal static void Prepare(ReminderItem item,DateTime now)
    {
        if(!ShouldPrepare(item,now)||prepared.ContainsKey(item.Id))return;
        var browser=LiveBrowserWindow();
        if(browser==IntPtr.Zero)return; // do not open browser early
        if(!TryUrl(item.AlertTarget,out var url))return;
        try
        {
            var formerlyFocused=GetForegroundWindow();
            // Existing browser: ask the Windows default HTTP(S) handler for
            // the tab 10–12 seconds early; restore the user's previous focus.
            OpenDefault(url);
            prepared[item.Id]=new Prepared(browser,item.Due);
            if(formerlyFocused!=IntPtr.Zero && formerlyFocused!=browser)
                RestoreFocus(formerlyFocused);
        }
        catch(Exception error){Debug.WriteLine("DeskSpace preopen URL: "+error);}
    }
    internal static void LaunchAtDue(ReminderItem item)
    {
        if(!TryUrl(item.AlertTarget,out var url))return;
        try
        {
            if(prepared.Remove(item.Id,out var ready) &&
               ready.BrowserWindow!=IntPtr.Zero &&
               IsWindow(ready.BrowserWindow))
            {
                ShowWindow(ready.BrowserWindow,9); // SW_RESTORE
                if(SetForegroundWindow(ready.BrowserWindow))return;
                // A window-activation restriction can prevent the final
                // focus change; do NOT open a duplicate tab as a fallback.
                Debug.WriteLine("DeskSpace: Windows prevented browser activation");
                return;
            }
            // No running default browser at preopen time: open normally at due,
            // allowing Windows to launch the default browser into foreground.
            OpenDefault(url);
        }
        catch(Exception error){Debug.WriteLine("DeskSpace due URL: "+error);}
    }
    internal static void ForgetMissing(IReadOnlyCollection<Guid> live)
    {
        foreach(var id in prepared.Keys.Where(id=>!live.Contains(id)).ToArray())
            prepared.Remove(id);
    }
}
