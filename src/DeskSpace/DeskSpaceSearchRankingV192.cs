using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// R192: bounded relevance-ranked collector, shared by Windows Search,
// installed application shortcuts and the all-drive fallback. It never
// reads file contents or follows links. Do not let early low-quality matches
// fill a global 400-row buffer before better results have been seen.
internal static class DeskSpaceSearchRankingV192
{
    internal sealed class Collector
    {
        private readonly string query;
        private readonly Dictionary<string,SearchHit> results=
            new(StringComparer.OrdinalIgnoreCase);

        internal Collector(string query)
        {
            this.query=query.Trim();
        }

        private static string Key(SearchHit hit)
        {
            string target=hit.OpenTarget??"";
            if(hit.Category.Equals("Applications",StringComparison.OrdinalIgnoreCase))
                return "app:"+hit.Name.Trim();
            if(string.IsNullOrWhiteSpace(target))
                return hit.Category+":"+hit.Name.Trim();
            try
            {
                if(Path.IsPathFullyQualified(target))
                    target=Path.GetFullPath(target)
                        .TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            }
            catch(Exception e) when(e is ArgumentException or NotSupportedException
                or PathTooLongException){}
            // Files from Windows Search and the fallback have the SAME key.
            // URIs/notes/settings retain their protocol, never interpreted as paths.
            return target;
        }

        private static int CategoryOrder(SearchHit hit)=>
            hit.Category switch
            {
                "Applications"=>0,"Settings"=>1,"Notes"=>2,
                "Reminders"=>3,"Calendar"=>4,"Information"=>5,
                "Files"=>6,_=>7
            };

        internal static int Quality(SearchHit hit,string term)
        {
            string name=hit.Name??"";
            if(name.Equals(term,StringComparison.OrdinalIgnoreCase))return 0;
            if(name.StartsWith(term,StringComparison.OrdinalIgnoreCase))return 1;
            int location=name.IndexOf(term,StringComparison.OrdinalIgnoreCase);
            if(location>0 && !char.IsLetterOrDigit(name[location-1]))return 2;
            if(location>=0)return 3;
            return (hit.OpenTarget??"").Contains(term,StringComparison.OrdinalIgnoreCase)?4:5;
        }

        private static int Limit(string category)=>category switch
        {
            "Files"=>170,
            "Applications"=>64,
            "Settings"=>32,
            "Notes" or "Reminders" or "Calendar" or "Information"=>36,
            _=>32
        };

        private int Compare(SearchHit first,SearchHit second)
        {
            int quality=Quality(first,query).CompareTo(Quality(second,query));
            if(quality!=0)return quality;
            int category=CategoryOrder(first).CompareTo(CategoryOrder(second));
            if(category!=0)return category;
            // Prefer the named Start menu launch shortcut when the same app
            // has both an App Paths registration and a Start menu shortcut.
            if(first.Category=="Applications" && second.Category=="Applications")
            {
                int a=(first.OpenTarget??"").EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)?0:1;
                int b=(second.OpenTarget??"").EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)?0:1;
                if(a!=b)return a.CompareTo(b);
            }
            int name=StringComparer.OrdinalIgnoreCase.Compare(first.Name,second.Name);
            return name!=0?name:StringComparer.OrdinalIgnoreCase.Compare(
                first.OpenTarget,second.OpenTarget);
        }

        // Returns true only when a new path is admitted, preserving the
        // existing 48-per-drive fallback quota and all-volume visibility.
        internal bool Add(SearchHit hit)
        {
            if(string.IsNullOrWhiteSpace(hit.Name))return false;
            string key=Key(hit);
            if(results.TryGetValue(key,out var previous))
            {
                if(Compare(hit,previous)<0)results[key]=hit;
                return false;
            }
            var categoryHits=results.Where(entry=>
                entry.Value.Category.Equals(hit.Category,StringComparison.OrdinalIgnoreCase)).ToList();
            int limit=Limit(hit.Category);
            if(categoryHits.Count>=limit)
            {
                var worst=categoryHits.OrderByDescending(e=>e.Value,
                    Comparer<SearchHit>.Create(Compare)).First();
                if(Compare(hit,worst.Value)>=0)return false;
                results.Remove(worst.Key);
            }
            results.Add(key,hit);
            return true;
        }

        internal List<SearchHit> Finish(int max=160)
        {
            return results.Values.OrderBy(hit=>hit,
                Comparer<SearchHit>.Create(Compare))
                .Take(Math.Clamp(max,1,160)).ToList();
        }

        internal int Count=>results.Count;
    }
}
