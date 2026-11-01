using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DeskSpace;

internal static class DeskSpaceReminderActionHistoryV83
{
    internal sealed record Entry(DateTime At,Guid ReminderId,string Title,
        string Action,string Result,string Detail);
    internal static string Filename=>Path.Combine(
        WorkspaceStore.Root,"reminder-action-history.jsonl");
    private static readonly object Gate=new();

    internal static void Record(ReminderItem reminder,string result,string detail="")
    {
        try
        {
            // Avoid logging full URLs, which may contain private query tokens.
            // A filename is enough to identify a local action without copying
            // the rest of the user's path into a second store.
            string kind=reminder.AlertAction;
            string safeTarget=kind=="Open URL" &&
                DeskSpaceReminderBrowserV80.TryUrl(reminder.AlertTarget,out var url)?
                    new Uri(url).Host:Path.GetFileName(reminder.AlertTarget??"");
            string description=string.IsNullOrEmpty(detail)?safeTarget:detail;
            var item=new Entry(DateTime.Now,reminder.Id,reminder.Title,
                kind,result,description);
            lock(Gate)
            {
                Directory.CreateDirectory(WorkspaceStore.Root);
                File.AppendAllText(Filename,JsonSerializer.Serialize(item)+
                    Environment.NewLine);
            }
        }
        catch(Exception ex){Debug.WriteLine("DeskSpace action history: "+ex);}
    }

    internal static IReadOnlyList<Entry> ReadRecent(int limit=25)
    {
        try
        {
            if(!File.Exists(Filename))return Array.Empty<Entry>();
            var results=new List<Entry>();
            foreach(var line in File.ReadLines(Filename).TakeLast(500))
            {
                try
                {
                    var item=JsonSerializer.Deserialize<Entry>(line);
                    if(item!=null)results.Add(item);
                }
                catch(JsonException){}
            }
            return results.TakeLast(Math.Clamp(limit,1,200)).Reverse().ToArray();
        }
        catch(IOException){return Array.Empty<Entry>();}
        catch(UnauthorizedAccessException){return Array.Empty<Entry>();}
    }
}
