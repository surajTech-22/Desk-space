$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function ReadSource([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function WriteSource([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function Swap([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $count=([regex]::Matches($script:source,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R84 $label expected one anchor, found $count"}
 $script:source=$script:source.Replace($a,$b)
}
Copy-Item 'r84/DeskSpaceAutomationV84.cs' "$r/DeskSpaceAutomationV84.cs" -Force
Copy-Item 'r84/DeskSpaceAutomationEditorV84.cs' "$r/DeskSpaceAutomationEditorV84.cs" -Force
Copy-Item 'r84/DeskSpaceDueShelfV84.cs' "$r/DeskSpaceDueShelfV74.cs" -Force
$p="$r/Models.cs";$source=ReadSource $p
Swap '    public bool SnoozeOnly { get; set; }' @'
    public bool SnoozeOnly { get; set; }
    public List<DeskSpaceActionStep> AutomationSteps { get; set; } = new();
    public string TriggerCondition { get; set; } = "None";
    public string TriggerTarget { get; set; } = "";
'@ 'reminder schema'
WriteSource $p $source
$p="$r/DeskSpaceInlineV36.cs";$source=ReadSource $p
$a=$source.IndexOf('    private void BuildReminders(StackPanel body)')
$b=$source.IndexOf('    private void BuildClipboard(StackPanel body)',$a)
if($a -lt 0 -or $b -le $a){throw 'R84 live reminder editor not found'}
$source=$source.Substring(0,$a)+(ReadSource 'r84/ReminderInlineV84.fragment.txt')+$source.Substring($b)
WriteSource $p $source
$p="$r/MainWindow.xaml";$source=ReadSource $p
$pattern='(?s)<TabItem Tag="Reminders" Header="◉  Reminders">.*?</TabItem>'
if(([regex]::Matches($source,$pattern)).Count -ne 1){throw 'R84 main reminders XAML tab not found'}
$fragment=ReadSource 'r84/MainReminderV84.xaml.fragment.txt'
$marker='              <Button x:Name="ReminderBrowseFileButton"'
if(!$fragment.Contains($marker)){throw 'R84 main Perform action bridge missing'}
$fragment=$fragment.Replace($marker,
    '              <Button Content="Perform action" Click="ReminderChooseAction_Click" Padding="9,5" Margin="0,0,7,3"/>'+
    [string][char]10+$marker)
$source=[regex]::Replace($source,$pattern,
    [System.Text.RegularExpressions.MatchEvaluator]{param($m)$fragment})
WriteSource $p $source
$p="$r/MainWindow.xaml.cs";$source=ReadSource $p
$a=$source.IndexOf('    private string reminderSelectedPath=""')
$b=$source.IndexOf('    private void NewSnippet_Click(',$a)
if($a -lt 0 -or $b -le $a){throw 'R84 actual main reminder handlers not found'}
$source=$source.Substring(0,$a)+
    (ReadSource 'r84/MainReminderHandlersV84.fragment.txt')+$source.Substring($b)
WriteSource $p $source
Write-Host 'R84: sequential validated reminder actions, conditions and saved templates in both reminder interfaces.'
