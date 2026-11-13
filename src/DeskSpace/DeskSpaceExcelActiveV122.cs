using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Non-invasive foreground check. No passwords, browser contents or Excel
// document contents are observed here; Excel preview stays in the existing
// STA/COM worker and never writes until the user explicitly approves.
internal static class DeskSpaceExcelActiveV122
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(
        IntPtr handle,out uint processId);
    internal static bool ForegroundExcel()
    {
        try {
            var hwnd=GetForegroundWindow();
            if(hwnd==IntPtr.Zero)return false;
            GetWindowThreadProcessId(hwnd,out uint id);
            if(id==0)return false;
            using var process=Process.GetProcessById((int)id);
            return process.ProcessName.Equals("EXCEL",
                StringComparison.OrdinalIgnoreCase);
        }catch{return false;}
    }
    [DllImport("user32.dll")] private static extern bool EnumWindows(
        WindowEnumV377 callback,IntPtr arg);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(
        IntPtr handle,System.Text.StringBuilder output,int length);
    private delegate bool WindowEnumV377(IntPtr handle,IntPtr arg);

    internal static bool AnyExcelOpen()
    {
        try
        {
            if(ForegroundExcel())return true;
            bool found=false;
            var sb=new System.Text.StringBuilder(64);
            EnumWindows((hwnd,_)=>{
                if(!IsWindowVisible(hwnd))return true;
                sb.Clear();
                if(GetClassName(hwnd,sb,sb.Capacity)>0 && sb.ToString()=="XLMAIN")
                {
                    found=true;
                    return false;
                }
                return true;
            },IntPtr.Zero);
            if(found)return true;
            var procs=Process.GetProcessesByName("EXCEL");
            try
            {
                foreach(var p in procs)
                {
                    try{if(p.MainWindowHandle!=IntPtr.Zero)return true;}
                    catch{}
                }
            }
            finally
            {
                foreach(var p in procs)try{p.Dispose();}catch{}
            }
            return false;
        }
        catch{return false;}
    }

    internal static bool SafeToSuggest(bool excelForeground,
        bool verifiedSignature,int writable)=>
        excelForeground&&verifiedSignature&&writable>0;
}
