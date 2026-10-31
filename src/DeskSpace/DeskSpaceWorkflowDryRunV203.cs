using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DeskSpace;

// R203: a deterministic, non-executing review surface for existing saved
// reminder workflows. No timers, subprocesses, network requests or file writes.
internal static class DeskSpaceWorkflowDryRunV203
{
    internal sealed record Review(bool Accepted,string Summary,
        IReadOnlyList<string> Steps);
    internal static Review Preview(DeskSpaceWorkflowTemplateV84 template)
    {
        if(template==null || string.IsNullOrWhiteSpace(template.Name) ||
           template.Name.Length>64 || template.Additional==null ||
           template.Additional.Count>8 ||
           !DeskSpaceAutomationV84.ValidCondition(
                template.Condition,template.ConditionTarget) ||
           !DeskSpaceDueShelfV74.IsAllowedAction(template.Action,template.Target) ||
           template.Additional.Any(x=>!DeskSpaceAutomationV84.IsValid(x)))
            return new Review(false,"Invalid or unsupported saved workflow",
                Array.Empty<string>());
        var actions=new List<DeskSpaceActionStep>();
        if(template.Action!="None")
            actions.Add(new DeskSpaceActionStep
            {
                Action=template.Action,Target=template.Target
            });
        actions.AddRange(template.Additional);
        var lines=actions.Select((step,index)=>
            $"{index+1}. {step.Action} · {DisplayTarget(step.Action,step.Target)}")
            .ToArray();
        string condition=template.Condition=="None"?"manual":
            template.Condition;
        return new Review(true,$"{actions.Count} approved action(s). "+
            $"Condition: {condition}. Manual review only.",lines);
    }
    internal static string DisplayTarget(string action,string? target)
    {
        if(string.IsNullOrWhiteSpace(target))return "no target";
        if(action=="Open URL")
        {
            return Uri.TryCreate(target,UriKind.Absolute,out var uri) &&
                (uri.Scheme=="https"||uri.Scheme=="http")?
                    uri.Host:"invalid URL";
        }
        string name=Path.GetFileName(target);
        if(name.Length>96)name=name[..96]+"…";
        return name;
    }
    internal static Window Create(Window owner)
    {
        var window=new Window
        {
            Title="DeskSpace workflow preview",Owner=owner,
            Width=520,Height=430,MinWidth=400,MinHeight=300,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ShowInTaskbar=false
        };
        var root=new DockPanel{Margin=new Thickness(16)};
        window.Content=root;
        var intro=new TextBlock
        {
            Text="Inspect saved reminder workflow actions. "+
            "This preview NEVER executes a step or changes a file.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)
        };
        DockPanel.SetDock(intro,Dock.Top);root.Children.Add(intro);
        var footer=new TextBlock
        {
            Text="No workflow will run from this window.",
            Margin=new Thickness(0,8,0,0),TextWrapping=TextWrapping.Wrap
        };
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var names=new ComboBox{MinHeight=34,Margin=new Thickness(0,0,0,10)};
        AutomationProperties.SetName(names,"Saved workflow template");
        DockPanel.SetDock(names,Dock.Top);root.Children.Add(names);
        var details=new ListBox{MinHeight=160};
        AutomationProperties.SetName(details,"Approved workflow steps preview");
        root.Children.Add(details);
        var templates=DeskSpaceWorkflowTemplatesV84.Load().ToArray();
        foreach(var item in templates)
            names.Items.Add(item.Name);
        names.SelectionChanged+=(_,_)=>{
            details.Items.Clear();
            int index=names.SelectedIndex;
            if(index<0||index>=templates.Length)return;
            var result=Preview(templates[index]);
            footer.Text=result.Summary;
            foreach(var step in result.Steps)
                details.Items.Add(step);
        };
        if(templates.Length>0)names.SelectedIndex=0;
        else footer.Text="No saved workflow templates found. Nothing was executed.";
        return window;
    }
    internal static void Show(Window owner)
    {
        var window=Create(owner);
        window.Show();
        window.Activate();
    }
}
