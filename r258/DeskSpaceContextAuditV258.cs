using System;

namespace DeskSpace;

// R258 privacy information contains only category counts from an
// explicitly started foreground-app suggestion session. No window titles,
// URLs, paths, document names or raw process names can enter this model.
internal static class DeskSpaceContextAuditV258
{
    internal static string Summary(bool listening,int events,string? category)
    {
        if(!listening)
            return "Privacy audit: Off. No listener active, no session history retained.";
        int count=Math.Clamp(events,0,10000);
        var safe=(category??"") switch
        {
            "Excel"=>"Excel",
            "Documents"=>"Documents",
            "Presentations"=>"Presentations",
            "Files"=>"Files",
            "Browser"=>"Browser",
            "Media"=>"Media",
            _=>"None"
        };
        return "Privacy audit: Listening (opt-in). Recognized app-category events: "+
            count+". Last category: "+safe+
            ". No titles, file paths, URLs, credentials or clipboard contents collected or stored.";
    }
}
