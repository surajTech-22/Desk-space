using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// Read-only ZIP preview. Does not extract entries or execute shell extensions.
// Huge/untrusted archives are bounded by compressed size and displayed rows.
internal static class DeskSpaceArchivePreviewV161
{
    internal static string Summary(string path,int maxEntries=18)
    {
        if(!File.Exists(path))return "Archive is unavailable.";
        if(!Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase))
            return "Not a ZIP archive.";
        if(new FileInfo(path).Length>256L*1024*1024)
            return "Archive is too large for an inline preview (256 MB limit).";
        maxEntries=Math.Clamp(maxEntries,1,30);
        using var zip=ZipFile.OpenRead(path);
        var sb=new StringBuilder();
        sb.Append(zip.Entries.Count).Append(" entries");
        int shown=0;
        foreach(var entry in zip.Entries)
        {
            if(shown>=maxEntries)break;
            // Paths are displayed as text ONLY; never extracted/normalized to
            // any filesystem destination. Remove control characters for UI.
            var clean=new string(entry.FullName
                .Where(c=>!char.IsControl(c)).Take(95).ToArray());
            sb.AppendLine().Append(clean);
            shown++;
        }
        if(zip.Entries.Count>shown)
            sb.AppendLine().Append("+").Append(zip.Entries.Count-shown)
              .Append(" more entries");
        return sb.ToString();
    }

    internal static FrameworkElement Create(string path)
    {
        var wrapper=new StackPanel{Margin=new Thickness(2,4,2,4)};
        wrapper.Children.Add(new TextBlock{
            Text="ZIP contents (read-only)",FontSize=12,
            FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,2,0,5)});
        try
        {
            wrapper.Children.Add(new TextBox{
                Text=Summary(path),FontFamily=new FontFamily("Consolas"),
                FontSize=11,IsReadOnly=true,TextWrapping=TextWrapping.NoWrap,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
                Width=300,Height=190,Background=new SolidColorBrush(
                    Color.FromRgb(24,32,55)),Foreground=Brushes.White,
                BorderThickness=new Thickness(0),Padding=new Thickness(6)});
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException
            or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            wrapper.Children.Add(new TextBlock{
                Text="This ZIP cannot be previewed ("+ex.GetType().Name+").",
                MaxWidth=290,Foreground=Brushes.LightSteelBlue,
                TextWrapping=TextWrapping.Wrap});
        }
        return wrapper;
    }
}
