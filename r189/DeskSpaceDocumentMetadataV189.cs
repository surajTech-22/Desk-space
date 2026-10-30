using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R189: explicit read-only previews for Office package structure, PCM WAV
// metadata and one folder level. No shell execution, archive extraction,
// audio playback, XML parsing, network, or background enumeration.
internal static class DeskSpaceDocumentMetadataV189
{
    internal const long MaxOfficeBytes=256L*1024*1024;
    internal const int MaxPackageEntries=4096;
    internal const int MaxFolderEntries=512;
    internal static bool Supports(string extension)=>extension is
        ".docx" or ".xlsx" or ".pptx" or ".wav";

    private static string Clean(string input,int max=95)=>
        new string(input.Where(c=>!char.IsControl(c)).Take(max).ToArray());

    private static bool HasLink(FileSystemInfo info)=>
        (info.Attributes&FileAttributes.ReparsePoint)!=0;

    internal static string Describe(string path)
    {
        if(Directory.Exists(path))return Folder(path);
        var info=new FileInfo(path);
        if(!info.Exists)return "Preview unavailable: item not found.";
        return info.Extension.ToLowerInvariant() switch
        {
            ".docx" or ".xlsx" or ".pptx"=>Office(info),
            ".wav"=>Wave(info),
            _=>"This file type has no additional metadata preview."
        };
    }

    private static string Folder(string path)
    {
        var dir=new DirectoryInfo(path);
        if(HasLink(dir))return "Folder preview refused: junction or symbolic link.";
        int files=0,folders=0,scanned=0;
        var names=new List<string>();
        foreach(var item in dir.EnumerateFileSystemInfos())
        {
            if(++scanned>MaxFolderEntries)
                return "Folder contains more than 512 direct entries; "+
                    "preview stopped to avoid large directory scans.";
            if(HasLink(item))continue;
            bool folder=(item.Attributes&FileAttributes.Directory)!=0;
            if(folder)folders++;else files++;
            if(names.Count<16)
                names.Add((folder?"[DIR] ":"[FILE] ")+Clean(item.Name));
        }
        var sb=new StringBuilder();
        sb.Append("Folder · ").Append(files).Append(" files, ")
            .Append(folders).AppendLine(" subfolders (one level)");
        foreach(var name in names)sb.AppendLine(name);
        if(files+folders>names.Count)
            sb.Append("+").Append(files+folders-names.Count).AppendLine(" more");
        return sb.ToString();
    }

    private static string Office(FileInfo info)
    {
        if(info.Length>MaxOfficeBytes)
            return "Office package exceeds the 256 MB preview limit.";
        using var zip=ZipFile.OpenRead(info.FullName);
        if(zip.Entries.Count>MaxPackageEntries)
            return "Office package has more than 4,096 parts; preview stopped.";
        // Only central-directory names/lengths: never read or decompress
        // document body, worksheet cells, macros or embedded attachments.
        string ext=info.Extension.ToLowerInvariant();
        string prefix=ext switch{
            ".docx"=>"word/",".xlsx"=>"xl/",_=>"ppt/"
        };
        string section=ext switch{
            ".docx"=>"Word", ".xlsx"=>"Excel",_=>"PowerPoint"
        };
        string main=ext switch{
            ".docx"=>"word/document.xml",
            ".xlsx"=>"xl/workbook.xml",
            _=>"ppt/presentation.xml"
        };
        bool valid=zip.Entries.Any(x=>x.FullName.Equals(
            "[Content_Types].xml",StringComparison.OrdinalIgnoreCase)) &&
            zip.Entries.Any(x=>x.FullName.Equals(main,
                StringComparison.OrdinalIgnoreCase));
        if(!valid)return "This "+section+" file lacks expected package parts.";
        int media=0,embedded=0,units=0;
        long uncompressed=0;
        string unitFolder=ext switch{
            ".docx"=>"word/header", ".xlsx"=>"xl/worksheets/sheet",
            _=>"ppt/slides/slide"
        };
        foreach(var entry in zip.Entries)
        {
            if(entry.Length>long.MaxValue-uncompressed)
                return "Package size metadata is too large to summarize.";
            uncompressed+=entry.Length;
            var name=entry.FullName;
            if(name.StartsWith(prefix+"media/",StringComparison.OrdinalIgnoreCase))
                media++;
            if(name.StartsWith(prefix+"embeddings/",StringComparison.OrdinalIgnoreCase))
                embedded++;
            if(name.StartsWith(unitFolder,StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)
                && !name.Contains("/_rels/",StringComparison.OrdinalIgnoreCase))
            {
                var suffix=name.Substring(unitFolder.Length,
                    name.Length-unitFolder.Length-4);
                if(int.TryParse(suffix,out int number)&&number>0)units++;
            }
        }
        // Worksheet and slide part counts are structural, not a claim
        // about visible tabs or slides in document relationship ordering.
        string unitLabel=ext switch{
            ".docx"=>"header XML parts", ".xlsx"=>"worksheet XML parts",
            _=>"slide XML parts"
        };
        return section+" package · metadata only"+Environment.NewLine+
            zip.Entries.Count+" package parts · "+
            uncompressed.ToString("N0")+" declared unpacked bytes"+
            Environment.NewLine+units+" "+unitLabel+
            " · "+media+" media parts · "+embedded+" embedded parts"+
            Environment.NewLine+
            "No document text, cells, slide contents or attachments were opened.";
    }

    private static string Wave(FileInfo info)
    {
        if(info.Length<44)return "WAV header is incomplete.";
        using var stream=new FileStream(info.FullName,FileMode.Open,
            FileAccess.Read,FileShare.ReadWrite,4096,FileOptions.SequentialScan);
        using var r=new BinaryReader(stream,Encoding.ASCII,true);
        string FourCc()=>Encoding.ASCII.GetString(r.ReadBytes(4));
        if(FourCc()!="RIFF"){return "Not a standard RIFF WAV file.";}
        r.ReadUInt32();
        if(FourCc()!="WAVE")return "RIFF file is not WAVE audio.";
        long scanLimit=Math.Min(stream.Length,64*1024);
        int? format=null,channels=null,bits=null;
        uint? rate=null,bytesPerSecond=null,dataBytes=null;
        int scanned=0;
        while(stream.Position+8<=scanLimit && scanned++<64)
        {
            string kind=FourCc();
            uint size=r.ReadUInt32();
            long start=stream.Position;
            // Never allocate or decode WAV payload, or seek beyond file.
            if((long)size>stream.Length-start)
                return "WAV chunks extend beyond file boundaries.";
            if(kind=="fmt " && size>=16 && start+16<=scanLimit)
            {
                format=r.ReadUInt16();
                channels=r.ReadUInt16();
                rate=r.ReadUInt32();
                bytesPerSecond=r.ReadUInt32();
                r.ReadUInt16();
                bits=r.ReadUInt16();
            }
            if(kind=="data")
            {
                dataBytes=size;
                break;
            }
            long next=start+(long)size+(size%2);
            if(next>stream.Length || next>scanLimit)break;
            stream.Position=next;
        }
        if(format==null||channels==null||rate==null)
            return "WAV metadata unavailable within the bounded header scan.";
        string duration=dataBytes.HasValue && bytesPerSecond>0?
            " · about "+TimeSpan.FromSeconds(
                (double)dataBytes.Value/bytesPerSecond.Value)
                .ToString(@"hh\:mm\:ss")+" duration":"";
        return "WAV audio · header metadata only"+Environment.NewLine+
            channels+" channel(s) · "+rate+" Hz · "+bits+" bits"+
            Environment.NewLine+"Codec format "+format+duration+
            Environment.NewLine+"No audio decoded or played.";
    }

    internal static FrameworkElement Create(string path)
    {
        string text;
        try{text=Describe(path);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or NotSupportedException
            or System.Security.SecurityException)
        {
            text="Read-only metadata preview unavailable ("+
                e.GetType().Name+").";
        }
        var box=new StackPanel{Margin=new Thickness(2,4,2,4)};
        box.Children.Add(new TextBlock{Text="File details · read-only",
            Foreground=Brushes.LightSteelBlue,FontSize=12,
            FontWeight=FontWeights.SemiBold});
        box.Children.Add(new TextBox{Text=text,IsReadOnly=true,
            FontSize=11,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Height=180,Width=300,
            Background=new SolidColorBrush(Color.FromRgb(24,32,55)),
            Foreground=Brushes.White,Padding=new Thickness(6),
            BorderThickness=new Thickness(0)});
        return box;
    }
}
