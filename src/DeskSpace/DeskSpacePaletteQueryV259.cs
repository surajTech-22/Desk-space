using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// R259: pure in-memory category-filtered Action Palette search. It never
// searches user documents, clipboard history, passwords or external services.
// All actions still require explicit user selection.
internal static class DeskSpacePaletteQueryV259
{
    internal const int MaxTokens=10;
    internal const int MaxTokenChars=64;
    internal static IReadOnlyList<DeskSpaceActionPaletteV197.Command> Filter(
        IReadOnlyList<DeskSpaceActionPaletteV197.Command> commands,string? query)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if(query?.Length>768)return Array.Empty<DeskSpaceActionPaletteV197.Command>();
        var tokens=(query??"").Split(' ',
            StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(tokens.Length>MaxTokens || tokens.Any(x=>x.Length>MaxTokenChars))
            return Array.Empty<DeskSpaceActionPaletteV197.Command>();
        string? group=null;
        var terms=new List<string>();
        foreach(var token in tokens)
        {
            if(token.StartsWith('@'))
            {
                var requested=token[1..];
                if(requested.Length==0)return Array.Empty<DeskSpaceActionPaletteV197.Command>();
                if(string.Equals(requested,"all",StringComparison.OrdinalIgnoreCase))
                {
                    if(group!=null)return Array.Empty<DeskSpaceActionPaletteV197.Command>();
                    continue;
                }
                if(!commands.Any(x=>string.Equals(x.Group,requested,
                       StringComparison.OrdinalIgnoreCase)) ||
                   (group!=null && !string.Equals(group,requested,
                       StringComparison.OrdinalIgnoreCase)))
                    return Array.Empty<DeskSpaceActionPaletteV197.Command>();
                group=requested;
            }
            else terms.Add(token);
        }
        return commands.Where(c=>
            (group==null||string.Equals(c.Group,group,
                StringComparison.OrdinalIgnoreCase)) &&
            terms.All(t=>(c.Name+" "+c.Description+" "+c.Group)
                .Contains(t,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c=>terms.Count==0?2:
                terms.Any(t=>string.Equals(c.Name,t,
                    StringComparison.OrdinalIgnoreCase))?0:
                c.Name.StartsWith(terms[0],StringComparison.OrdinalIgnoreCase)?1:2)
            .ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase)
            .ThenBy(c=>c.Id,StringComparer.Ordinal)
            .ToArray();
    }

    internal static int Navigate(int selected,int count,int delta)
    {
        if(count<=0)return -1;
        return Math.Clamp(selected<0?0:selected+delta,0,count-1);
    }
}
