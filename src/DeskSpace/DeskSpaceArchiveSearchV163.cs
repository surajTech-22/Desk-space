using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// Searches ZIP central directory metadata only. Never extracts, runs, or
// reads the decompressed entry payload. Bounded preview for untrusted archives.
internal static class DeskSpaceArchiveSearchV163
{
    internal const long MaxArchiveBytes=256L*1024*1024;
    internal const int MaxEntries=10000;
    internal const int MaxShown=60;

    internal static (string Summary,string[] Entries) Read(string path,string filter="")
    {
        if(!File.Exists(path))return ("Archive unavailable",Array.Empty<string>());
        if(!Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase))
            return ("Not a ZIP archive",Array.Empty<string>());
        long fileSize=new FileInfo(path).Length;
        if(fileSize>MaxArchiveBytes)return (
            "ZIP preview limit: archives over 256 MB",Array.Empty<string>());
        using var zip=ZipFile.OpenRead(path);
        var entries=zip.Entries;
        if(entries.Count>MaxEntries)return (
            "ZIP preview limit: more than 10,000 entries",Array.Empty<string>());
        var folders=entries.Count(e=>e.FullName.EndsWith("/",StringComparison.Ordinal));
        var files=entries.Count-folders;
        long uncompressed=0;
        foreach(var e in entries)
        {
            if(e.Length>long.MaxValue-uncompressed)
                return ("ZIP size totals exceed preview range",Array.Empty<string>());
            uncompressed+=e.Length;
        }
        var q=(filter??"").Trim();
        var matches=entries.Where(e=>q.Length==0||
            e.FullName.Contains(q,StringComparison.OrdinalIgnoreCase)).ToArray();
        var visible=matches.Take(MaxShown).Select(e=>
        {
            var safe=new string(e.FullName.Where(c=>!char.IsControl(c))
                .Take(100).ToArray());
            return (e.FullName.EndsWith("/",StringComparison.Ordinal)?"[DIR] ":"[FILE] ")+safe;
        }).ToArray();
        var header=$"{files:N0} files · {folders:N0} folders · {uncompressed:N0} bytes unpacked";
        if(q.Length>0)header+=$" · {matches.Length:N0} matches";
        if(matches.Length>visible.Length)header+=$" · showing first {visible.Length}";
        return (header,visible);
    }

    internal static FrameworkElement Create(string path)
    {
        var panel=new StackPanel{Margin=new Thickness(2,4,2,4)};
        panel.Children.Add(new TextBlock{Text="ZIP contents · read-only",
            Foreground=Brushes.LightSteelBlue,FontSize=12,
            FontWeight=FontWeights.SemiBold});
        var search=new TextBox{Width=300,Margin=new Thickness(0,5,0,6),
            ToolTip="Filter names inside ZIP, without extracting files"};
        panel.Children.Add(search);
        var meta=new TextBlock{Width=300,Foreground=Brushes.LightSteelBlue,
            TextWrapping=TextWrapping.Wrap,FontSize=10,
            Margin=new Thickness(0,0,0,5)};
        panel.Children.Add(meta);
        var results=new TextBox{FontFamily=new FontFamily("Consolas"),
            FontSize=11,IsReadOnly=true,TextWrapping=TextWrapping.NoWrap,
            Width=300,Height=190,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            Background=new SolidColorBrush(Color.FromRgb(24,32,55)),
            Foreground=Brushes.White,BorderThickness=new Thickness(0),
            Padding=new Thickness(6)};
        panel.Children.Add(results);
        void Refresh(string filter)
        {
            try
            {
                var (summary,names)=Read(path,filter);
                meta.Text=summary;
                results.Text=string.Join(Environment.NewLine,names);
            }
            catch(Exception error) when(error is IOException or InvalidDataException
                or UnauthorizedAccessException or ArgumentException
                or NotSupportedException)
            {
                meta.Text="ZIP preview unavailable ("+error.GetType().Name+")";
                results.Text="";
            }
        }
        search.TextChanged+=(_,_)=>Refresh(search.Text);
        Refresh("");
        return panel;
    }
}
