using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeskSpace;

public sealed class DeskSpaceActionStep
{
    public string Action { get; set; } = "None";
    public string Target { get; set; } = "";
    public string Label { get; set; } = "";
}

internal static class DeskSpaceAutomationV84
{
    internal static readonly string[] Actions={"Open application","Open file","Open URL"};
    internal static readonly string[] Conditions={
        "None","File exists","Application running","Network available"
    };
    internal static bool IsValid(DeskSpaceActionStep step)
        =>step!=null && DeskSpaceDueShelfV74.IsAllowedAction(step.Action,step.Target) &&
          step.Action!="None";

    internal static bool ValidCondition(string condition,string target)
    {
        switch(condition)
        {
            case "None":
            case "Network available":return true;
            case "File exists":return !string.IsNullOrWhiteSpace(target) &&
                Path.IsPathFullyQualified(target) && target.IndexOfAny(
                    Path.GetInvalidPathChars())<0;
            case "Application running":
                return !string.IsNullOrWhiteSpace(target) &&
                    target.Length<=96 && target.All(c=>char.IsLetterOrDigit(c) ||
                        c is '_' or '-' or '.') &&
                    !target.Contains("..",StringComparison.Ordinal);
            default:return false;
        }
    }

    internal static bool ConditionReady(ReminderItem item)
    {
        string condition=item.TriggerCondition??"None";
        string target=item.TriggerTarget??"";
        if(!ValidCondition(condition,target))return false;
        try
        {
            return condition switch{
                "File exists"=>File.Exists(target),
                "Application running"=>IsRunning(target),
                "Network available"=>NetworkInterface.GetIsNetworkAvailable(),
                _=>true
            };
        }
        catch(Exception error) when(error is IOException or
            UnauthorizedAccessException or ArgumentException)
        {Debug.WriteLine("DeskSpace condition: "+error);return false;}
    }

    private static bool IsRunning(string target)
    {
        string name=Path.GetFileNameWithoutExtension(target);
        var processes=Process.GetProcessesByName(name);
        try{return processes.Length>0;}
        finally{foreach(var p in processes)p.Dispose();}
    }

    internal static List<DeskSpaceActionStep> CopySteps(IEnumerable<DeskSpaceActionStep>? steps)
        =>steps?.Where(x=>x!=null).Take(8).Select(x=>
            new DeskSpaceActionStep{Action=x.Action,Target=x.Target,
                Label=x.Label}).ToList() ?? new List<DeskSpaceActionStep>();

    internal static bool ValidPlan(ReminderItem item,out string problem)
    {
        problem="";
        if(!ValidCondition(item.TriggerCondition??"None",item.TriggerTarget??""))
        {problem="Invalid trigger condition";return false;}
        // Validate the original persisted list before the eight-step editor cap.
        // Never silently drop a ninth action (or a null entry) from a workflow.
        var source=item.AutomationSteps;
        if(source!=null && (source.Count>8 || source.Any(x=>x==null)))
        {problem="A workflow may contain at most 8 valid additional actions";return false;}
        var steps=CopySteps(source);
        if(steps.Any(x=>!IsValid(x)))
        {problem="An additional step has an invalid target";return false;}
        if(!DeskSpaceDueShelfV74.IsAllowedAction(item.AlertAction,item.AlertTarget))
        {problem="The primary action is invalid";return false;}
        return true;
    }

    // Executing apps/files/web pages is restricted to the validated actions.
    // Validation of ALL steps occurs before any step is launched.
    // The scheduler persists the due/claimed state before calling this method.
    internal static Task Run(ReminderItem original)
    {
        if(!ValidPlan(original,out var problem))
        {
            DeskSpaceReminderActionHistoryV83.Record(original,"Failed",problem);
            return Task.CompletedTask;
        }
        var batch=new List<DeskSpaceActionStep>();
        if(original.AlertAction!="None")batch.Add(new DeskSpaceActionStep{
            Action=original.AlertAction,Target=original.AlertTarget});
        batch.AddRange(CopySteps(original.AutomationSteps));
        var snapshot=new ReminderItem{Id=original.Id,Title=original.Title,
            AlertAction=original.AlertAction,AlertTarget=original.AlertTarget};
        return Task.Run(async()=>{
            for(int i=0;i<batch.Count;i++)
            {
                var step=batch[i];
                try
                {
                    if(step.Action=="Open URL")
                    {
                        if(!DeskSpaceReminderBrowserV80.TryUrl(step.Target,out var url))
                            throw new InvalidOperationException("Invalid URL");
                        if(i==0 && snapshot.AlertAction=="Open URL")
                            DeskSpaceReminderBrowserV80.LaunchAtDue(snapshot);
                        else Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
                    }
                    else if(Process.Start(new ProcessStartInfo(step.Target){
                        UseShellExecute=true})==null)
                        throw new InvalidOperationException("Windows did not launch the target");
                    DeskSpaceReminderActionHistoryV83.Record(snapshot,"Launched",
                        $"Step {i+1}/{batch.Count} {step.Action} · "+
                        (step.Action=="Open URL"?
                            new Uri(step.Target).Host:Path.GetFileName(step.Target)));
                }
                catch(Exception error)
                {
                    DeskSpaceReminderActionHistoryV83.Record(snapshot,"Failed",
                        $"Step {i+1} {error.GetType().Name}");
                    return; // No later steps if prior launch fails
                }
                if(i+1<batch.Count)await Task.Delay(400);
            }
        });
    }
}

internal sealed class DeskSpaceWorkflowTemplateV84
{
    public string Name { get; set; } = "";
    public string Action { get; set; } = "None";
    public string Target { get; set; } = "";
    public List<DeskSpaceActionStep> Additional { get; set; } = new();
    public string Condition { get; set; } = "None";
    public string ConditionTarget { get; set; } = "";
}

internal static class DeskSpaceWorkflowTemplatesV84
{
    private static readonly object Lock=new();
    internal static string Filename=>
        Path.Combine(WorkspaceStore.Root,"automation-templates.json");
    internal static IReadOnlyList<DeskSpaceWorkflowTemplateV84> Load()
    {
        lock(Lock)
        {
            try
            {
                if(!File.Exists(Filename))return Array.Empty<DeskSpaceWorkflowTemplateV84>();
                return JsonSerializer.Deserialize<List<DeskSpaceWorkflowTemplateV84>>(
                    File.ReadAllText(Filename))?.Take(30).ToArray() ??
                    Array.Empty<DeskSpaceWorkflowTemplateV84>();
            }
            catch(Exception ex) when(ex is IOException or JsonException or
                UnauthorizedAccessException)
            {Debug.WriteLine("Template load: "+ex);return Array.Empty<DeskSpaceWorkflowTemplateV84>();}
        }
    }
    internal static void Save(DeskSpaceWorkflowTemplateV84 item)
    {
        if(string.IsNullOrWhiteSpace(item.Name)||item.Name.Length>64)
            throw new ArgumentException("Template name must be 1 to 64 characters");
        if(!DeskSpaceAutomationV84.ValidCondition(item.Condition,item.ConditionTarget) ||
           !DeskSpaceDueShelfV74.IsAllowedAction(item.Action,item.Target) ||
           item.Additional==null || item.Additional.Count>8 ||
           item.Additional.Any(x=>!DeskSpaceAutomationV84.IsValid(x)))
            throw new ArgumentException("Invalid template");
        lock(Lock)
        {
            var items=Load().ToList();
            items.RemoveAll(x=>x.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase));
            items.Insert(0,item);
            if(items.Count>30)items.RemoveRange(30,items.Count-30);
            Directory.CreateDirectory(WorkspaceStore.Root);
            var temp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,JsonSerializer.Serialize(items));
                File.Move(temp,Filename,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
