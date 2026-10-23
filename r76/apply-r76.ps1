$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function SaveText([string]$path,[string]$s){
 [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
function One([string]$s,[string]$old,[string]$new,[string]$tag){
 $s=$s.Replace([string][char]13+[char]10,[string][char]10)
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
   throw "R76 unique anchor missing: $tag"
 }
 return $s.Replace($old,$new)
}
Copy-Item 'r76/DeskSpaceDueShelfV76.cs' "$root/DeskSpaceDueShelfV74.cs" -Force
$p="$root/Models.cs"
$s=[IO.File]::ReadAllText($p)
$s=One $s '    public DateTime? LastAlert { get; set; }' @'
    public DateTime? LastAlert { get; set; }
    // Backwards compatible: existing workspace.json reminders default to
    // the original Windows sound and do not launch anything.
    public string AlertSound { get; set; } = "Exclamation";
    public string AlertSoundFile { get; set; } = "";
    public string AlertAction { get; set; } = "None";
    public string AlertTarget { get; set; } = "";
'@ 'persisted reminder sound/action'
SaveText $p $s

$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p)
$s=One $s @'
        "Information", "Notes", "Reminders", "Clipboard", "Applications", "Tasks", "Excel Functions"
'@ @'
        "Information", "Notes", "Reminders", "Clipboard", "Applications", "Excel Functions"
'@ 'remove duplicate Tasks outer-sector'
$s=One $s @'
        "◆", "✎", "◉", "▣", "◈", "☑", "▥"
'@ @'
        "◆", "✎", "◉", "▣", "◈", "▥"
'@ 'remove duplicate Tasks icon'
$s=$s.Replace('outer?7:6','outer?6:6')
$s=$s.Replace('selected==12','selected==11')
$s=$s.Replace('selected!=12','selected!=11')
$s=$s.Replace('highlightedSector==12','highlightedSector==11')
$s=$s.Replace('outer sector 12','outer sector 11')
$s=$s.Replace('outer index 12','outer index 11')
# Retain legacy center display if selected previously, but remove redundant
# Tasks center-menu option and its old ring entry.
$s=One $s 'new[]{"Clock","Date","Calendar","Notes","Reminders","Tasks"}' 'new[]{"Clock","Date","Calendar","Notes","Reminders"}' 'center chooser removes duplicate Tasks'
$s=One $s 'if(name is "Clock" or "Tasks" or "Weather")' 'if(name is "Clock" or "Weather")' 'Tasks no longer special click action'
$s=$s.Replace('name=="Reminders"?278:291','name=="Reminders"?420:291')
SaveText $p $s

$p="$root/DeskSpaceInlineV11.cs"
$s=[IO.File]::ReadAllText($p)
$begin=$s.IndexOf('    private void BuildReminders(StackPanel body)')
$end=$s.IndexOf('    private void BuildClipboard(StackPanel body)',$begin)
if($begin -lt 0 -or $end -le $begin){throw 'R76 missing reminder form block'}
$replacement=[IO.File]::ReadAllText('r76/ReminderEditorV76.fragment.txt')
$s=$s.Substring(0,$begin)+$replacement+$s.Substring($end)
SaveText $p $s
# Re-anchor all outer child wheels after removing the duplicate Tasks wedge.
# Information stays in slot 0; Applications and Excel move to slots 4/5
# of six wedges. Do not let saved pins/mappings drift away from their parent.
$p="$root/DeskSpaceApplicationsV55.cs"
$s=[IO.File]::ReadAllText($p)
$s=One $s 'DeskSpaceDonutGeometryV43.OuterSectorCenter(4)-360.0/14.0' `
 'DeskSpaceDonutGeometryV43.OuterSectorCenter(4,6)-360.0/12.0' 'Apps root anchor'
SaveText $p $s
$p="$root/DeskSpaceExcelWheelV42.cs"
$s=[IO.File]::ReadAllText($p)
$s=One $s 'DeskSpaceDonutGeometryV43.OuterSectorCenter(6)-360.0/14.0' `
 'DeskSpaceDonutGeometryV43.OuterSectorCenter(5,6)-360.0/12.0' 'Excel root anchor'
SaveText $p $s

# R42 created a legacy 13-sector WPF smoke AFTER source checkout. The
# approved merge removes only the duplicate Tasks wedge: test twelve.
$p='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($p)
$s=One $s '    private static void TestDonut13()' '    private static void TestDonut12()' 'WPF donut method'
$s=One $s '            TestDonut13();' '            TestDonut12();' 'WPF donut caller'
$s=One $s 'ring.Count==13' 'ring.Count==12' 'WPF ring sector count'
$s=One $s 'i<13;i++' 'i<12;i++' 'WPF hit testing loop'
$s=One $s '(360.0/(i>=6?7:6))' '(360.0/(i>=6?6:6))' 'WPF hit angles'
$s=$s.Replace('13-sector donut mismatch','12-sector donut mismatch')
$s=$s.Replace('donut-13-excel-functions','donut-12-merged-reminders')
$s=$s.Replace('Excel Functions removed an original 12-sector feature',
  'R76 merged Tasks and Reminders into one of twelve sectors')
SaveText $p $s

Write-Host 'R76 source patched: 6+6 rings; single Reminders module; sound/file/app choices persisted without changing old data.'
