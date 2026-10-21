$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r33/DeskSpaceInformationV33.cs' "$root/DeskSpaceInformationV33.cs" -Force
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$source){[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))}
function Must([string]$before,[string]$after) {
    if(!$script:s.Contains($before)){throw "R33 source anchor missing: $before"}
    $script:s=$script:s.Replace($before,$after)
}

$p="$root/WidgetHost.cs"
$s=Read $p
Must 'DeskSpaceInformationV32? informationOverlay' 'DeskSpaceInformationV33? informationOverlay'
Must 'new DeskSpaceInformationV32(this,' 'new DeskSpaceInformationV33(this,'
Save $p $s

$p="$root/DeskSpaceModuleV10.cs"
$s=Read $p
Must '    public FrameworkElement Build(string mode)' @'
    private Action? headerAddAction;
    public FrameworkElement Build(string mode)
'@
$buildStart=$s.IndexOf('    public FrameworkElement Build(string mode)',[StringComparison]::Ordinal)
$buildEnd=if($buildStart -ge 0){$s.IndexOf('    private void BuildSearch(',$buildStart,[StringComparison]::Ordinal)}else{-1}
if($buildStart -lt 0 -or $buildEnd -le $buildStart){throw 'R33 module Build boundaries unavailable'}
$block=$s.Substring($buildStart,$buildEnd-$buildStart)
if(!$block.Contains('var top=new DockPanel();') -or !$block.Contains('switch(mode)'))
{throw 'R33 module header changed since prior release'}
$block=$block.Replace('    {'+[string][char]10+'        var root = new DockPanel',
    '    {'+[string][char]10+'        headerAddAction=null;'+[string][char]10+'        var root = new DockPanel')
$last=$block.LastIndexOf('        return new Border{',[StringComparison]::Ordinal)
if($last -lt 0){throw 'R33 module return was unexpectedly changed'}
$header=@'
        if(headerAddAction!=null)
        {
            var add=Btn("+",()=>headerAddAction?.Invoke());
            add.ToolTip=mode switch{
                "Notes"=>"New note",
                "Reminders"=>"New reminder",
                "Calendar"=>"New calendar event",
                "Clipboard"=>"Capture current clipboard text/files",
                _=>"Add new"};
            add.MinWidth=32;add.Width=34;add.Height=31;
            add.FontSize=19;
            add.Margin=new Thickness(2,0,5,0);
            add.Background=Brushes.White;
            DockPanel.SetDock(add,Dock.Right);
            top.Children.Insert(0,add);
        }
'@
$block=$block.Substring(0,$last)+$header+[string][char]10+$block.Substring($last)
$s=$s.Substring(0,$buildStart)+$block+$s.Substring($buildEnd)

function Amend([string]$start,[string]$end,[string]$old,[string]$new,[string]$name){
    $a=$script:s.IndexOf($start,[StringComparison]::Ordinal)
    $b=if($a -ge 0){$script:s.IndexOf($end,$a,[StringComparison]::Ordinal)}else{-1}
    if($a -lt 0 -or $b -le $a){throw "R33 cannot isolate $name"}
    $part=$script:s.Substring($a,$b-$a)
    if(!$part.Contains($old)){throw "R33 could not locate end of $name"}
    $part=$part.Replace($old,$new)
    $script:s=$script:s.Substring(0,$a)+$part+$script:s.Substring($b)
}
$tail='        Reload();'+[string][char]10+'    }'
Amend '    private void BuildNotes(' '    private void BuildReminders(' $tail @'
        headerAddAction=()=>{
            editing=null;
            picker.SelectedItem=null;
            title.Clear();text.Clear();
            title.Focus();
        };
        Reload();
    }
'@ 'Notes'
Amend '    private void BuildReminders(' '    private void BuildCalendar(' $tail @'
        headerAddAction=()=>{
            list.SelectedItem=null;
            title.Clear();due.SelectedDate=DateTime.Today;
            time.Text="09:00";title.Focus();
        };
        Reload();
    }
'@ 'Reminders'
Amend '    private void BuildCalendar(' '    private void BuildClipboard(' $tail @'
        headerAddAction=()=>{
            title.Clear();date.SelectedDate=DateTime.Today;
            repeat.SelectedIndex=0;title.Focus();
        };
        Reload();
    }
'@ 'Calendar'
# The existing clipboard deliberately keeps data memory-only. + captures
# the currently copied text/files using that SAME clipboard history logic.
# It never claims a paste was completed or deletes source files.
$clipBegin=$s.IndexOf('    private void BuildClipboard(',[StringComparison]::Ordinal)
$clipEnd=if($clipBegin -ge 0){$s.IndexOf('    private void BuildPassword(', $clipBegin,[StringComparison]::Ordinal)}else{-1}
if($clipBegin -lt 0 -or $clipEnd -le $clipBegin){throw 'R33 clipboard method missing'}
$clipboard=$s.Substring($clipBegin,$clipEnd-$clipBegin)
if(!$clipboard.Contains($tail)){throw 'R33 clipboard final Reload missing'}
$clipboard=$clipboard.Replace($tail,@'
        headerAddAction=()=>{
            DeskSpaceClipboardV10.Poll();
            DeskSpaceClipboardV10.HoldCurrentFiles();
            Reload();
        };
        Reload();
    }
'@)
$s=$s.Substring(0,$clipBegin)+$clipboard+$s.Substring($clipEnd)

Save $p $s
Write-Host 'R33: Information shelf follows circle at compact content-dependent size; Notes/Reminders/Calendar/Clipboard gain header + wired to their existing save/capture flows.'
