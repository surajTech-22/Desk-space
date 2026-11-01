using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// Pure result adapter: the ONE existing multi-drive index supplies the hits;
// this class never crawls disks, reads notes, executes targets or tracks users.
internal static class DeskSpaceUniversalSearchPolicyV269
{
    internal static readonly string[] Categories={
        "All","Files","Applications","Settings","Notes",
        "Reminders","Calendar","Information"
    };
    internal static IReadOnlyList<SearchHit> Filter(
        IEnumerable<SearchHit>? hits,string? category,int limit=150)
    {
        if(hits==null)return Array.Empty<SearchHit>();
        string chosen=Categories.Contains(category,StringComparer.OrdinalIgnoreCase)
            ?category!:"All";
        var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result=new List<SearchHit>();
        foreach(var hit in hits)
        {
            if(hit==null || string.IsNullOrWhiteSpace(hit.Name) ||
               string.IsNullOrWhiteSpace(hit.OpenTarget) ||
               (chosen!="All" && !string.Equals(hit.Category,chosen,
                    StringComparison.OrdinalIgnoreCase)))continue;
            // Avoid duplicate file/shortcut entries returned by two indexes.
            string key=hit.Category+"\0"+hit.OpenTarget.Trim();
            if(!known.Add(key))continue;
            result.Add(hit);
            if(result.Count>=Math.Clamp(limit,1,160))break;
        }
        return result;
    }
    internal static bool Accept(
        int capturedSequence,int activeSequence,
        bool cancelled,string? enteredTerm,string? currentTerm,
        string? selectedCategory,string? currentCategory)=>
        !cancelled && capturedSequence==activeSequence &&
        string.Equals(enteredTerm,currentTerm,StringComparison.Ordinal) &&
        string.Equals(selectedCategory,currentCategory,StringComparison.Ordinal);
}
