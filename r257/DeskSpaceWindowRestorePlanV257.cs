using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DeskSpace;

// R257: pure, read-only preview of existing window-layout restore.
// Requires exact saved monitor identity; never silently moves a window
// to the primary monitor when the original display is disconnected.
internal static class DeskSpaceWindowRestorePlanV257
{
    internal sealed record Live(string Process,string TitleHash);
    internal sealed record Row(string Process,string Monitor,Rectangle Bounds,
        bool Ready,string Reason);
    internal sealed record Plan(int Ready,int Skipped,IReadOnlyList<Row> Rows);

    internal static Plan Build(
        IReadOnlyList<DeskSpaceWindowLayoutsV179.WindowEntry> saved,
        IReadOnlyList<Live> live,
        IReadOnlyDictionary<string,Rectangle> workingAreas)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(workingAreas);
        var rows=new List<Row>();
        var used=new HashSet<int>();
        foreach(var entry in saved.Take(32))
        {
            var matches=live.Select((x,i)=>(x,i)).Where(x=>
                string.Equals(x.x.Process,entry.Process,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.x.TitleHash,entry.TitleHash,
                    StringComparison.Ordinal)).ToArray();
            if(entry.Instance<0 || entry.Instance>=matches.Length ||
               !used.Add(entry.Instance>=0 && entry.Instance<matches.Length?
                    matches[entry.Instance].i:-1))
            {
                rows.Add(new(entry.Process,entry.Monitor,Rectangle.Empty,
                    false,"Matching running window unavailable"));
                continue;
            }
            if(!workingAreas.TryGetValue(entry.Monitor,out var area))
            {
                rows.Add(new(entry.Process,entry.Monitor,Rectangle.Empty,
                    false,"Saved monitor disconnected"));
                continue;
            }
            try
            {
                var bounds=DeskSpaceWindowLayoutsV179.RestoreBounds(entry,area);
                rows.Add(new(entry.Process,entry.Monitor,bounds,true,
                    "Ready for explicit confirmation"));
            }
            catch(ArgumentException)
            {
                rows.Add(new(entry.Process,entry.Monitor,Rectangle.Empty,
                    false,"Saved window coordinates invalid"));
            }
        }
        return new(rows.Count(x=>x.Ready),rows.Count(x=>!x.Ready),rows);
    }
}
