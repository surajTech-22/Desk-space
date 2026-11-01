using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

namespace DeskSpace;

// R241: bounded Word Open XML preview. Never load add-ins, external XML
// entities, macros, file attachments, shell commands or remote content.
internal static class DeskSpaceDocxPreviewV241
{
    internal const long MaxFileBytes=16L*1024*1024;
    internal const long MaxXmlBytes=2L*1024*1024;
    internal const int MaxCharacters=3000;
    private const string WordNamespace=
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal sealed record Result(bool Available,string Text,string Status);

    internal static bool Supports(string? extension)=>
        string.Equals(extension,".docx",StringComparison.OrdinalIgnoreCase);

    internal static Result ReadDocument(string path)
    {
        try
        {
            if(!Supports(Path.GetExtension(path)))
                return new(false,string.Empty,"Word preview unavailable for this file type.");
            var info=new FileInfo(path);
            if(!info.Exists || info.Length==0 || info.Length>MaxFileBytes ||
               (info.Attributes&FileAttributes.ReparsePoint)!=0)
                return new(false,string.Empty,
                    "Word preview unavailable: missing, linked or larger than 16 MiB.");
            using var stream=new FileStream(info.FullName,FileMode.Open,
                FileAccess.Read,FileShare.ReadWrite,4096,FileOptions.SequentialScan);
            using var archive=new ZipArchive(stream,ZipArchiveMode.Read,false);
            // Never process anything besides the actual Word document XML.
            var matches=archive.Entries.Where(x=>string.Equals(x.FullName,
                "word/document.xml",StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if(matches.Length!=1 || matches[0].Length<=0 ||
               matches[0].Length>MaxXmlBytes)
                return new(false,string.Empty,
                    "Word preview unavailable: missing or oversized document XML.");
            if(archive.Entries.Any(x=>x.FullName.EndsWith(
                "/vbaProject.bin",StringComparison.OrdinalIgnoreCase)))
                return new(false,string.Empty,
                    "Word preview disabled for documents containing macros.");

            using var xmlStream=matches[0].Open();
            using var safe=new BoundedStream(xmlStream,MaxXmlBytes);
            var settings=new XmlReaderSettings{
                DtdProcessing=DtdProcessing.Prohibit,
                XmlResolver=null,
                MaxCharactersInDocument=MaxXmlBytes,
                MaxCharactersFromEntities=0,
                CloseInput=false,
                IgnoreComments=true,
                IgnoreProcessingInstructions=true};
            using var reader=XmlReader.Create(safe,settings);
            var text=new StringBuilder();
            bool paragraphStarted=false;
            bool truncated=false;
            void Append(string part)
            {
                if(string.IsNullOrEmpty(part))return;
                int remaining=MaxCharacters-text.Length;
                if(remaining<=0){truncated=true;return;}
                if(part.Length>remaining)
                {
                    text.Append(part.AsSpan(0,remaining));
                    truncated=true;
                }
                else text.Append(part);
            }
            while(reader.Read())
            {
                if(reader.NodeType!=XmlNodeType.Element ||
                   reader.NamespaceURI!=WordNamespace)continue;
                if(reader.LocalName=="p")
                {
                    if(paragraphStarted)Append(Environment.NewLine);
                    paragraphStarted=true;
                }
                else if(reader.LocalName=="t")
                {
                    Append(reader.ReadElementContentAsString());
                }
                else if(reader.LocalName=="tab")Append("\t");
                else if(reader.LocalName=="br")Append(Environment.NewLine);
                if(truncated)break;
            }
            if(text.Length==0)
                return new(false,string.Empty,"No displayable Word text found.");
            return new(true,text.ToString(),
                truncated?"Showing only the first 3000 characters of Word text.":
                    "Read-only Word document text; formatting not reproduced.");
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or
            UnauthorizedAccessException or XmlException or ArgumentException or
            NotSupportedException)
        {
            return new(false,string.Empty,
                "Word preview unavailable ("+ex.GetType().Name+").");
        }
    }

    // A tampered ZIP header may lie about an entry's decompressed size.
    // Enforce an independent byte limit while streaming the XML reader.
    private sealed class BoundedStream : Stream
    {
        private readonly Stream source;
        private readonly long limit;
        private long read;
        internal BoundedStream(Stream source,long limit)
        {
            this.source=source;this.limit=limit;
        }
        public override int Read(byte[] buffer,int offset,int count)
        {
            if(read>=limit)throw new InvalidDataException("Word XML byte limit exceeded.");
            int amount=source.Read(buffer,offset,
                (int)Math.Min((long)count,limit-read+1));
            read+=amount;
            if(read>limit)throw new InvalidDataException("Word XML byte limit exceeded.");
            return amount;
        }
        public override int Read(Span<byte> buffer)
        {
            if(read>=limit)throw new InvalidDataException("Word XML byte limit exceeded.");
            int n=source.Read(buffer[..(int)Math.Min(buffer.Length,limit-read+1)]);
            read+=n;
            if(read>limit)throw new InvalidDataException("Word XML byte limit exceeded.");
            return n;
        }
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>read;set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }

    internal static FrameworkElement Create(string path)
    {
        var panel=new StackPanel{MaxWidth=326,Margin=new Thickness(2,4,2,2)};
        var status=new TextBlock{
            Text="Word preview: not loaded",FontSize=11,
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSteelBlue,
            Margin=new Thickness(0,0,0,6)};
        var excerpt=new TextBox{
            IsReadOnly=true,IsTabStop=false,
            TextWrapping=TextWrapping.Wrap,FontSize=11,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Height=245,MaxWidth=300};
        panel.Children.Add(status);panel.Children.Add(excerpt);
        CancellationTokenSource? pending=null;
        panel.Loaded+=async (_,_)=>{
            if(pending!=null)return;
            var scope=new CancellationTokenSource();
            pending=scope;
            status.Text="Loading Word text read-only…";
            try
            {
                var result=await Task.Run(()=>ReadDocument(path),scope.Token);
                if(scope.IsCancellationRequested || !panel.IsLoaded)return;
                status.Text=result.Status;
                excerpt.Text=result.Available?result.Text:string.Empty;
            }
            catch(OperationCanceledException){}
        };
        panel.Unloaded+=(_,_)=>{
            var scope=pending;
            pending=null;
            scope?.Cancel();
            scope?.Dispose();
            excerpt.Clear();
            status.Text="Word preview closed.";
        };
        return panel;
    }
}
