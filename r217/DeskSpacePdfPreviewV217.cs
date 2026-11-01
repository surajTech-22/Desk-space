using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DeskSpace;

// R217: explicit, read-only PDF page preview. Render only one bounded page
// at a time with Windows' PDF renderer; do not invoke browser extensions,
// extract embedded attachments, search PDF content or persist any document.
internal static class DeskSpacePdfPreviewV217
{
    internal const long MaxFileBytes=32L*1024*1024;
    internal const uint MaxPages=100;
    internal static bool Allowed(long length)=>length>0&&length<=MaxFileBytes;

    internal static FrameworkElement Create(string path)
    {
        var panel=new StackPanel{MaxWidth=326,Margin=new Thickness(2,4,2,2)};
        var status=new TextBlock{
            Text="PDF preview: not loaded",FontSize=11,
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,0,0,5)};
        var image=new Image{
            MaxWidth=300,MaxHeight=385,Stretch=Stretch.Uniform,
            HorizontalAlignment=HorizontalAlignment.Center};
        var previous=new Button{Content="Previous",IsEnabled=false,
            Padding=new Thickness(5,3,5,3),Margin=new Thickness(0,0,5,0)};
        var next=new Button{Content="Next",IsEnabled=false,
            Padding=new Thickness(5,3,5,3)};
        var controls=new StackPanel{
            Orientation=Orientation.Horizontal,
            HorizontalAlignment=HorizontalAlignment.Center,
            Margin=new Thickness(0,6,0,0)};
        controls.Children.Add(previous);controls.Children.Add(next);
        panel.Children.Add(status);panel.Children.Add(image);
        panel.Children.Add(controls);
        PdfDocument? document=null;
        bool loading=false;
        int page=0;
        async Task Render(int requested)
        {
            if(loading)return;
            loading=true;
            previous.IsEnabled=false;next.IsEnabled=false;
            try
            {
                var info=new FileInfo(path);
                if(!info.Exists||!Allowed(info.Length) ||
                   (info.Attributes&System.IO.FileAttributes.ReparsePoint)!=0)
                {
                    status.Text="PDF preview unavailable: file absent, linked or larger than 32 MiB.";
                    return;
                }
                if(document==null)
                {
                    var file=await StorageFile.GetFileFromPathAsync(info.FullName);
                    document=await PdfDocument.LoadFromFileAsync(file);
                }
                uint available=Math.Min(document.PageCount,MaxPages);
                if(available==0)
                {
                    status.Text="This PDF contains no renderable pages.";
                    return;
                }
                page=Math.Clamp(requested,0,(int)available-1);
                using var selected=document.GetPage((uint)page);
                using var buffer=new InMemoryRandomAccessStream();
                await selected.RenderToStreamAsync(buffer,new PdfPageRenderOptions{
                    DestinationWidth=300
                });
                buffer.Seek(0);
                using var readable=buffer.AsStreamForRead();
                var bitmap=new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption=BitmapCacheOption.OnLoad;
                bitmap.StreamSource=readable;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source=bitmap;
                status.Text="Page "+(page+1)+" of "+document.PageCount+
                    (document.PageCount>MaxPages?
                        " · first 100 pages previewable":"");
                previous.IsEnabled=page>0;
                next.IsEnabled=(uint)(page+1)<available;
            }
            catch(Exception ex)
            {
                // Password-protected, corrupt or unsupported PDF documents
                // should never crash the widget or disclose document content.
                status.Text="Cannot render PDF ("+ex.GetType().Name+").";
                image.Source=null;
            }
            finally{loading=false;}
        }
        bool initialised=false;
        panel.Loaded+=async (_,_)=>{
            if(initialised)return;
            initialised=true;
            await Render(0);
        };
        previous.Click+=async (_,_)=>await Render(page-1);
        next.Click+=async (_,_)=>await Render(page+1);
        return panel;
    }
}
