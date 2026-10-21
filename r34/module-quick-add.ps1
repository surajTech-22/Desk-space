$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r34/DeskSpaceInlineV34.cs' "$root/DeskSpaceInlineV34.cs" -Force
Copy-Item 'r34/DeskSpaceCompactCalendarV34.cs' "$root/DeskSpaceCompactCalendarV34.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$before,[string]$after) {
    if(!$script:s.Contains($before)){throw "R34 quick add missing anchor: $before"}
    $script:s=$script:s.Replace($before,$after)
}

# Add small + badges INSIDE the actual outer/inner donut sectors rather than
# to the legacy DeskSpaceModuleV10 view that the compact modules never use.
$donutStart=$s.IndexOf('    private void BuildDonut()', [StringComparison]::Ordinal)
$donutEnd=if($donutStart -ge 0){$s.IndexOf('    private ', $donutStart+26,[StringComparison]::Ordinal)}else{-1}
if($donutStart -lt 0 -or $donutEnd -le $donutStart){throw 'R34 BuildDonut method unavailable'}
$donut=$s.Substring($donutStart,$donutEnd-$donutStart)
$required=@('bool outer=i>=6;int slot=i%6;','double radius=outer?162:98;',
 'canvas.Children.Add(label);')
foreach($key in $required){if(!$donut.Contains($key)){throw "R34 BuildDonut anchor missing: $key"}}
$donut=$donut.Replace('bool outer=i>=6;int slot=i%6;',@'
bool outer=i>=6;int slot=i%6;
            bool hasQuickAdd=HasModuleQuickAdd(Segments[i]);
'@)
$donut=$donut.Replace('double radius=outer?162:98;',
    'double radius=outer?(hasQuickAdd?151:162):(hasQuickAdd?87:98);')
$donut=$donut.Replace('            canvas.Children.Add(label);',@'
            canvas.Children.Add(label);
            if(hasQuickAdd)
            {
                // True in-donut hit region, no separate popup HWND or
                // overlapping modal panel. Click and drag release both
                // route through QuickAddHitAt on the parent gesture.
                double markRadius=outer?182:113;
                double px=210+markRadius*Math.Cos(angle);
                double py=210+markRadius*Math.Sin(angle);
                var mark=new Ellipse{
                    Width=20,Height=20,Fill=accent,
                    Stroke=Brushes.White,StrokeThickness=1.4,
                    IsHitTestVisible=false};
                Canvas.SetLeft(mark,px-10);Canvas.SetTop(mark,py-10);
                canvas.Children.Add(mark);
                var plus=new TextBlock{
                    Text="+",FontSize=17,FontWeight=FontWeights.Bold,
                    Foreground=Brushes.White,TextAlignment=TextAlignment.Center,
                    Width=20,Height=20,IsHitTestVisible=false};
                Canvas.SetLeft(plus,px-10);Canvas.SetTop(plus,py-2-10);
                canvas.Children.Add(plus);
            }
'@)
$s=$s.Substring(0,$donutStart)+$donut+$s.Substring($donutEnd)

# Quick add hit testing is always in the same 420-DIP coordinate space
# as SectorAt(), so screen scaling does not change its click area.
$utility=@'
    private static bool HasModuleQuickAdd(string module) =>
        module is "Notes" or "Reminders" or "Calendar" or "Clipboard";

    private int QuickAddHitAt(Point p)
    {
        if(!ringExpanded || activeModule!=null)return -1;
        double scale=420.0/Width;
        double x=210+(p.X-Width/2)*scale;
        double y=210+(p.Y-Height/2)*scale;
        for(int i=0;i<Segments.Length;i++)
        {
            if(!HasModuleQuickAdd(Segments[i]))continue;
            bool outer=i>=6;
            int slot=i%6;
            double angle=(-90+slot*60)*Math.PI/180;
            double distance=outer?182:113;
            double px=210+distance*Math.Cos(angle);
            double py=210+distance*Math.Sin(angle);
            double dx=x-px,dy=y-py;
            if(dx*dx+dy*dy<=121)return i; // eleven-DIP target radius
        }
        return -1;
    }

'@
$utility=$utility.Replace([string][char]13+[char]10,[string][char]10)
$location=$s.IndexOf('    private void BuildDonut()', [StringComparison]::Ordinal)
if($location -lt 0){throw 'R34 no insertion location for plus hit test'}
$s=$s.Substring(0,$location)+$utility+$s.Substring($location)

$upStart=$s.IndexOf('    private void OnGestureUp(',[StringComparison]::Ordinal)
$upEnd=if($upStart -ge 0){$s.IndexOf('    private void ', $upStart+25,[StringComparison]::Ordinal)}else{-1}
if($upStart -lt 0 -or $upEnd -le $upStart){throw 'R34 cannot isolate ring gesture-up'}
$up=$s.Substring($upStart,$upEnd-$upStart)
$oldUp='        informationOverlay?.EndCapturedDrag();'
$insert=@'
        if(ringGesture)
        {
            int addSector=QuickAddHitAt(e.GetPosition(this));
            if(addSector>=0)
            {
                holdTimer.Stop();
                holdPending=false;ringGesture=false;highlightedSector=-1;
                if(IsMouseCaptured)ReleaseMouseCapture();
                string target=Segments[addSector];
                CollapseRing();
                Dispatcher.BeginInvoke(new Action(()=>OpenInWidget(target,true)));
                e.Handled=true;return;
            }
        }
        informationOverlay?.EndCapturedDrag();
'@
$insert=$insert.Replace([string][char]13+[char]10,[string][char]10)
if(!$up.Contains($oldUp)){throw 'R34 mouse-up hook missing'}
$up=$up.Replace($oldUp,$insert)
$s=$s.Substring(0,$upStart)+$up+$s.Substring($upEnd)

Must 'private void OpenInWidget(string name)' 'private void OpenInWidget(string name,bool quickAddRequested=false)'
Must 'new DeskSpaceInlineV11(name,previousCenter,ReturnFromModule)' 'new DeskSpaceInlineV34(name,previousCenter,ReturnFromModule,quickAddRequested)'
Must 'new DeskSpaceCompactCalendarV24().Create(' 'new DeskSpaceCompactCalendarV34().Create('

# The calendar donut + is the one direct route into the existing full
# planner's event editor; ordinary Calendar still opens the shorter preview.
$openStart=$s.IndexOf('    private void OpenInWidget(',[StringComparison]::Ordinal)
$openEnd=if($openStart -ge 0){$s.IndexOf('    private void PositionModuleFromCircle(', $openStart,[StringComparison]::Ordinal)}else{-1}
if($openStart -lt 0 -or $openEnd -le $openStart){throw 'R34 OpenInWidget boundaries missing'}
$open=$s.Substring($openStart,$openEnd-$openStart)
$calendarOld=@'
        else if(name=="Calendar")
        {
            var bounds=ModuleArea();
            double size=Math.Min(OpenDiameter,Math.Min(bounds.Width,bounds.Height));
            Width=size;Height=size;
            surface=new Viewbox{Stretch=Stretch.Uniform,
                Child=new DeskSpaceCompactCalendarV34().Create(OpenCalendarExpanded,ReturnFromModule)};
        }
'@
$calendarNew=@'
        else if(name=="Calendar")
        {
            if(quickAddRequested)
            {
                var bounds=ModuleArea();
                Width=Math.Min(bounds.Width,455);
                Height=Math.Min(bounds.Height,550);
                surface=activeModule.Build("Calendar");
            }
            else
            {
                var bounds=ModuleArea();
                double size=Math.Min(OpenDiameter,Math.Min(bounds.Width,bounds.Height));
                Width=size;Height=size;
                surface=new Viewbox{Stretch=Stretch.Uniform,
                    Child=new DeskSpaceCompactCalendarV34().Create(OpenCalendarExpanded,ReturnFromModule)};
            }
        }
'@
$calendarOld=$calendarOld.Replace([string][char]13+[char]10,[string][char]10)
$calendarNew=$calendarNew.Replace([string][char]13+[char]10,[string][char]10)
if(!$open.Contains($calendarOld)){throw 'R34 Calendar view quick add patch not compatible'}
$open=$open.Replace($calendarOld,$calendarNew)
$open=$open.Replace('        Show();Activate();',@'
        Show();Activate();
        if(quickAddRequested && name=="Calendar")
            Dispatcher.BeginInvoke(new Action(()=>activeModule?.StartNewItem()),
                System.Windows.Threading.DispatcherPriority.Input);
'@)
$s=$s.Substring(0,$openStart)+$open+$s.Substring($openEnd)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))

$p="$root/DeskSpaceModuleV10.cs"
$module=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$anchor='    private Action? headerAddAction;'
if(!$module.Contains($anchor)){throw 'R34 live calendar header + source unavailable'}
$module=$module.Replace($anchor,$anchor+[string][char]10+
'    public void StartNewItem()=>headerAddAction?.Invoke();')
[IO.File]::WriteAllText($p,$module,[Text.UTF8Encoding]::new($false))
Write-Host 'R34: Notes/Reminders/Calendar/Clipboard + targets on actual inner/outer donut, compact inline headers and calendar preview, linked to live creation/capture workflows.'
