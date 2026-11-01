using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// R262: no global recent-file polling, background watchers, file-content
// reads, or automatic launch. Applies only to an explicitly selected pinned
// application's Windows Recent shortcuts, with OS handler revalidation.
internal static class DeskSpacePinnedDocumentChoiceV262
{
    internal sealed record Candidate(string Path,DateTime LastUsed,string Handler);
    internal const int MaxResults=10;
    private static readonly HashSet<string> AllowedExtensions=new(
        new []{".docx",".doc",".xlsx",".xls",".xlsm",".csv",".pdf",
            ".pptx",".ppt",".txt",".md",".rtf",".odt",".ods",
            ".jpg",".jpeg",".png",".webp",".json",".xml",".log"},
        StringComparer.OrdinalIgnoreCase);

    internal static bool SafeDocument(string? path)
    {
        if(string.IsNullOrWhiteSpace(path) || path.Length>1024 ||
           !Path.IsPathFullyQualified(path))return false;
        try
        {
            if(!AllowedExtensions.Contains(System.IO.Path.GetExtension(path)))
                return false;
            var info=new FileInfo(path);
            return info.Exists && info.Length>0 &&
                (info.Attributes&FileAttributes.ReparsePoint)==0;
        }
        catch(Exception ex)when(ex is ArgumentException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {return false;}
    }

    internal static IReadOnlyList<Candidate> Filter(
        string pinnedExe,IEnumerable<Candidate> candidates)
    {
        if(string.IsNullOrWhiteSpace(pinnedExe) || candidates==null)
            return Array.Empty<Candidate>();
        return candidates.Where(c=>
                SafeDocument(c.Path) &&
                DeskSpacePinnedRecentV122.SameExecutableV122(
                    c.Handler,pinnedExe))
            .GroupBy(c=>c.Path,StringComparer.OrdinalIgnoreCase)
            .Select(g=>g.OrderByDescending(c=>c.LastUsed).First())
            .OrderByDescending(c=>c.LastUsed)
            .ThenBy(c=>System.IO.Path.GetFileName(c.Path),
                StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults).ToArray();
    }
}
