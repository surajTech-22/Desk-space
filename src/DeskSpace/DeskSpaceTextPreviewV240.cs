using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R240: a Deck preview must never run a script, read beyond a bounded text
// excerpt, extract an archive, follow an explicit reparse point, or alter
// clipboard/file data. The user explicitly expands a preview to start reading.
internal static class DeskSpaceTextPreviewV240
{
    internal const long MaxFileBytes=2L*1024*1024;
    internal const int MaxCharacters=3600;
    private static readonly HashSet<string> Extensions=new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".txt",".md",".csv",".tsv",".json",".xml",".yaml",".yml",
        ".log",".ini",".cs",".py",".ps1",".js",".css",".html",".sql"
    };

    internal sealed record Result(bool Available,string Text,string Status);

    internal static bool Supports(string? extension)=>
        extension!=null && Extensions.Contains(extension);

    internal static Result ReadSnippet(string path)
    {
        try
        {
            var extension=Path.GetExtension(path);
            if(!Supports(extension))
                return new(false,string.Empty,"Text preview unavailable for this file type.");
            var file=new FileInfo(path);
            if(!file.Exists)
                return new(false,string.Empty,"Text preview unavailable: file is missing.");
            if((file.Attributes & FileAttributes.ReparsePoint)!=0)
                return new(false,string.Empty,"Text preview unavailable for linked files.");
            if(file.Length<=0 || file.Length>MaxFileBytes)
                return new(false,string.Empty,"Text preview unavailable: empty or larger than 2 MiB.");
            // FileShare.ReadWrite enables preview of saved logs while rejecting
            // delete/rename races; FileStream is never opened for writing.
            using var stream=new FileStream(file.FullName,FileMode.Open,
                FileAccess.Read,FileShare.ReadWrite,4096,FileOptions.SequentialScan);
            using var reader=new StreamReader(stream,
                new UTF8Encoding(false,true),detectEncodingFromByteOrderMarks:true,
                bufferSize:1024);
            var buffer=new char[MaxCharacters+1];
            int count=0;
            while(count<buffer.Length)
            {
                int read=reader.Read(buffer,count,buffer.Length-count);
                if(read==0)break;
                count+=read;
            }
            int displayed=Math.Min(count,MaxCharacters);
            for(int i=0;i<displayed;i++)
            {
                char c=buffer[i];
                if(char.IsControl(c) && c!='\r' && c!='\n' && c!='\t')
                    return new(false,string.Empty,"Text preview unavailable: binary/control characters.");
            }
            string text=new(buffer,0,displayed);
            return new(true,text,
                count>MaxCharacters || stream.Position<stream.Length?
                    "Showing only the beginning of the file (read-only).":
                    "Read-only text preview.");
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or DecoderFallbackException or ArgumentException or NotSupportedException)
        {
            return new(false,string.Empty,
                "Text preview unavailable ("+ex.GetType().Name+").");
        }
    }

    internal static FrameworkElement Create(string path)
    {
        var panel=new StackPanel{MaxWidth=326,Margin=new Thickness(2,4,2,2)};
        var status=new TextBlock{
            Text="Text preview: not loaded",
            TextWrapping=TextWrapping.Wrap,FontSize=11,
            Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,0,0,6)};
        var preview=new TextBox{
            IsReadOnly=true,IsTabStop=false,
            FontFamily=new FontFamily("Consolas"),
            FontSize=11,TextWrapping=TextWrapping.NoWrap,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Height=245,MaxWidth=300,
            Text="Open the Deck preview to read the first part of this file."};
        panel.Children.Add(status);
        panel.Children.Add(preview);
        CancellationTokenSource? pending=null;
        panel.Loaded+=async (_,_)=>{
            if(pending!=null)return;
            var scope=new CancellationTokenSource();
            pending=scope;
            status.Text="Loading read-only text preview…";
            try
            {
                var result=await Task.Run(()=>ReadSnippet(path),scope.Token);
                if(scope.IsCancellationRequested || !panel.IsLoaded)return;
                status.Text=result.Status;
                preview.Text=result.Available?result.Text:string.Empty;
            }
            catch(OperationCanceledException){}
        };
        panel.Unloaded+=(_,_)=>{
            var scope=pending;
            pending=null;
            scope?.Cancel();
            scope?.Dispose();
            // No retained plaintext in a hidden or disposed Deck view.
            preview.Clear();
            status.Text="Text preview closed.";
        };
        return panel;
    }
}
