namespace DeskSpace;

// R226: compact the Deck title to meaningful counts. In the user's screenshot,
// "0 held · 0 new" occupied width despite there being no such content.
internal static class DeskSpaceDeckHeaderV226
{
    internal static string Format(bool copying,int apps,int held,int recent)
    {
        if(copying)return "Copying…";
        apps=System.Math.Max(0,apps);
        held=System.Math.Max(0,held);
        recent=System.Math.Max(0,recent);
        var text="Deck";
        if(apps>0)text+=" · "+apps+(apps==1?" app":" apps");
        if(held>0)text+=" · "+held+" held";
        if(recent>0)text+=" · "+recent+" new";
        return text;
    }
}
