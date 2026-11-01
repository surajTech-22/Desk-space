using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DeskSpace;
internal static class DeskSpaceShellIconV14
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Info {
        public IntPtr hIcon;public int iIcon;public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string szTypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path,uint attrs,ref Info info,uint size,uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    private static readonly ConcurrentDictionary<string,ImageSource> Cache=new(StringComparer.OrdinalIgnoreCase);
    public static ImageSource? Get(string path)
    {
        if(string.IsNullOrEmpty(path) || path.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase))return null;
        if(Cache.TryGetValue(path,out var source))return source;
        var info=new Info();
        try {
            if(SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf<Info>(),0x101)==IntPtr.Zero||info.hIcon==IntPtr.Zero)return null;
            try {
                var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(info.hIcon,
                    System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(20,20));
                image.Freeze();if(Cache.Count<512)Cache.TryAdd(path,image);
                return image;
            }finally{DestroyIcon(info.hIcon);}
        }catch{return null;}
    }
}