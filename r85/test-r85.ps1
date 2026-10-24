$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$files=@{
  Model="$r/Models.cs"
  Engine="$r/DeskSpaceAutomationV84.cs"
  Editor="$r/DeskSpaceAutomationEditorV84.cs"
  Due="$r/DeskSpaceDueShelfV74.cs"
  Widget="$r/DeskSpaceInlineV36.cs"
  Main="$r/MainWindow.xaml.cs"
  Xaml="$r/MainWindow.xaml"
  Profile="$r/DeskSpaceWorkspaceProfilesV85.cs"
  Preview="$r/DeskSpaceFilePreviewV85.cs"
  Shortcut="$r/DeskSpaceCommandShortcutV85.cs"
  Host="$r/WidgetHost.cs"
  Shelf="$r/DeskSpaceDropShelfV15.cs"
  Tests="r36/UiSmoke/Program.cs"
}
foreach($pair in $files.GetEnumerator()){
 if(!(Test-Path $pair.Value)){throw "Missing $($pair.Key) source"}
}
function Needs([string]$file,[string[]]$tokens){
 $content=[IO.File]::ReadAllText($files[$file])
 foreach($token in $tokens){if(!$content.Contains($token)){
   throw "$file integration missing $token"
 }}
}
Needs Model @('public List<DeskSpaceActionStep> AutomationSteps',
 'TriggerCondition','TriggerTarget')
Needs Engine @('ValidCondition(','ConditionReady(','internal static Task Run(',
 'DeskSpaceWorkflowTemplatesV84','UseShellExecute=true',
 'File.Move(temp,Filename,true)')
Needs Editor @('DeskSpaceReminderPickerV81.Create(',
 'Save template','Test condition','+ Add action step','LoadTemplate()',
 'MessageBox.Show("Open this target now')
Needs Due @('Where(DeskSpaceAutomationV84.ConditionReady)',
 'DeskSpaceAutomationV84.Run(reminder)','nextCheck=now.AddSeconds(10)')
Needs Widget @('var automation=new DeskSpaceAutomationEditorV84(',
 'AutomationSteps=automation.Steps','automation.Load(entry);',
 'DeskSpaceClipboardQuickActionsV85.Open(clip)')
Needs Main @('private DeskSpaceAutomationEditorV84? reminderAutomation;',
 'ReminderAutomationHost_Loaded','AutomationSteps=reminderAutomation?.Steps',
 'reminderAutomation?.Load(selected)')
Needs Xaml @('x:Name="ReminderAutomationHost"',
 'Loaded="ReminderAutomationHost_Loaded"')
Needs Profile @('widget-profiles.json','PinnedApps.Clear()',
 'target.Save();','AddMenu(')
Needs Preview @('class DeskSpaceFilePreviewV85','class DeskSpaceClipboardQuickActionsV85',
 'Preview file','SafeOpen(')
Needs Shortcut @('RegisterHotKey(','UnregisterHotKey(')
Needs Host @('DeskSpaceWorkspaceProfilesV85.AddMenu(',
 'DeskSpaceCommandShortcutV85','OpenInWidget("Search")')
Needs Shelf @('DeskSpaceFilePreviewV85.Attach(')
Needs Tests @('TestAutomationAndProductivityV85();',
 'r84-workflow-editor','r85-recent-file-preview')
Write-Host 'R84+R85 PREBUILD PASS: safeguarded multi-actions, conditional scheduling, templates, profiles, preview, clipboard, hotkey, both editors.'
