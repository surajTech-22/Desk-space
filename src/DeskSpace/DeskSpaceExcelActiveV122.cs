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
    internal static bool SafeToSuggest(bool excelForeground,
        bool verifiedSignature,int writable)=>
        excelForeground&&verifiedSignature&&writable>0;
}
