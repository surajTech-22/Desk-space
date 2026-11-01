using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// One editor is reused by the floating widget and main Workspace.
// The original primary-action picker remains unchanged; this only adds
// optional ordered steps, a condition, and reusable action templates.
internal sealed class DeskSpaceAutomationEditorV84
{
    private readonly Func<(string Action,string Target)> primary;
    private readonly Action<string,string> assignPrimary;
    private readonly List<DeskSpaceActionStep> steps=new();
    private readonly StackPanel list=new();
    // A single disclosure control is shared by the floating reminder panel
    // and the main Workspace reminder editor. Collapsing never resets data.
    private readonly Expander advanced=new(){
        IsExpanded=false,FontSize=12,Foreground=Brushes.LightSteelBlue,
        HorizontalContentAlignment=HorizontalAlignment.Stretch,
        ToolTip="Expand to edit additional actions, conditions and templates"
    };
    private readonly ComboBox action=new(){Width=143,Height=28,
        ItemsSource=DeskSpaceAutomationV84.Actions,SelectedIndex=0};
    private readonly TextBox target=new(){Width=245,Height=28,
        Margin=new Thickness(3)};
    private readonly StackPanel search=new();
    private string selectedTarget="";
    private readonly ComboBox condition=new(){Width=158,Height=28,
        ItemsSource=DeskSpaceAutomationV84.Conditions,SelectedIndex=0};
    private readonly TextBox conditionTarget=new(){Width=208,Height=28,
        Margin=new Thickness(4)};
    private readonly ComboBox templates=new(){Width=170,Height=28};
    private readonly TextBox templateName=new(){Width=146,Height=28};
    private readonly TextBlock notice=new(){FontSize=11,
        Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap};

    internal DeskSpaceAutomationEditorV84(
        Func<(string Action,string Target)> primary,
        Action<string,string> assignPrimary)
    {
        this.primary=primary;this.assignPrimary=assignPrimary;
        action.SelectionChanged+=(_,_)=>RefreshTargetPicker();
        condition.SelectionChanged+=(_,_)=>RefreshCondition();
    }
    internal string ConditionMode=>condition.SelectedItem?.ToString()??"None";
    internal string ConditionTarget=>conditionTarget.Text.Trim();
    internal List<DeskSpaceActionStep> Steps=>
        DeskSpaceAutomationV84.CopySteps(steps);

    private void RefreshAdvancedHeader()
    {
        int count=steps.Count;
        bool conditional=ConditionMode!="None";
        advanced.Header=count==0 && !conditional?
            "Advanced options (optional)":
            "Advanced options · "+count+" step"+(count==1?"":"s")+
                (conditional?" · condition":"");
    }
    private static TextBlock Label(string title)=>new(){
        Text=title,FontSize=12,Foreground=Brushes.LightSteelBlue,
        Margin=new Thickness(2,6,2,3)
    };
    private static Button Button(string title,Action clicked)
    {
        var button=new Button{Content=title,Padding=new Thickness(8,5,8,5),
            Margin=new Thickness(3),MinHeight=27};
        button.Click+=(_,_)=>clicked();
        return button;
    }

    internal FrameworkElement Create()
    {
        var root=new StackPanel{Margin=new Thickness(2)};
        root.Children.Add(Label("Ordered extra steps"));
        var row=new WrapPanel();
        row.Children.Add(action);row.Children.Add(target);
        var browse=Button("Browse",()=>{
            string mode=action.SelectedItem?.ToString()??"Open file";
            if(mode=="Open URL")return;
            var dlg=new Microsoft.Win32.OpenFileDialog{
                CheckFileExists=true,Filter=mode=="Open application"?
                    "Programs (*.exe;*.lnk)|*.exe;*.lnk":
                    "All files (*.*)|*.*"};
            if(dlg.ShowDialog()==true)
            {
                selectedTarget=dlg.FileName;
                target.Text=Path.GetFileName(dlg.FileName);
            }
        });
        browse.ToolTip="Select any installed app shortcut or file";
        row.Children.Add(browse);
        root.Children.Add(row);
        root.Children.Add(search);
        root.Children.Add(Button("+ Add action step",()=>{
            string mode=action.SelectedItem?.ToString()??"Open file";
            string value=mode=="Open URL"?target.Text.Trim():selectedTarget;
            if(mode=="Open URL" &&
                DeskSpaceReminderBrowserV80.TryUrl(value,out var url))
                value=url;
            var newStep=new DeskSpaceActionStep{
                Action=mode,Target=value,Label=mode=="Open URL"?
                    value:Path.GetFileName(value)
            };
            if(steps.Count>=8 || !DeskSpaceAutomationV84.IsValid(newStep))
            {notice.Text="Choose a valid app/file/URL; maximum 8 extra actions.";return;}
            steps.Add(newStep);
            selectedTarget="";target.Clear();
            RenderSteps();
            notice.Text="The actions execute in order, after the primary action.";
        }));
        root.Children.Add(list);
        root.Children.Add(Label("Run only when (optional condition)"));
        var conditionRow=new WrapPanel();
        conditionRow.Children.Add(condition);
        conditionRow.Children.Add(conditionTarget);
        conditionRow.Children.Add(Button("Test condition",()=>{
            var fake=new ReminderItem{TriggerCondition=ConditionMode,
                TriggerTarget=ConditionTarget};
            notice.Text=DeskSpaceAutomationV84.ValidCondition(
                    ConditionMode,ConditionTarget)?
                (DeskSpaceAutomationV84.ConditionReady(fake)?
                    "Condition currently satisfied":"Condition currently not satisfied"):
                "Invalid condition target.";
        }));
        root.Children.Add(conditionRow);
        var templateRow=new WrapPanel();
        templateName.Text="My routine";
        templateRow.Children.Add(templateName);
        templateRow.Children.Add(Button("Save template",SaveTemplate));
        templateRow.Children.Add(templates);
        templateRow.Children.Add(Button("Load",LoadTemplate));
        root.Children.Add(Label("Reusable workflow templates"));
        root.Children.Add(templateRow);
        root.Children.Add(notice);
        RefreshTargetPicker();RefreshCondition();RefreshTemplates();RenderSteps();
        advanced.Content=root;
        return new Border{
            BorderThickness=new Thickness(0,1,0,0),
            BorderBrush=new SolidColorBrush(Color.FromRgb(100,109,139)),
            Child=advanced,Margin=new Thickness(0,6,0,5)};
    }
    private void RefreshTargetPicker()
    {
        if(search==null)return;
        search.Children.Clear();
        string mode=action.SelectedItem?.ToString()??"Open application";
        target.Visibility=mode=="Open URL"?Visibility.Visible:
            Visibility.Collapsed;
        target.ToolTip=mode=="Open URL"?"Paste an http(s) website":"";
        selectedTarget="";
        if(mode is "Open application" or "Open file")
            search.Children.Add(DeskSpaceReminderPickerV81.Create(mode,
                (path,label)=>{
                    selectedTarget=path;
                    target.Text=label;notice.Text=label+" selected";
                }));
    }
    private void RefreshCondition()
    {
        string mode=ConditionMode;
        conditionTarget.Visibility=mode is "File exists" or
            "Application running"?Visibility.Visible:Visibility.Collapsed;
        conditionTarget.ToolTip=mode=="File exists"?
            "Full path to a file that must exist":
            "Process name such as EXCEL or chrome";
        RefreshAdvancedHeader();
    }
    private void RefreshTemplates()
    {
        templates.ItemsSource=DeskSpaceWorkflowTemplatesV84.Load()
            .Select(x=>x.Name).ToArray();
    }
    private void SaveTemplate()
    {
        var (mode,path)=primary();
        var item=new DeskSpaceWorkflowTemplateV84{
            Name=templateName.Text.Trim(),Action=mode,Target=path,
            Additional=Steps,Condition=ConditionMode,
            ConditionTarget=ConditionTarget};
        var existing=DeskSpaceWorkflowTemplatesV84.Load()
            .Any(x=>x.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase));
        if(existing && MessageBox.Show("Replace saved template '"+item.Name+"'?",
            "DeskSpace templates",MessageBoxButton.YesNo,
            MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try
        {
            DeskSpaceWorkflowTemplatesV84.Save(item);
            RefreshTemplates();templates.SelectedItem=item.Name;
            notice.Text="Template saved. No action has been executed.";
        }
        catch(Exception error) when(error is ArgumentException or IOException or
            UnauthorizedAccessException)
        {notice.Text="Could not save template: "+error.Message;}
    }
    private void LoadTemplate()
    {
        var name=templates.SelectedItem?.ToString();
        var item=DeskSpaceWorkflowTemplatesV84.Load()
            .FirstOrDefault(x=>x.Name.Equals(name,
                StringComparison.OrdinalIgnoreCase));
        if(item==null){notice.Text="Choose a saved template.";return;}
        assignPrimary(item.Action,item.Target);
        steps.Clear();steps.AddRange(DeskSpaceAutomationV84.CopySteps(item.Additional));
        condition.SelectedItem=item.Condition;
        conditionTarget.Text=item.ConditionTarget;
        RenderSteps();
        notice.Text="Template loaded into this editor; save the reminder to apply.";
    }

    internal void Load(ReminderItem item)
    {
        steps.Clear();
        steps.AddRange(DeskSpaceAutomationV84.CopySteps(item.AutomationSteps));
        condition.SelectedItem=DeskSpaceAutomationV84.Conditions
            .Contains(item.TriggerCondition)?item.TriggerCondition:"None";
        conditionTarget.Text=item.TriggerTarget??"";
        // Existing advanced settings should be visible when opening a saved item.
        advanced.IsExpanded=steps.Count>0 || ConditionMode!="None";
        RenderSteps();
    }
    internal void Reset()
    {
        steps.Clear();condition.SelectedIndex=0;
        conditionTarget.Clear();advanced.IsExpanded=false;
        RenderSteps();notice.Text="";
    }
    private void RenderSteps()
    {
        RefreshAdvancedHeader();
        list.Children.Clear();
        for(int i=0;i<steps.Count;i++)
        {
            int index=i;
            var step=steps[i];
            var wrap=new WrapPanel{Margin=new Thickness(1,2,1,2)};
            string label=step.Action+" · "+(step.Action=="Open URL"?
                (DeskSpaceReminderBrowserV80.TryUrl(step.Target,out var url)?
                    new Uri(url).Host:step.Target):Path.GetFileName(step.Target));
            var text=new TextBlock{Text=(i+1)+". "+label,
                Width=245,TextTrimming=TextTrimming.CharacterEllipsis,
                VerticalAlignment=VerticalAlignment.Center,
                Foreground=Brushes.White,ToolTip=label};
            wrap.Children.Add(text);
            wrap.Children.Add(Button("↑",()=>{
                if(index<=0)return;
                (steps[index-1],steps[index])=(steps[index],steps[index-1]);
                RenderSteps();
            }));
            wrap.Children.Add(Button("↓",()=>{
                if(index>=steps.Count-1)return;
                (steps[index+1],steps[index])=(steps[index],steps[index+1]);
                RenderSteps();
            }));
            wrap.Children.Add(Button("Test",()=>{
                if(!DeskSpaceAutomationV84.IsValid(step))
                {notice.Text="That step has an invalid target.";return;}
                if(MessageBox.Show("Open this target now to test it?\n"+label,
                    "DeskSpace action test",MessageBoxButton.YesNo,
                    MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                try
                {
                    string target=step.Target;
                    if(step.Action=="Open URL" &&
                        DeskSpaceReminderBrowserV80.TryUrl(target,out var url))
                        target=url;
                    Process.Start(new ProcessStartInfo(target){
                        UseShellExecute=true});
                    notice.Text="Windows accepted the test launch request.";
                }
                catch(Exception error)
                {notice.Text="Test failed: "+error.Message;}
            }));
            wrap.Children.Add(Button("×",()=>{
                steps.RemoveAt(index);RenderSteps();
            }));
            list.Children.Add(wrap);
        }
    }
}
