using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace DeskSpace;

// R342: one context-action policy for both donut results and Workspace Search.
// Paths are checked on every menu opening (not only when indexed), so renamed
// or disconnected files are not presented as actionable. No destructive action.
internal static class DeskSpaceUnifiedResultMenuV342
{
    internal sealed record Choice(string Id,string Label);

    internal static IReadOnlyList<Choice> Available(SearchHit? hit,bool pinMode=false)
    {
        if(hit==null)return Array.Empty<Choice>();
        bool app=hit.Category.Equals("Applications",StringComparison.OrdinalIgnoreCase);
        bool fileCategory=hit.Category.Equals("Files",StringComparison.OrdinalIgnoreCase);
        string path=hit.OpenTarget??"";
        bool existsFile=false,existsFolder=false;
        try
        {
            if(fileCategory || app)
            {
                existsFile=File.Exists(path);
                existsFolder=fileCategory && Directory.Exists(path);
            }
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException){}
        if(pinMode)
            return app && existsFile?
                new[]{new Choice("pin","Pin application")}:Array.Empty<Choice>();
        var choices=new List<Choice>();
        if(existsFile || existsFolder)
        {
            choices.Add(new Choice("open","Open"));
            choices.Add(new Choice("folder","Show in Explorer"));
            choices.Add(new Choice("path","Copy path"));
            if(existsFile)choices.Add(new Choice("file","Copy file"));
            if(app && existsFile)choices.Add(new Choice("pin","Pin application"));
        }
        else if(hit.Category is "Notes" or "Reminders" or "Calendar" or "Information")
        {
            // Explicit copy is distinct from launching or treating text as a
            // file path. Existing host action requires user consent for
            // Information; this menu only surfaces the choice.
            choices.Add(new Choice("open","Copy selected entry"));
        }
        return choices;
    }

    internal static ContextMenu Create(SearchHit hit,Action<string,SearchHit> execute,
        bool pinMode=false)
    {
        var menu=new ContextMenu();
        foreach(var choice in Available(hit,pinMode))
        {
            var code=choice.Id;
            var item=new MenuItem{Header=choice.Label};
            item.Click+=(_,_)=>{
                // Changes between preview and click must not bypass the
                // existence/permissions checks used at menu creation.
                if(Available(hit,pinMode).Any(x=>x.Id==code))
                    execute(code,hit);
            };
            menu.Items.Add(item);
        }
        return menu;
    }
}
