using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DeskSpace;

// File-name facets for the ONE shared Search Index. Filters never open file
// contents, bypass Windows permissions or trigger an additional disk crawl.
// Supported: ext:pdf,docx type:folder|file|image|document|sheet|archive|audio|video
// in:"D:\Client Files" after:2026-01-01 before:2026-12-31 min:10kb max:5mb
internal static class DeskSpaceSearchFiltersV326
{
    private static readonly Regex Tokens=new(
        @"(?<!\S)(?<key>ext|type|in|after|before|min|max):(?:""(?<quoted>[^""]+)""|(?<unquoted>\S+))",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal sealed class Criteria
    {
        internal string Term="";
        internal HashSet<string>? Extensions;
        internal string? Type;
        internal string? Directory;
        internal DateTime? AfterUtc;
        internal DateTime? BeforeExclusiveUtc;
        internal long? MinBytes;
        internal long? MaxBytes;
        internal bool HasFileFilters => Extensions!=null || Type!=null ||
            Directory!=null || AfterUtc.HasValue || BeforeExclusiveUtc.HasValue ||
            MinBytes.HasValue || MaxBytes.HasValue;

        internal bool Matches(SearchHit hit)
        {
            if(!HasFileFilters)return true;
            if(!string.Equals(hit.Category,"Files",StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(hit.OpenTarget))return false;
            try
            {
                string path=hit.OpenTarget;
                bool folder=System.IO.Directory.Exists(path);
                bool file=!folder && File.Exists(path);
                if(!folder && !file)return false;
                if(Directory!=null)
                {
                    string normalized=path.Replace('/','\\');
                    string needed=Directory.Replace('/','\\');
                    if(needed.Length==2 && char.IsLetter(needed[0]) && needed[1]==':')
                        needed+="\\";
                    bool contained=needed.Contains('\\')
                        ?normalized.StartsWith(needed,StringComparison.OrdinalIgnoreCase)
                        :normalized.Split('\\',StringSplitOptions.RemoveEmptyEntries)
                            .Any(segment=>segment.Contains(needed,StringComparison.OrdinalIgnoreCase));
                    if(!contained)return false;
                }
                if(Extensions!=null &&
                    (folder||!Extensions.Contains(Path.GetExtension(path))))return false;
                if(Type!=null&&!TypeMatches(Type,folder,path))return false;
                if(MinBytes.HasValue||MaxBytes.HasValue)
                {
                    if(!file)return false;
                    long bytes=new FileInfo(path).Length;
                    if(MinBytes.HasValue&&bytes<MinBytes.Value)return false;
                    if(MaxBytes.HasValue&&bytes>MaxBytes.Value)return false;
                }
                if(AfterUtc.HasValue||BeforeExclusiveUtc.HasValue)
                {
                    DateTime changed=folder
                        ?System.IO.Directory.GetLastWriteTimeUtc(path)
                        :File.GetLastWriteTimeUtc(path);
                    if(AfterUtc.HasValue&&changed<AfterUtc.Value)return false;
                    if(BeforeExclusiveUtc.HasValue&&changed>=BeforeExclusiveUtc.Value)return false;
                }
                return true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException
                or System.Security.SecurityException){return false;}
        }
    }

    internal static Criteria Parse(string? input)
    {
        string source=(input??"").Trim();
        var result=new Criteria{Term=source};
        if(source.Length==0)return result;
        result.Term=Tokens.Replace(source,match=>
        {
            string key=match.Groups["key"].Value.ToLowerInvariant();
            string value=match.Groups["quoted"].Success?
                match.Groups["quoted"].Value:match.Groups["unquoted"].Value;
            if(value.Length==0)return match.Value;
            switch(key)
            {
                case "ext":
                    var list=value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)
                        .Select(x=>x.StartsWith('.')?x:"."+x)
                        .ToArray();
                    if(list.Length==0 || list.Any(x=>x.Length<2||
                        x.Skip(1).Any(c=>!char.IsLetterOrDigit(c))))return match.Value;
                    result.Extensions=new HashSet<string>(list,StringComparer.OrdinalIgnoreCase);
                    return " ";
                case "type":
                    if(!new[]{"file","files","folder","folders","image","images",
                        "document","documents","sheet","spreadsheet","archive","archives",
                        "audio","video"}.Contains(value,StringComparer.OrdinalIgnoreCase))
                        return match.Value;
                    result.Type=value.ToLowerInvariant();return " ";
                case "in":
                    if(value.IndexOfAny(new[]{'*','?','\0'})>=0)return match.Value;
                    result.Directory=value;return " ";
                case "after":
                    if(!DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var after))
                        return match.Value;
                    result.AfterUtc=after;return " ";
                case "before":
                    if(!DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var before))
                        return match.Value;
                    if(before.Date>=DateTime.MaxValue.Date)return match.Value;
                    result.BeforeExclusiveUtc=before.Date.AddDays(1);return " ";
                case "min":
                    if(!TryBytes(value,out long min))return match.Value;
                    result.MinBytes=min;return " ";
                case "max":
                    if(!TryBytes(value,out long max))return match.Value;
                    result.MaxBytes=max;return " ";
            }
            return match.Value;
        }).Trim();
        result.Term=Regex.Replace(result.Term,@"\s+"," ");
        return result;
    }

    private static bool TryBytes(string text,out long bytes)
    {
        bytes=0;
        var m=Regex.Match(text,@"^(?<n>\d+(?:\.\d+)?)(?<unit>b|kb|mb|gb|tb)?$",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        if(!m.Success || !decimal.TryParse(m.Groups["n"].Value,
            NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal n))
            return false;
        decimal factor=m.Groups["unit"].Value.ToLowerInvariant() switch
        {"kb"=>1024m,"mb"=>1024m*1024m,"gb"=>1024m*1024m*1024m,
         "tb"=>1024m*1024m*1024m*1024m,_=>1m};
        decimal product=n*factor;
        if(product>long.MaxValue)return false;
        bytes=(long)Math.Ceiling(product);
        return true;
    }

    private static bool TypeMatches(string type,bool folder,string path)
    {
        if(type is "folder" or "folders")return folder;
        if(type is "file" or "files")return !folder;
        if(folder)return false;
        string ext=Path.GetExtension(path).ToLowerInvariant();
        return type switch
        {
            "image" or "images"=>new[]{".png",".jpg",".jpeg",".webp",".gif",".tif",".tiff",".bmp",".heic"}.Contains(ext),
            "document" or "documents"=>new[]{".pdf",".doc",".docx",".rtf",".odt",".txt",".md"}.Contains(ext),
            "sheet" or "spreadsheet"=>new[]{".xls",".xlsx",".xlsm",".csv",".tsv",".ods"}.Contains(ext),
            "archive" or "archives"=>new[]{".zip",".7z",".rar",".tar",".gz",".bz2"}.Contains(ext),
            "audio"=>new[]{".mp3",".wav",".m4a",".flac",".ogg"}.Contains(ext),
            "video"=>new[]{".mp4",".mkv",".avi",".mov",".webm"}.Contains(ext),
            _=>false
        };
    }
}
