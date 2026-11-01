using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R254: deterministic read-only batch rename PREVIEW. There is deliberately
// NO rename/commit button until undo/rollback and approval are implemented.
internal static class DeskSpaceBatchRenamePreviewV254
{
    internal const int MaxFiles=128;
    internal sealed record Proposed(string Before,string After,bool Conflict);
    internal sealed record Plan(bool Eligible,string Explanation,
        IReadOnlyList<Proposed> Files);

    private static bool SafeAffix(string? value)=>
        value!=null && value.Length<=64 &&
        !value.Any(c=>char.IsControl(c) || "<>:\"/\\|?*".Contains(c));

    internal static Plan Prepare(IEnumerable<string> names,string prefix,string suffix)
    {
        if(!SafeAffix(prefix) || !SafeAffix(suffix) ||
            (prefix.Length==0 && suffix.Length==0))
            return new(false,"Enter a valid nonempty prefix or suffix "+
                "(64 characters maximum each).",Array.Empty<Proposed>());
        var input=names?.ToArray()??Array.Empty<string>();
        if(input.Length==0 || input.Length>MaxFiles)
            return new(false,"Choose 1–128 direct files.",Array.Empty<Proposed>());
        if(input.Any(name=>string.IsNullOrWhiteSpace(name) ||
            Path.GetFileName(name)!=name ||
            name.EndsWith(' ') || name.EndsWith('.') ||
            name.Any(c=>char.IsControl(c) || "<>:\"/\\|?*".Contains(c))) ||
           input.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=input.Length)
            return new(false,"Invalid or duplicate Windows file names.",
                Array.Empty<Proposed>());
        var source=new HashSet<string>(input,StringComparer.OrdinalIgnoreCase);
        var output=new List<Proposed>();
        foreach(var name in input.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase))
        {
            var stem=Path.GetFileNameWithoutExtension(name);
            var extension=Path.GetExtension(name);
            var proposed=prefix+stem+suffix+extension;
            bool conflict=proposed.Length>255 ||
                proposed.EndsWith(' ') || proposed.EndsWith('.') ||
                string.Equals(proposed,name,StringComparison.OrdinalIgnoreCase) ||
                (source.Contains(proposed) &&
                 !string.Equals(proposed,name,StringComparison.OrdinalIgnoreCase));
            output.Add(new(name,proposed,conflict));
        }
        var collisions=output.GroupBy(x=>x.After,
            StringComparer.OrdinalIgnoreCase)
            .Where(g=>g.Count()>1)
            .SelectMany(g=>g).Select(x=>x.Before)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<output.Count;i++)
            if(collisions.Contains(output[i].Before))
                output[i]=output[i] with {Conflict=true};
        bool eligible=!output.Any(x=>x.Conflict);
        return new(eligible,eligible?
            "Preview only: "+output.Count+" proposed names. No files changed.":
            "Blocked: at least one result collides, is unchanged or has an invalid length.",
            output);
    }

    internal static Plan InspectFolder(string folder,string prefix,string suffix)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(folder) ||
               !Directory.Exists(folder))
                return new(false,"Select an existing folder.",Array.Empty<Proposed>());
            var root=new DirectoryInfo(folder);
            if((root.Attributes&FileAttributes.ReparsePoint)!=0)
                return new(false,"Linked folders are not supported.",Array.Empty<Proposed>());
            var files=root.EnumerateFiles("*",SearchOption.TopDirectoryOnly)
                .Where(f=>(f.Attributes&(FileAttributes.ReparsePoint|
                    FileAttributes.Hidden|FileAttributes.System))==0)
                .Select(f=>f.Name).Take(MaxFiles+1).ToArray();
            return Prepare(files,prefix,suffix);
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return new(false,"Cannot preview folder ("+e.GetType().Name+").",
                Array.Empty<Proposed>());
        }
    }

    internal static void Show(Window owner)
    {
        var window=new Window{
            Title="DeskSpace — Batch rename preview (no changes)",
            Width=540,Height=575,MinWidth=420,MinHeight=345,
            Owner=owner.IsVisible?owner:null,ShowInTaskbar=false,
            WindowStartupLocation=owner.IsVisible?
                WindowStartupLocation.CenterOwner:
                WindowStartupLocation.CenterScreen};
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(new TextBlock{
            Text="Batch rename — preview only",
            FontSize=18,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,6)});
        content.Children.Add(new TextBlock{
            Text="Select a folder and preview prefix/suffix changes. "+
                 "Only direct visible files are considered, up to 128. "+
                 "No renaming, deletion, move or background watching.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,12)});
        var prefix=new TextBox{MinHeight=28,MaxLength=64};
        var suffix=new TextBox{MinHeight=28,MaxLength=64};
        AutomationProperties.SetName(prefix,"Filename prefix");
        AutomationProperties.SetName(suffix,"Filename suffix");
        content.Children.Add(new TextBlock{Text="Prefix"});
        content.Children.Add(prefix);
        content.Children.Add(new TextBlock{
            Text="Suffix",Margin=new Thickness(0,8,0,0)});
        content.Children.Add(suffix);
        var inspect=new Button{
            Content="Choose folder and preview",
            Padding=new Thickness(9,6,9,6),
            HorizontalAlignment=HorizontalAlignment.Left,
            Margin=new Thickness(0,12,0,8)};
        var status=new TextBlock{
            Text="No preview calculated.",
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,8)};
        var rows=new StackPanel();
        content.Children.Add(inspect);content.Children.Add(status);
        content.Children.Add(rows);
        window.Content=new ScrollViewer{
            Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        inspect.Click+=async (_,_)=>{
            using var picker=new Forms.FolderBrowserDialog{
                Description="Choose folder for read-only batch rename preview",
                ShowNewFolderButton=false};
            if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
            var selected=picker.SelectedPath;
            var beforePrefix=prefix.Text;
            var beforeSuffix=suffix.Text;
            rows.Children.Clear();
            inspect.IsEnabled=false;
            status.Text="Inspecting filenames; no changes made…";
            try
            {
                var plan=await Task.Run(()=>InspectFolder(
                    selected,beforePrefix,beforeSuffix));
                if(!window.IsLoaded)return;
                status.Text=plan.Explanation;
                foreach(var entry in plan.Files)
                    rows.Children.Add(new TextBlock{
                        Text=entry.Before+" → "+entry.After+
                            (entry.Conflict?"  [BLOCKED]":""),
                        TextWrapping=TextWrapping.Wrap,
                        Foreground=entry.Conflict?Brushes.Firebrick:Brushes.Black,
                        Margin=new Thickness(0,0,0,5)});
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException)
            {
                if(window.IsLoaded)status.Text="Preview failed ("+
                    e.GetType().Name+"). No files changed.";
            }
            finally{if(window.IsLoaded)inspect.IsEnabled=true;}
        };
        window.Show();
    }
}
