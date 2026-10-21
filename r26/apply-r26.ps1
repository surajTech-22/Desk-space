$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r26/DeskSpaceInformationV26.cs' "$root/DeskSpaceInformationV26.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R26 widget source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
$start=$s.IndexOf('    private static readonly string[] Segments = {',[StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    private DeskSpaceModuleV10? activeModule;', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R26 cannot find complete donut segments and icon arrays'}
$segment=$s.Substring($start,$end-$start)
foreach($key in @('"Information"','"Notes"','"Reminders"','"Search"','"Calendar"','SegmentIcons')) {
    if(!$segment.Contains($key)){throw "R26 ring safety check missing: $key"}
}
$replacement=@'
    // The inner ring holds direct tools. The existing OUTER ring holds
    // collection/list modules that may open contextual partial sub-arcs.
    private static readonly string[] Segments = {
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings",
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks"
    };
    private static readonly string[] SegmentIcons = {
        "⌕", "▦", "⊞", "◷", "☀", "⚙",
        "◆", "✎", "◉", "▣", "▤", "☑"
    };
'@
$s=$s.Substring(0,$start)+$replacement+[Environment]::NewLine+$s.Substring($end)
Must '    private DeskSpaceModuleV10? activeModule;' @'
    private DeskSpaceModuleV10? activeModule;
    private DeskSpaceInformationV26? informationOverlay;
'@
$click=$s.IndexOf('    private void ClickSegment(string name)',[StringComparison]::Ordinal)
$clickEnd=if($click -ge 0){$s.IndexOf('    private void ApplySettings()', $click,[StringComparison]::Ordinal)}else{-1}
if($click -lt 0 -or $clickEnd -le $click){throw 'R26 cannot isolate ring selection'}
$method=$s.Substring($click,$clickEnd-$click)
$opening='    private void ClickSegment(string name)'+[string][char]10+'    {'
if(!$method.Contains($opening)){throw 'R26 click segment opening syntax changed'}
$method=$method.Replace($opening,$opening+@'

        if(name=="Information")
        {
            OpenInformationArc();
            return;
        }
        informationOverlay?.Hide();
'@)
$s=$s.Substring(0,$click)+$method+$s.Substring($clickEnd)
$open=$s.IndexOf('    private void OpenInWidget(string name)',[StringComparison]::Ordinal)
$openEnd=if($open -ge 0){$s.IndexOf('    private void PositionModuleFromCircle(', $open,[StringComparison]::Ordinal)}else{-1}
if($open -lt 0 -or $openEnd -le $open){throw 'R26 module opening method boundary changed'}
$openMethod=$s.Substring($open,$openEnd-$open)
$openAnchor='    private void OpenInWidget(string name)'+[string][char]10+'    {'
if(!$openMethod.Contains($openAnchor)){throw 'R26 module opening syntax changed'}
$openMethod=$openMethod.Replace($openAnchor,$openAnchor+@'

        if(name=="Information"){OpenInformationArc();return;}
        informationOverlay?.Hide();
'@)
$s=$s.Substring(0,$open)+$openMethod+$s.Substring($openEnd)
$overlay=@'
    private void OpenInformationArc()
    {
        if(activeModule!=null)return;
        informationOverlay??=new DeskSpaceInformationV26(this,()=>{
            if(informationOverlay?.IsOpen==true)
                content.Text="Information"+Environment.NewLine+
                    informationOverlay.SavedCount+" saved";
            else UpdateCenter();
        });
        informationOverlay.Show();
        if(informationOverlay.IsOpen)
            content.Text="Information"+Environment.NewLine+
                informationOverlay.SavedCount+" saved";
    }

'@
$anchor='    private void ClickSegment(string name)'
if(!$s.Contains($anchor)){throw 'R26 missing arc insertion anchor'}
$s=$s.Replace($anchor,$overlay+$anchor)
$center='    private void UpdateCenter()'+[string][char]10+'    {'
if(!$s.Contains($center)){throw 'R26 UpdateCenter method not located'}
$s=$s.Replace($center,$center+@'

        if(informationOverlay?.IsOpen==true)
        {
            content.Text="Information"+Environment.NewLine+
                informationOverlay.SavedCount+" saved";
            return;
        }
'@)
# The existing MouseLeave behavior can collapse the ring when the cursor
# enters the outside popup; hold it open while its linked arc/shelf is active.
$leave='if (ringExpanded && !ringGesture && !draggingGesture &&'
if(!$s.Contains($leave)){throw 'R26 MouseLeave ring anchor not located'}
$s=$s.Replace($leave,'if (ringExpanded && informationOverlay?.IsOpen!=true && !ringGesture && !draggingGesture &&')
$cleanup='activeModule?.Dispose();'
if(!$s.Contains($cleanup)){throw 'R26 WPF cleanup anchor absent'}
$s=$s.Replace($cleanup,'activeModule?.Dispose(); informationOverlay?.Dispose();')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R26 outer ring contains collection-based modules; Information opens partial sliding arc and compact shelf. Existing live modules, vault, and source preserved.'
