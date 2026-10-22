$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$before,[string]$after)
{
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($before)){throw "R39 ring anchor missing: $($before.Substring(0,[Math]::Min(100,$before.Length)))"}
 $script:s=$script:s.Replace($before,$after)
}
Must @'
    private static readonly string[] Segments = {
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings",
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks"
    };
    private static readonly string[] SegmentIcons = {
        "⌕", "▦", "⊞", "◷", "☀", "⚙",
        "◆", "✎", "◉", "▣", "▤", "☑"
    };
'@ @'
    // Two new modules each retain a dedicated usable hit sector.
    // Existing six inner and six outer modules remain present, in the
    // same order. Excel is the seventh inner sector; Excel Clipboard
    // is the seventh outer sector. No duplicate/hidden module.
    private static readonly string[] Segments = {
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings", "Excel",
        "Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks", "Excel Clipboard"
    };
    private static readonly string[] SegmentIcons = {
        "⌕", "▦", "⊞", "◷", "☀", "⚙", "▥",
        "◆", "✎", "◉", "▣", "▤", "☑", "▦"
    };
'@
Must 'if(selected!=6 && informationOverlay?.IsOpen==true &&' 'if(selected!=7 && informationOverlay?.IsOpen==true &&'
Must 'if(selected==6)' 'if(selected==7)'
# WPF pointer-to-wedge hit testing: a single shared geometric model for
# hover, captured drag release, keyboard focus and mouse click.
$start=$s.IndexOf('    private int SectorAt(',[StringComparison]::Ordinal)
if($start -lt 0){throw 'R39 original SectorAt function not found'}
$end=$s.IndexOf([string][char]10+'    private ', $start+20,[StringComparison]::Ordinal)
if($end -le $start){throw 'R39 SectorAt method boundary not found'}
$sector=@'
    private int SectorAt(Point p)
    {
        if(!ringExpanded || activeModule!=null)return -1;
        double scale=420.0/Width;
        double x=210+(p.X-Width/2)*scale;
        double y=210+(p.Y-Height/2)*scale;
        double dx=x-210,dy=y-210;
        double radius=Math.Sqrt(dx*dx+dy*dy);
        if(radius<70 || radius>195 ||
           (radius>124 && radius<132))return -1;
        bool outer=radius>=132;
        const double sectors=7;
        double step=360.0/sectors;
        double angle=Math.Atan2(dy,dx)*180/Math.PI;
        double offset=(angle+90+step/2+720)%360;
        int slot=(int)Math.Floor(offset/step);
        if(slot<0 || slot>=7 || offset-slot*step>=step-1.5)return -1;
        return (outer?7:0)+slot;
    }
'@
$sector=$sector.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$start)+$sector+$s.Substring($end)
# Quick-add controls must use the exact same seven-slot centers.
$qa=$s.IndexOf('    private int QuickAddHitAt(',[StringComparison]::Ordinal)
$qb=$s.IndexOf('    private void BuildDonut()', $qa,[StringComparison]::Ordinal)
if($qa -lt 0 -or $qb -lt $qa){throw 'R39 QuickAddHitAt segment missing'}
$part=$s.Substring($qa,$qb-$qa)
foreach($pair in @(
 @('bool outer=i>=6;','bool outer=i>=7;'),
 @('int slot=i%6;','int slot=i%7;'),
 @('(-90+slot*60)*Math.PI/180','(-90+slot*(360.0/7))*Math.PI/180')
)){
 if(!$part.Contains($pair[0])){throw "R39 quick-add anchor missing: $($pair[0])"}
 $part=$part.Replace($pair[0],$pair[1])
}
$s=$s.Substring(0,$qa)+$part+$s.Substring($qb)
# Ring geometry and labels both use the SAME seven-segment step.
$ba=$s.IndexOf('    private void BuildDonut()', [StringComparison]::Ordinal)
$bb=$s.IndexOf('    private static Geometry Sector(', $ba,[StringComparison]::Ordinal)
if($ba -lt 0 -or $bb -lt $ba){throw 'R39 BuildDonut segment missing'}
$ring=$s.Substring($ba,$bb-$ba)
foreach($pair in @(
 @('for(int i=0;i<12;i++)','for(int i=0;i<Segments.Length;i++)'),
 @('bool outer=i>=6;int slot=i%6;','bool outer=i>=7;int slot=i%7;'),
 @('double first=-120+slot*60,last=first+58.5;','double step=360.0/7;double first=-90-step/2+slot*step,last=first+step-1.5;'),
 @('(-90+slot*60)*Math.PI/180','(-90+slot*(360.0/7))*Math.PI/180')
)){
 if(!$ring.Contains($pair[0])){throw "R39 ring rendering anchor missing: $($pair[0])"}
 $ring=$ring.Replace($pair[0],$pair[1])
}
# Slightly smaller inner-ring labels avoid colliding with the 130 DIP
# centre and outer + badges in the seven-sector layout.
$ring=$ring.Replace('FontSize=outer?13:12,','FontSize=outer?12:11,')
$ring=$ring.Replace('double radius=outer?(hasQuickAdd?151:162):(hasQuickAdd?87:98);',
  'double radius=outer?(hasQuickAdd?151:162):(hasQuickAdd?94:100);')
$ring=$ring.Replace('Text=SegmentIcons[i]+"\n"+Segments[i],FontSize=outer?13:12,',
  'Text=SegmentIcons[i]+"\n"+(Segments[i]=="Excel Clipboard"?"Excel Clips":Segments[i]),FontSize=outer?12:12,')
$s=$s.Substring(0,$ba)+$ring+$s.Substring($bb)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R39 donut has fourteen real mouse/hover/captured-drag target sectors, preserving original twelve and adding Excel + separate Excel Clipboard.'
