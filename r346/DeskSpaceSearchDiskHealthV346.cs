using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R346: on-demand disk footprint and persistence report for the compact
// name-only local search index. Zero content reads, writes, VACUUM commands,
// or destructive cache cleanup while the widget is running.
internal static class DeskSpaceSearchDiskHealthV346
{
    internal sealed record Cache(string File,long Bytes,bool Current);
    internal sealed record Report(IReadOnlyList<Cache> Caches,
        bool CurrentAvailable,long CurrentBytes,long LegacyBytes);
    internal const long WarningBytes=512L*1024*1024;

    internal static Report Inspect(string? folder=null)
    {
        folder??=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"DeskSpace","Search");
        if(!Directory.Exists(folder))
            return new Report(Array.Empty<Cache>(),false,0,0);
        var items=new List<Cache>();
        // SQLite WAL mode stores uncheckpointed index pages in -wal and
        // allocates a -shm coordination file. Ignoring them underreports disk
        // usage, especially while an index build is in progress.
        const string currentDatabase="filenames-v336.sqlite";
        const string oldDatabase="filenames-v330.sqlite";
        foreach(var name in new[]{
            currentDatabase,currentDatabase+"-wal",currentDatabase+"-shm",
            oldDatabase,oldDatabase+"-wal",oldDatabase+"-shm"})
        {
            var filename=Path.Combine(folder,name);
            try
            {
                var info=new FileInfo(filename);
                if(!info.Exists ||
                    (info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
                items.Add(new Cache(name,info.Length,
                    name.StartsWith(currentDatabase,StringComparison.Ordinal)));
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or System.Security.SecurityException){}
        }
        // A stray -wal/-shm without its database must not be reported as a
        // healthy persisted index, although its footprint is still counted.
        return new Report(items,items.Any(x=>
                x.Current && x.File==currentDatabase && x.Bytes>0),
            items.Where(x=>x.Current).Sum(x=>x.Bytes),
            items.Where(x=>!x.Current).Sum(x=>x.Bytes));
    }

    internal static bool Oversized(long bytes)=>bytes>=WarningBytes;

    internal static string Describe(Report report)
    {
        var current=report.CurrentAvailable?
            $"Compact filename cache: {report.CurrentBytes/1048576.0:N1} MB.":
            "Compact filename cache not yet available; indexing may still be starting.";
        string old=report.LegacyBytes>0?
            $" Previous full-path cache: {report.LegacyBytes/1048576.0:N1} MB (not modified).":
            " No previous full-path cache detected.";
        string warning=Oversized(report.CurrentBytes)?
            " WARNING: the current index exceeds 512 MB. Check indexing health before restarting or rebuilding.":
            " The compact cache is below the 512 MB warning threshold.";
        long sidecarBytes=report.Caches
            .Where(x=>x.File.EndsWith("-wal",StringComparison.Ordinal)||
                      x.File.EndsWith("-shm",StringComparison.Ordinal))
            .Sum(x=>x.Bytes);
        return current+Environment.NewLine+old+Environment.NewLine+
            $" SQLite WAL/SHM sidecars included: {sidecarBytes/1048576.0:N1} MB."+
            Environment.NewLine+warning+
            Environment.NewLine+
            "This view reads only cache metadata. No filenames, documents, or search queries are displayed. "+
            "Files are never deleted from this tool.";
    }

    internal static Window Create(Window? owner=null,string? folder=null)
    {
        var win=new Window{
            Title="DeskSpace · Search disk index health",Width=580,Height=350,
            MinWidth=440,MinHeight=280,ShowInTaskbar=false,
            Background=Brushes.White,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null&&owner.IsVisible)win.Owner=owner;
        var root=new StackPanel{Margin=new Thickness(18)};
        win.Content=root;
        root.Children.Add(new TextBlock{Text="Persistent search cache",
            FontSize=20,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,9)});
        var details=new TextBlock{TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,10)};
        root.Children.Add(details);
        var refresh=new Button{Content="Refresh metadata",
            Padding=new Thickness(11,6,11,6),
            HorizontalAlignment=HorizontalAlignment.Left};
        root.Children.Add(refresh);
        root.Children.Add(new TextBlock{
            Text="The on-disk index survives application restarts. This report never scans documents or modifies databases. Obsolete caches are not deleted automatically.",
            Foreground=Brushes.DimGray,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,14,0,0)});
        void ShowStats()
        {
            try{details.Text=Describe(Inspect(folder));}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or System.Security.SecurityException)
            {details.Text="Unable to inspect local search cache metadata ("+
                ex.GetType().Name+").";}
        }
        refresh.Click+=(_,_)=>ShowStats();
        ShowStats();
        return win;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
