using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskSpace;

// Extract the Windows icon of the actual executable or Start menu shortcut.
// This is display-only: no program is launched merely to get its icon.
internal static class DeskSpaceApplicationIconsV65
{
    private static readonly ConcurrentDictionary<string,ImageSource> Cache=
        new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]
        public string szTypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path,uint attributes,
        ref SHFILEINFO info,uint size,uint flags);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static ImageSource? Get(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;
        if(Cache.TryGetValue(path,out var cached))return cached;
        try
        {
            var info=new SHFILEINFO();
            const uint SHGFI_ICON=0x100,SHGFI_SMALLICON=0x1;
            var result=SHGetFileInfo(path,0,ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(),SHGFI_ICON|SHGFI_SMALLICON);
            if(result==IntPtr.Zero||info.hIcon==IntPtr.Zero)return null;
            try
            {
                var icon=Imaging.CreateBitmapSourceFromHIcon(info.hIcon,
                    Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(24,24));
                icon.Freeze();
                Cache[path]=icon;
                return icon;
            }
            finally{DestroyIcon(info.hIcon);}
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ExternalException or ArgumentException)
        {return null;}
    }
}
