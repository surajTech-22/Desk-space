$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r62/DeskSpaceSubDonutStandardV62.cs' (Join-Path $root 'DeskSpaceSubDonutStandardV62.cs') -Force
# The latest Applications module is copied by R59 before R61.
Copy-Item 'r55/DeskSpaceApplicationsV55.cs' (Join-Path $root 'DeskSpaceApplicationsV55.cs') -Force
function Read([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function Swap([string]$a,[string]$b)
{
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R62 expected 1 anchor, found $n : $a"}
 $script:s=$script:s.Replace($a,$b)
}
# Information: four saved entries plus its permanent + New = five slots.
# Count bubbles are real active controls; mouse wheel increments ONE item.
$p=Join-Path $root 'DeskSpaceInformationV36.cs';$s=Read $p
Swap '    private const int PageSize=4;' @'
    private const int PageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private readonly DeskSpaceSubDonutStandardV62.Wheel standardWheel=new();
'@
Swap @'
        double halfSweep=count==1?17:Math.Min(57,12.0*count);
        double start=-90-halfSweep;
        double step=halfSweep*2/count;
'@ @'
        var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);
        double halfSweep=layout.Half;
        double start=layout.From(0);
        double step=layout.Step;
'@
Swap @'
        // Only offer pagination when there is actually something to page.
        if(startIndex>0)
        {
            var left=SmallButton("‹",()=>Slide(-1));
            left.MouseEnter+=(_,_)=>StartEdgeScroll(-1);
            left.Width=32;left.Height=29;left.FontSize=20;
            left.ToolTip="Previous collections";
            Canvas.SetLeft(left,0);Canvas.SetTop(left,166);
            arcCanvas.Children.Add(left);
        }
        if(startIndex+PageSize<all.Count)
        {
            var right=SmallButton("›",()=>Slide(1));
            right.MouseEnter+=(_,_)=>StartEdgeScroll(1);
            right.Width=32;right.Height=29;right.FontSize=20;
            right.ToolTip="Next collections";
            Canvas.SetLeft(right,456);Canvas.SetTop(right,166);
            arcCanvas.Children.Add(right);
        }
'@ @'
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count+1,startIndex,count,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
        DeskSpaceSubDonutStandardV62.DrawHiddenCounters(
            arcCanvas,countView,arcCanvas.Height/2,Slide);
'@
Swap '        Slide(e.Delta<0?1:-1);e.Handled=true;' @'
        int amount=standardWheel.Step(e.Delta);
        if(amount!=0)Slide(amount);
        e.Handled=true;
'@
Swap @'
        // A short eased radial fade makes fast page changes read as a
        // continuous wheel, not a jump between disjoint menus.
        var fade=new DoubleAnimation(.68,1,
            new Duration(TimeSpan.FromMilliseconds(145))){
            EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
        arcCanvas.BeginAnimation(UIElement.OpacityProperty,fade);
'@ @'
        DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,-90);
'@
Save $p $s

# Excel Functions: one reusable five-item standard at every nesting depth;
# independent wheel accumulators, count bubbles and same eased slide.
$p=Join-Path $root 'DeskSpaceExcelWheelV42.cs';$s=Read $p
Swap '    private const int PerPage=4;' @'
    private const int PerPage=DeskSpaceSubDonutStandardV62.Visible;
    private readonly DeskSpaceSubDonutStandardV62.Wheel rootWheel=new();
'@
# A nested ring inherits the angular width of its selected parent item,
# so it can use at most HALF of each adjacent sibling's available space.
Swap '        internal readonly double Angle;' @'
        internal readonly double Angle;
        internal readonly double ParentSweep;
'@
Swap '        internal ChildRing(int depth,string group,double angle,Window owner)' @'
        internal ChildRing(int depth,string group,double angle,double parentSweep,Window owner)
'@
Swap '            Depth=depth;Group=group;Angle=angle;' @'
            Depth=depth;Group=group;Angle=angle;ParentSweep=parentSweep;
'@
$oldRoot='                    ExpandRing(1,"",RootAngle(slot));'
$newRoot='                    ExpandRing(1,"",RootAngle(slot),rootStep);'
if(([regex]::Matches($s,[regex]::Escape($oldRoot))).Count -ne 2){
    throw 'R62 expected both delayed and direct Excel information expansion sites'
}
$s=$s.Replace($oldRoot,$newRoot)
Swap '                    ExpandRing(depth+1,item.Group,RingAngle(ring,slot));' @'
                    ExpandRing(depth+1,item.Group,RingAngle(ring,slot),ring.StepAngle);
'@
Swap '    private void ExpandRing(int depth,string group,double angle)' @'
    private void ExpandRing(int depth,string group,double angle,double parentSweep)
'@
Swap '        var ring=new ChildRing(depth,group,angle,owner);' @'
        var ring=new ChildRing(depth,group,angle,parentSweep,owner);
'@

Swap '        internal int Start,Hovered=-1;' @'
        internal int Start,Hovered=-1;
        internal readonly DeskSpaceSubDonutStandardV62.Wheel Wheel=new();
'@
Swap '            PageRing(ring,e.Delta<0?1:-1);e.Handled=true;' @'
            int amount=ring.Wheel.Step(e.Delta);
            if(amount!=0)PageRing(ring,amount);
            e.Handled=true;
'@
Swap @'
        int count=ring.Items.Count;
        if(count==0)return;
        // Information's exact half-sweep and relative WPF Popup positioning.
        double half=DeskSpaceSubArcGeometryV48.Half(count);
'@ @'
        int count=ring.Items.Count;
        if(count==0)return;
        // Standard shared five-slot sweep and relative WPF Popup positioning.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ring.Angle,ring.ParentSweep).Half;
'@
Swap @'
        int count=displayed.Count;
        if(count==0)return;
        // Same small partial sector arc used by the working Information
        // submenu, centered precisely on Excel's existing outer wedge.
        double half=DeskSpaceSubArcGeometryV48.Half(count);
'@ @'
        int count=displayed.Count;
        if(count==0)return;
        // Shared standard root arc, centered on Excel's existing outer wedge.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ArcCenter).Half;
'@
Swap @'
        if(ring.Start>0)
        {
            var previous=SmallButton("‹",()=>PageRing(ring,-1));
            Canvas.SetLeft(previous,0);
            Canvas.SetTop(previous,Math.Max(0,ring.Surface.Height/2-14));
            ring.Surface.Children.Add(previous);
        }
        if(ring.Start+PerPage<all.Count)
        {
            var next=SmallButton("›",()=>PageRing(ring,1));
            Canvas.SetLeft(next,Math.Max(0,ring.Surface.Width-31));
            Canvas.SetTop(next,Math.Max(0,ring.Surface.Height/2-14));
            ring.Surface.Children.Add(next);
        }
'@ @'
        var scrollView=DeskSpaceSubDonutStandardV62.Plan(
            all.Count,ring.Start,centre);
        DeskSpaceSubDonutStandardV62.DrawHiddenCounters(
            ring.Surface,scrollView,ring.Surface.Height/2,
            dir=>PageRing(ring,dir));
'@
Swap @'
        CloseFrom(ring.Depth+1);
        RenderRing(ring);
'@ @'
        CloseFrom(ring.Depth+1);
        RenderRing(ring);
        DeskSpaceSubDonutStandardV62.Animate(ring.Surface,dir,ring.Angle);
'@
Swap @'
        if(start>0)
        {
            var previous=SmallButton("‹",()=>Page(-1));
            Canvas.SetLeft(previous,0);
            Canvas.SetTop(previous,Math.Max(0,canvas.Height/2-15));
            canvas.Children.Add(previous);
        }
        if(start+PerPage<all.Count)
        {
            var next=SmallButton("›",()=>Page(1));
            Canvas.SetLeft(next,Math.Max(0,canvas.Width-30));
            Canvas.SetTop(next,Math.Max(0,canvas.Height/2-15));
            canvas.Children.Add(next);
        }
'@ @'
        var scrollView=DeskSpaceSubDonutStandardV62.Plan(all.Count,start,ArcCenter);
        DeskSpaceSubDonutStandardV62.DrawHiddenCounters(
            canvas,scrollView,canvas.Height/2,Page);
'@
Swap @'
        start=Math.Clamp(start+dir,0,Math.Max(0,Entries().Count-PerPage));
        Render();
        if(!CanPage(dir))edgeTimer.Stop();
'@ @'
        start=Math.Clamp(start+dir,0,Math.Max(0,Entries().Count-PerPage));
        Render();
        DeskSpaceSubDonutStandardV62.Animate(canvas,dir,ArcCenter);
        if(!CanPage(dir))edgeTimer.Stop();
'@
Swap '        Page(e.Delta<0?1:-1);e.Handled=true;' @'
        int amount=rootWheel.Step(e.Delta);
        if(amount!=0)Page(amount);
        e.Handled=true;
'@
Save $p $s
Write-Host 'R62: unified max-5 visible parent/child menus; smooth mouse-wheel transitions and both hidden-side counters.'
