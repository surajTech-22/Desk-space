$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function ReadSource([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function WriteSource([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R83 $label anchor count=$n"}
 $script:s=$script:s.Replace($a,$b)
}
Copy-Item 'r83/DeskSpaceReminderScheduleV83.cs' "$r/DeskSpaceReminderScheduleV83.cs" -Force
Copy-Item 'r83/DeskSpaceReminderActionHistoryV83.cs' "$r/DeskSpaceReminderActionHistoryV83.cs" -Force
Copy-Item 'r83/DeskSpaceDueShelfV83.cs' "$r/DeskSpaceDueShelfV74.cs" -Force
$p="$r/Models.cs";$s=ReadSource $p
One '    public DateTime? LastAlert { get; set; }' @'
    public DateTime? LastAlert { get; set; }
    // Optional properties; legacy workspace.json files remain compatible.
    public string RepeatPattern { get; set; } = "None";
    public string RepeatDays { get; set; } = "";
    public int RepeatEveryDays { get; set; } = 1;
    public DateTime? SnoozedUntil { get; set; }
    public bool SnoozeOnly { get; set; }
'@ 'ReminderItem recurrence schema'
One @'
    public void Save(Workspace model)
    {
        var tmp = DataFile + ".tmp";
        var serialized = JsonSerializer.Serialize(model, Options);
        File.WriteAllText(tmp, serialized);
        if (File.Exists(DataFile)) File.Copy(DataFile, DataFile + ".bak", true);
        File.Move(tmp, DataFile, true);
    }
'@ @'
    public void Save(Workspace model)
    {
        // Named mutex serializes writes from the two DeskSpace processes.
        // A unique temporary file prevents a failed writer from corrupting
        // the prior workspace.json. Preserve a last-known-good backup.
        using var gate=new System.Threading.Mutex(false,
            @"Local\DeskSpace.Workspace.JsonWrite");
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(10));}
            catch(System.Threading.AbandonedMutexException){acquired=true;}
            if(!acquired)throw new IOException("DeskSpace workspace is busy");
            var temp=DataFile+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                var serialized=JsonSerializer.Serialize(model,Options);
                File.WriteAllText(temp,serialized);
                if(File.Exists(DataFile))
                    File.Copy(DataFile,DataFile+".bak",true);
                File.Move(temp,DataFile,true);
            }
            finally
            {
                if(File.Exists(temp))File.Delete(temp);
            }
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }
'@ 'atomic cross-process save'
WriteSource $p $s

$p="$r/DeskSpaceInlineV36.cs";$s=ReadSource $p
$a=$s.IndexOf('    private void BuildReminders(StackPanel body)')
$b=$s.IndexOf('    private void BuildClipboard(StackPanel body)',$a)
if($a -lt 0 -or $b -le $a){throw 'R83 actual V36 reminder editor missing'}
$s=$s.Substring(0,$a)+(ReadSource 'r83/ReminderInlineV83.fragment.txt')+$s.Substring($b)
WriteSource $p $s

$p="$r/MainWindow.xaml";$s=ReadSource $p
$pattern='(?s)<TabItem Tag="Reminders" Header="◉  Reminders">.*?</TabItem>'
if(([regex]::Matches($s,$pattern)).Count -ne 1){throw 'R83 main Workspace reminder tab missing'}
$fragment=ReadSource 'r83/MainReminderV83.xaml.fragment.txt'
$old='              <Button x:Name="ReminderBrowseFileButton"'
$insert='              <Button Content="Perform action" Click="ReminderChooseAction_Click" Padding="9,5" Margin="0,0,7,3"/>'+[string][char]10+'              <Button x:Name="ReminderBrowseFileButton"'
if(!$fragment.Contains($old)){throw 'R83 existing Perform action button marker missing'}
$fragment=$fragment.Replace($old,$insert)
$s=[regex]::Replace($s,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$fragment})
WriteSource $p $s

$p="$r/MainWindow.xaml.cs";$s=ReadSource $p
$a=$s.IndexOf('    private string reminderSelectedPath=""')
if($a -lt 0){$a=$s.IndexOf('    private static string ReminderMode(')}
$b=$s.IndexOf('    private void NewSnippet_Click(',$a)
if($a -lt 0 -or $b -le $a){throw 'R83 main Workspace reminder handlers missing'}
$s=$s.Substring(0,$a)+(ReadSource 'r83/MainReminderHandlersV83.fragment.txt')+$s.Substring($b)
WriteSource $p $s
Write-Host 'R83: shared recurrence/snooze engine, event-driven due scheduler, missed-item recovery, action history, backup-safe workspace writes, both real reminder editors updated.'
