using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// R256: native Windows drag selection is made while still hovering, BEFORE
// mouse release. A post-drop prompt cannot authorize Copy/ZIP afterwards.
internal static class DeskSpaceFolderPreDropV256
{
    internal enum Choice { None,Zip,Copy }

    internal static Choice Hover(bool overZip,bool overCopy)
    {
        if(overZip==overCopy)return Choice.None;
        return overZip?Choice.Zip:Choice.Copy;
    }

    internal static bool Authorized(Choice selected,
        IReadOnlyList<string>? previewed,IReadOnlyList<string>? dropped)
    {
        if(selected==Choice.None || previewed==null || dropped==null ||
           previewed.Count==0 || previewed.Count>128 ||
           previewed.Count!=dropped.Count)return false;
        for(int i=0;i<previewed.Count;i++)
            if(string.IsNullOrWhiteSpace(previewed[i]) ||
               !string.Equals(previewed[i],dropped[i],
                   StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }
}
