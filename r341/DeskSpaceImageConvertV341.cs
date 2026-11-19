using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DeskSpace;

// R329-06: Manual, bounded conversion with preview and distinct destination.
// Each output is staged to a temporary file and moved with NO overwrite.
internal static class DeskSpaceImageConvertV341
{
    internal sealed record Plan(string Source,string Target,string Format);
    internal const int MaximumFiles=50;
    internal const long MaximumSourceBytes=50L*1024*1024;
    internal const long MaximumPixels=24000000;

    internal static IReadOnlyList<Plan> Preview(
        IReadOnlyList<string> sources,string destination,string format)
    {
        if(format is not ("png" or "jpg"))
            throw new ArgumentException("Choose PNG or JPG.");
        if(sources.Count==0||sources.Count>MaximumFiles)
            throw new ArgumentException("Select between 1 and 50 images.");
        if(!Directory.Exists(destination))throw new DirectoryNotFoundException(destination);
        var result=new List<Plan>(sources.Count);
        var outputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var raw in sources)
        {
            var path=Path.GetFullPath(raw);
            string ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff"))
                throw new ArgumentException("Unsupported image type: "+ext);
            if(!File.Exists(path))throw new FileNotFoundException("Image missing.",path);
            if(new FileInfo(path).Length>MaximumSourceBytes)
                throw new InvalidOperationException("Files over 50 MB are not converted.");
            string name=Path.GetFileNameWithoutExtension(path);
            string target=Path.Combine(destination,name+"_converted."+format);
            // No overwrites, even for two inputs sharing a base filename.
            if(string.Equals(path,target,StringComparison.OrdinalIgnoreCase) ||
                !outputs.Add(target)||File.Exists(target))
                throw new IOException("Output exists or collides: "+Path.GetFileName(target)+
                    ". Choose another destination or rename the original.");
            result.Add(new Plan(path,target,format));
        }
        return result;
    }

    internal static async Task<int> ConvertAsync(
        IReadOnlyList<Plan> plan,CancellationToken ct=default)
    {
        int completed=0;
        foreach(var item in plan)
        {
            ct.ThrowIfCancellationRequested();
            string temp=item.Target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                // Limit file sizes AND decoded dimensions to avoid excessive
                // RAM usage on large source images.
                await Task.Run(()=>
                {
                    ct.ThrowIfCancellationRequested();
                    using var read=new FileStream(item.Source,FileMode.Open,
                        FileAccess.Read,FileShare.Read);
                    var decoder=BitmapDecoder.Create(read,BitmapCreateOptions.DelayCreation,
                        BitmapCacheOption.None);
                    var frame=decoder.Frames[0];
                    if((long)frame.PixelWidth*frame.PixelHeight>MaximumPixels)
                        throw new InvalidOperationException(
                            "Image exceeds 24 million pixels: "+Path.GetFileName(item.Source));
                    BitmapEncoder encoder=item.Format=="png"
                        ?new PngBitmapEncoder()
                        :new JpegBitmapEncoder{QualityLevel=92};
                    encoder.Frames.Add(BitmapFrame.Create(frame));
                    using var output=new FileStream(temp,FileMode.CreateNew,
                        FileAccess.Write,FileShare.None);
                    encoder.Save(output);
                    ct.ThrowIfCancellationRequested();
                },ct);
                // Atomic publication and no file overwrite.
                File.Move(temp,item.Target);
                completed++;
            }
            finally
            {
                if(File.Exists(temp))File.Delete(temp);
            }
        }
        return completed;
    }

    internal static Window Create(Window? owner=null)
    {
        var window=new Window{Title="DeskSpace · Safe image conversion",
            Width=740,Height=530,MinWidth=570,MinHeight=440,
            Background=Brushes.White,ShowInTaskbar=false,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null&&owner.IsVisible)window.Owner=owner;
        var root=new DockPanel{Margin=new Thickness(16)};window.Content=root;
        var heading=new StackPanel();
        heading.Children.Add(new TextBlock{Text="Batch image conversion",
            FontSize=21,FontWeight=FontWeights.Bold});
        heading.Children.Add(new TextBlock{Text="Choose up to 50 images and a different output folder. Preview outputs before conversion. Originals are untouched; existing output files are never overwritten.",
            Foreground=Brushes.DimGray,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,5,0,9)});
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var controls=new StackPanel{Margin=new Thickness(0,0,0,8)};
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        var choose=new Button{Content="Choose images",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var destination=new Button{Content="Output folder",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var type=new ComboBox{Width=110,SelectedIndex=0};
        type.Items.Add("png");type.Items.Add("jpg");
        row.Children.Add(choose);row.Children.Add(destination);
        row.Children.Add(type);controls.Children.Add(row);
        var folderText=new TextBlock{Text="No output folder selected",
            Foreground=Brushes.DimGray,Margin=new Thickness(0,7,0,6),
            TextTrimming=TextTrimming.CharacterEllipsis};
        controls.Children.Add(folderText);
        var row2=new StackPanel{Orientation=Orientation.Horizontal};
        var preview=new Button{Content="Preview",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0)};
        var convert=new Button{Content="Convert",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0),IsEnabled=false};
        var cancel=new Button{Content="Cancel",Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,8,0),IsEnabled=false};
        var status=new TextBlock{Text="Ready",VerticalAlignment=VerticalAlignment.Center};
        row2.Children.Add(preview);row2.Children.Add(convert);
        row2.Children.Add(cancel);row2.Children.Add(status);
        controls.Children.Add(row2);
        DockPanel.SetDock(controls,Dock.Top);root.Children.Add(controls);
        var results=new TextBox{IsReadOnly=true,AcceptsReturn=true,
            FontFamily=new FontFamily("Consolas"),FontSize=12,Padding=new Thickness(7),
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(results);
        string[] selected=Array.Empty<string>();string outputFolder="";
        IReadOnlyList<Plan>? candidate=null;CancellationTokenSource? pending=null;
        bool busy=false;
        void Invalidate(){candidate=null;convert.IsEnabled=false;results.Clear();}
        choose.Click+=(_,_)=>{
            if(busy)return;
            var dlg=new OpenFileDialog{Title="Images to convert",Multiselect=true,
                Filter="Supported images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"};
            if(dlg.ShowDialog(window)==true){selected=dlg.FileNames;Invalidate();
                status.Text=selected.Length+" image(s) selected";}
        };
        destination.Click+=(_,_)=>{
            if(busy)return;
            var dlg=new OpenFolderDialog{Title="Destination for converted images"};
            if(dlg.ShowDialog(window)==true){outputFolder=dlg.FolderName;
                folderText.Text=outputFolder;Invalidate();}
        };
        type.SelectionChanged+=(_,_)=>Invalidate();
        preview.Click+=(_,_)=>{
            if(busy)return;
            try{
                candidate=Preview(selected,outputFolder,(string)type.SelectedItem);
                results.Text=string.Join(Environment.NewLine,
                    candidate.Select(x=>Path.GetFileName(x.Source)+" → "+x.Target));
                convert.IsEnabled=true;
                status.Text="Preview ready — "+candidate.Count+" output(s)";
            }
            catch(Exception ex)when(ex is IOException or ArgumentException
                or InvalidOperationException or UnauthorizedAccessException)
            {Invalidate();status.Text=ex.Message;}
        };
        convert.Click+=async (_,_)=>{
            if(candidate==null||busy)return;
            busy=true;convert.IsEnabled=false;choose.IsEnabled=false;
            destination.IsEnabled=false;cancel.IsEnabled=true;
            pending=new CancellationTokenSource();
            try{
                status.Text="Converting locally…";
                int count=await ConvertAsync(candidate,pending.Token);
                if(!pending.IsCancellationRequested)
                    status.Text=count+" image(s) converted; originals unchanged.";
            }
            catch(OperationCanceledException){status.Text="Conversion cancelled.";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException)
            {status.Text=ex.Message;}
            finally{
                busy=false;choose.IsEnabled=true;destination.IsEnabled=true;
                cancel.IsEnabled=false;pending?.Dispose();pending=null;
                candidate=null;convert.IsEnabled=false;
            }
        };
        cancel.Click+=(_,_)=>pending?.Cancel();
        window.Closed+=(_,_)=>pending?.Cancel();
        return window;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
