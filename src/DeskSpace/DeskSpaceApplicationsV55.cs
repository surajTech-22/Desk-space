using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// One shared-centre Information/Excel-style contextual arc. No fullscreen
// transparent popup and no second file manager/pin-search popup.
internal sealed class DeskSpaceApplicationsV55 : IDisposable
{
    private readonly Window owner;
    private readonly Func<IReadOnlyList<string>> pinned;
    private readonly Action<string> addPin,removePin;
    private readonly Action openSearch;
    private readonly Popup arc;
    private readonly Canvas canvas;
    private readonly List<(Geometry Hit,string Kind,string Path)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> fills=new();
    private readonly List<(string Label,string Kind,string Path,double A,double B,int Depth)> items=new();
    private readonly Brush normal=new SolidColorBrush(Color.FromRgb(217,229,248));
    private readonly Brush selected=new SolidColorBrush(Color.FromRgb(143,172,235));
    private readonly Brush accent=new SolidColorBrush(Color.FromRgb(78,98,180));
    private const double BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=323,FileInner=325,FileOuter=465;
    private Rect bounds=Rect.Empty;
    private int childPage,hovered=-1,placementTick;
    private readonly DeskSpaceSubDonutStandardV62.Wheel scrollWheel=new();
    private DeskSpaceSubDonutStandardV62.View currentChildView;
    private string group="";
    private string activeAppPath="";
    private double activeAppCenter;
    private IReadOnlyList<PinnedAppFileItemV380> activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
    private bool activeAppLoading;
    private int activeAppVersion;
    private bool disposed;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll",ExactSpelling=true)]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);

    private static double AppInner(int depth)=>depth switch{
        0=>BaseInner,
        1=>SubInner,
        2=>FileInner,
        _=>DeskSpaceSubArcGeometryV48.Inner(depth)
    };
    private static double AppOuter(int depth)=>depth switch{
        0=>BaseOuter,
        1=>SubOuter,
        2=>FileOuter,
        _=>DeskSpaceSubArcGeometryV48.Outer(depth)
    };
    private static Geometry AppSector(int depth,double from,double to)
    {
        double r0=AppInner(depth),r1=AppOuter(depth);
        if(to<=from||to-from>180)
            throw new ArgumentOutOfRangeException(nameof(to));
        Point At(double r,double deg)
        {
            var p=DeskSpaceSubArcGeometryV48.Polar(r,deg);
            return new Point(DeskSpaceSubArcGeometryV48.CX+p.X,DeskSpaceSubArcGeometryV48.CY+p.Y);
        }
        var fig=new PathFigure{StartPoint=At(r1,from),IsClosed=true};
        fig.Segments.Add(new ArcSegment{
            Point=At(r1,to),Size=new Size(r1,r1),
            SweepDirection=SweepDirection.Clockwise,IsLargeArc=false});
        fig.Segments.Add(new LineSegment(At(r0,to),true));
        fig.Segments.Add(new ArcSegment{
            Point=At(r0,from),Size=new Size(r0,r0),
            SweepDirection=SweepDirection.Counterclockwise,IsLargeArc=false});
        return new PathGeometry(new[]{fig});
    }
    private double FitDepth2Angle(double preferred,double half,double outerRadius)
    {
        Rect work=DeskSpaceSubArcGeometryV48.MonitorWorkArea(owner);
        var handle=new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        double dpiX=PresentationSource.FromVisual(owner)?
            .CompositionTarget?.TransformToDevice.M11??1.0;
        double dpiY=PresentationSource.FromVisual(owner)?
            .CompositionTarget?.TransformToDevice.M22??1.0;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner)*Math.Max(dpiX,dpiY);
        Point centre=new(work.Left+work.Width/2,work.Top+work.Height/2);
        if(handle!=IntPtr.Zero && GetWindowRect(handle,out var rect) &&
           rect.Right>rect.Left && rect.Bottom>rect.Top)
            centre=new Point((rect.Left+rect.Right)/2.0,(rect.Top+rect.Bottom)/2.0);
        double[] shifts={0,-8,8,-16,16,-24,24,-32,32};
        double best=preferred,bestScore=double.PositiveInfinity;
        foreach(double shift in shifts)
        {
            double candidate=preferred+shift;
            double penalty=Math.Abs(shift)*1.5;
            for(int n=0;n<=16;n++)
            {
                double angle=candidate-half+(2*half*n/16.0);
                var p=DeskSpaceSubArcGeometryV48.Polar(outerRadius,angle);
                double x=centre.X+p.X*scale;
                double y=centre.Y+p.Y*scale;
                const double pad=12;
                penalty+=80*(Math.Max(0,work.Left+pad-x)+
                    Math.Max(0,x-(work.Right-pad))+
                    Math.Max(0,work.Top+pad-y)+
                    Math.Max(0,y-(work.Bottom-pad)));
            }
            if(penalty<bestScore){bestScore=penalty;best=candidate;}
        }
        return best;
    }
    private static string FormatArcLabelV380(string text)
    {
        if(string.IsNullOrEmpty(text))return text;
        return text.Replace("_","_\u200B").Replace("-","-\u200B");
    }
    private static ToolTip CreateCompactTooltipV380(string title,string? subtitle=null)
    {
        var panel=new StackPanel{MaxWidth=250};
        panel.Children.Add(new TextBlock{
            Text=title,FontWeight=FontWeights.SemiBold,FontSize=12,
            TextWrapping=TextWrapping.Wrap});
        if(!string.IsNullOrWhiteSpace(subtitle))
            panel.Children.Add(new TextBlock{
                Text=subtitle,FontSize=11,Opacity=0.82,
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0)});
        return new ToolTip{Content=panel,MaxWidth=268};
    }
    private static (double Left,double Width) InscribedHorizontalSpanV380(
        Geometry geom,double cx,double cy,double halfHeight,double pad,
        double minWidth,double maxWidth,double canvasWidth)
    {
        double leftBound=double.NegativeInfinity,rightBound=double.PositiveInfinity;
        var gb=geom.Bounds;
        double scanMin=Math.Max(0,gb.Left);
        double scanMax=Math.Min(canvasWidth,gb.Right);
        double[] ys=halfHeight>1?new[]{cy-halfHeight,cy,cy+halfHeight}:new[]{cy};
        foreach(double y in ys)
        {
            double seed=cx;
            if(!geom.FillContains(new Point(cx,y)))
            {
                seed=-1;double bestDist=double.MaxValue;
                for(int s=0;s<=24;s++)
                {
                    double px=scanMin+(scanMax-scanMin)*s/24.0;
                    if(geom.FillContains(new Point(px,y)))
                    {
                        double d=Math.Abs(px-cx);
                        if(d<bestDist){bestDist=d;seed=px;}
                    }
                }
                if(seed<0)continue;
            }
            double lo=scanMin,hi=seed;
            for(int k=0;k<12;k++)
            {
                double mid=(lo+hi)*.5;
                if(geom.FillContains(new Point(mid,y)))hi=mid;else lo=mid;
            }
            double yLeft=hi;
            lo=seed;hi=scanMax;
            for(int k=0;k<12;k++)
            {
                double mid=(lo+hi)*.5;
                if(geom.FillContains(new Point(mid,y)))lo=mid;else hi=mid;
            }
            double yRight=lo;
            if(yLeft>leftBound)leftBound=yLeft;
            if(yRight<rightBound)rightBound=yRight;
        }
        if(double.IsNegativeInfinity(leftBound) || double.IsPositiveInfinity(rightBound) || rightBound<=leftBound)
        {
            double fallbackW=Math.Clamp(gb.Width-pad*2,minWidth,maxWidth);
            double fallbackL=Math.Clamp(cx-fallbackW/2,2,Math.Max(2,canvasWidth-fallbackW-2));
            return (fallbackL,fallbackW);
        }
        double innerL=Math.Max(2,leftBound+pad);
        double innerR=Math.Min(canvasWidth-2,rightBound-pad);
        double span=innerR-innerL;
        if(span<=8)
        {
            double mid=(leftBound+rightBound)*.5;
            double narrowW=Math.Clamp(Math.Min(minWidth,canvasWidth-4),18,maxWidth);
            double narrowL=Math.Clamp(mid-narrowW/2,2,Math.Max(2,canvasWidth-narrowW-2));
            return (narrowL,narrowW);
        }
        double finalW=Math.Clamp(span,Math.Min(minWidth,span),maxWidth);
        double finalMid=(innerL+innerR)*.5;
        double finalL=Math.Clamp(finalMid-finalW/2,2,Math.Max(2,canvasWidth-finalW-2));
        return (finalL,finalW);
    }
    public bool IsArcOpen=>arc.IsOpen;
    public int PinnedCount=>pinned().Count;
    // Main parent outer slot #4 starts at this physical direction (the
    // center-of-label angle, not the geometric midpoint).
    internal static double ApplicationsParentCenter=>
        DeskSpaceDonutGeometryV43.OuterSectorCenter(4,6)-360.0/12.0;

    public DeskSpaceApplicationsV55(Window owner,Func<IReadOnlyList<string>> pinned,
        Action<string> addPin,Action<string> removePin,Action openSearch)
    {
        this.owner=owner;this.pinned=pinned;this.addPin=addPin;
        this.removePin=removePin;this.openSearch=openSearch;
        canvas=new Canvas{Width=1,Height=1,Background=null};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
        // Left click retains the original open-latest pinned action. A
        // RIGHT click on that same pinned arc explicitly opens the recent
        // document choice list; no background discovery is scheduled.
        canvas.PreviewMouseRightButtonDown+=RecentRightClickV262;
        canvas.MouseWheel+=Wheel;
        arc=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas};
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
        owner.LocationChanged+=Moved;owner.SizeChanged+=Resized;
    }
    private static bool IsLaunchable(string p)
    {
        if(string.IsNullOrWhiteSpace(p)||!File.Exists(p))return false;
        var ext=System.IO.Path.GetExtension(p);
        return ext.Equals(".exe",StringComparison.OrdinalIgnoreCase)||
            ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||
            ext.Equals(".url",StringComparison.OrdinalIgnoreCase)||
            ext.Equals(".bat",StringComparison.OrdinalIgnoreCase);
    }
    private List<(string Label,string Kind,string Path)> Children()
    {
        // The installed-app list belongs in Search. The Applications wheel
        // shows PINNED shortcuts and provides + Pin / Search actions only.
        var source=pinned().Where(IsLaunchable).Distinct(
            StringComparer.OrdinalIgnoreCase).OrderBy(p=>
            System.IO.Path.GetFileNameWithoutExtension(p),
            StringComparer.OrdinalIgnoreCase).ToList();
        return source.Select(p=>(
            Label:System.IO.Path.GetFileNameWithoutExtension(p),
            Kind:"launch",Path:p)).ToList();
    }
    private void Add(string label,string kind,string path,double a,double b,int depth)
        =>items.Add((label,kind,path,a,b,depth));
    private void Render()
    {
        items.Clear();hitZones.Clear();fills.Clear();canvas.Children.Clear();hovered=-1;
        double centre=ApplicationsParentCenter;
        var roots=new[]{
            ("Pinned","pinned"),("+ Pin","add"),("Search","search")};
        var rootPlan=DeskSpaceSubDonutStandardV62.Plan(roots.Length,0,centre);
        for(int i=0;i<roots.Length;i++)
            Add(roots[i].Item1,roots[i].Item2,"",
                rootPlan.From(i),rootPlan.To(i),0);
        currentChildView=default;
        if(group=="pinned")
        {
            var children=Children();
            currentChildView=DeskSpaceSubDonutStandardV62.Plan(
                children.Count,childPage,centre,rootPlan.Step);
            childPage=currentChildView.Start;
            if(children.Count==0)
                Add("No pins","empty","",centre-12,centre+12,1);
            for(int i=0;i<currentChildView.Count;i++)
            {
                var item=children[currentChildView.Start+i];
                Add(item.Label,item.Kind,item.Path,
                    currentChildView.From(i),currentChildView.To(i),1);
            }
            if(!string.IsNullOrEmpty(activeAppPath) &&
               children.Any(c=>string.Equals(c.Path,activeAppPath,StringComparison.OrdinalIgnoreCase)))
            {
                var d2Items=new List<(string Label,string Kind,string Path)>();
                foreach(var f in activeAppFiles.Take(4))
                {
                    string lbl=f.TallyCompanyCode!=null
                        ? "Tally #"+f.TallyCompanyCode
                        : FormatArcLabelV380(f.DisplayName);
                    d2Items.Add((lbl,"launchfile",f.FullPath));
                }
                if(d2Items.Count==0 && activeAppLoading)
                    d2Items.Add(("Loading…","loading",""));
                d2Items.Add(("All files…","allfiles",activeAppPath));
                double d2Half=Math.Clamp(d2Items.Count*5.25,11.0,28.0);
                double d2Preferred=activeAppCenter*0.45+centre*0.55;
                double d2Center=FitDepth2Angle(d2Preferred,d2Half,AppOuter(2));
                double d2Start=d2Center-d2Half;
                double d2Step=(d2Half*2)/d2Items.Count;
                double normAngle=((d2Center%360)+360)%360;
                bool leftHalf=normAngle>90 && normAngle<270;
                for(int i=0;i<d2Items.Count;i++)
                {
                    var d2=leftHalf?d2Items[d2Items.Count-1-i]:d2Items[i];
                    Add(d2.Label,d2.Kind,d2.Path,d2Start+d2Step*i,d2Start+d2Step*(i+1),2);
                }
            }
        }
        var shapes=items.Select(x=>AppSector(x.Depth,x.A,x.B)).ToArray();
        bounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        canvas.Width=bounds.Width+margin*2;
        canvas.Height=bounds.Height+margin*2;
        var clippedGeoms=new Geometry[items.Count];
        // Pass 1: all wedge geometries at Z=1
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];
            var geometry=DeskSpaceSubArcGeometryV48.Clip(shapes[i],bounds,margin);
            clippedGeoms[i]=geometry;
            bool isAction=item.Kind is "add" or "allfiles";
            bool isSelectedApp=item.Kind=="launch" && !string.IsNullOrEmpty(activeAppPath) &&
                string.Equals(item.Path,activeAppPath,StringComparison.OrdinalIgnoreCase);
            ToolTip tip=item.Kind=="launch"
                ? CreateCompactTooltipV380(item.Label,"Click to open · Right-click for all files")
                : item.Kind=="launchfile"
                    ? CreateCompactTooltipV380(System.IO.Path.GetFileName(item.Path) is string fn && fn.Length>0?fn:item.Label,
                        System.IO.Path.GetDirectoryName(item.Path))
                    : item.Kind=="allfiles"
                        ? CreateCompactTooltipV380("All compatible files",System.IO.Path.GetFileNameWithoutExtension(item.Path))
                        : CreateCompactTooltipV380(item.Label);
            var wedge=new System.Windows.Shapes.Path{
                Data=geometry,Stroke=Brushes.White,StrokeThickness=1.5,
                Fill=isAction?accent:(isSelectedApp?selected:normal),
                Cursor=Cursors.Hand,ToolTip=tip,Tag=i};
            ToolTipService.SetInitialShowDelay(wedge,item.Kind=="launchfile"?650:850);
            ToolTipService.SetBetweenShowDelay(wedge,200);
            Panel.SetZIndex(wedge,1);
            fills.Add(wedge);
            hitZones.Add((geometry,item.Kind,item.Path));
            canvas.Children.Add(wedge);
        }
        // Pass 2: all icons & labels strictly inscribed inside each wedge at Z=10
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];
            bool isAction=item.Kind is "add" or "allfiles";
            double midR=(AppInner(item.Depth)+AppOuter(item.Depth))/2;
            double midA=(item.A+item.B)/2;
            var p=DeskSpaceSubArcGeometryV48.Polar(midR,midA);
            double midX=DeskSpaceSubArcGeometryV48.CX+p.X-bounds.Left+margin;
            double midY=DeskSpaceSubArcGeometryV48.CY+p.Y-bounds.Top+margin;
            bool isAppIcon=item.Kind=="launch" && DeskSpaceApplicationIconsV65.Get(item.Path)!=null;
            double maxLabelH=item.Depth>=2?29:(isAppIcon?24:26);
            double probeHalfH=item.Depth>=2?13:(isAppIcon?17:11);
            double pad=item.Depth>=2?8:4;
            double maxW=item.Depth>=2?120:(item.Depth==1?66:82);
            var (boxLeft,boxWidth)=InscribedHorizontalSpanV380(
                clippedGeoms[i],midX,midY,probeHalfH,pad,item.Depth>=2?46:34,maxW,canvas.Width);
            double unitCenterX=boxLeft+boxWidth*.5;
            var label=new TextBlock{
                Text=item.Label,
                Width=boxWidth,
                MaxHeight=maxLabelH,
                FontSize=item.Depth==1?9.5:10,
                FontWeight=isAction?FontWeights.SemiBold:FontWeights.Normal,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                IsHitTestVisible=false,
                Foreground=isAction?Brushes.White:
                    new SolidColorBrush(Color.FromRgb(37,51,79))};
            label.Measure(new Size(boxWidth,maxLabelH));
            double actualH=Math.Min(maxLabelH,Math.Max(12,label.DesiredSize.Height));
            if(isAppIcon)
            {
                var iconSource=DeskSpaceApplicationIconsV65.Get(item.Path)!;
                const double iconSize=18,gap=2;
                double totalH=iconSize+gap+actualH;
                double topY=Math.Clamp(midY-totalH*.5,2,Math.Max(2,canvas.Height-totalH-2));
                var appIcon=new Image{Source=iconSource,Width=iconSize,Height=iconSize,
                    Stretch=Stretch.Uniform,IsHitTestVisible=false};
                Panel.SetZIndex(appIcon,10);
                Canvas.SetLeft(appIcon,Math.Clamp(unitCenterX-iconSize*.5,2,Math.Max(2,canvas.Width-iconSize-2)));
                Canvas.SetTop(appIcon,topY);
                canvas.Children.Add(appIcon);
                Canvas.SetLeft(label,boxLeft);
                Canvas.SetTop(label,topY+iconSize+gap);
            }
            else
            {
                Canvas.SetLeft(label,boxLeft);
                Canvas.SetTop(label,Math.Clamp(midY-actualH*.5,2,Math.Max(2,canvas.Height-actualH-2)));
            }
            Panel.SetZIndex(label,10);
            canvas.Children.Add(label);
        }
        if(group=="pinned")
            DeskSpaceSubDonutStandardV62.DrawHiddenCounters(canvas,
                currentChildView,canvas.Height/2,MoveChild);
        Position();
    }
    private void ClearActiveAppFilesV380()
    {
        activeAppVersion++;
        activeAppPath="";
        activeAppLoading=false;
        activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
    }
    private void SelectAppFilesV380(string path,double centerAngle)
    {
        if(!IsLaunchable(path))return;
        if(string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase) &&
           Math.Abs(activeAppCenter-centerAngle)<0.5)
            return;
        activeAppPath=path;
        activeAppCenter=centerAngle;
        int version=++activeAppVersion;
        if(DeskSpacePinnedAppFilesV380.TryGetCachedQuickArcFiles(path,out var cached))
        {
            activeAppFiles=cached;
            activeAppLoading=false;
            Render();
            return;
        }
        activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
        activeAppLoading=true;
        Render();
        _=DeskSpacePinnedAppFilesV380.GetQuickArcFilesAsync(path,4).ContinueWith(t=>{
            if(disposed)return;
            var result=t.Status==System.Threading.Tasks.TaskStatus.RanToCompletion
                ? t.Result
                : Array.Empty<PinnedAppFileItemV380>();
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(disposed || !arc.IsOpen || version!=activeAppVersion ||
                   !string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase))
                    return;
                activeAppFiles=result;
                activeAppLoading=false;
                Render();
            }));
        });
    }
    private void MoveChild(int delta)
    {
        if(group!="pinned")return;
        int next=DeskSpaceSubDonutStandardV62.Advance(currentChildView,delta);
        if(next==childPage)return;
        childPage=next;
        ClearActiveAppFilesV380();
        Render();
        DeskSpaceSubDonutStandardV62.Animate(canvas,delta,
            ApplicationsParentCenter);
    }
    private void Position()
    {
        if(disposed||bounds.IsEmpty)return;
        canvas.LayoutTransform=new ScaleTransform(
            DeskSpaceDonutGeometryV43.Scale(owner),
            DeskSpaceDonutGeometryV43.Scale(owner));
        // This is the tight-bounds positioning used by working Excel sub-arcs;
        // a 680px Popup was previously being displaced by WPF edge handling.
        var offset=DeskSpaceSubArcGeometryV48.PopupOffset(owner,bounds);
        arc.HorizontalOffset=offset.X+(++placementTick%2)*.001;
        arc.VerticalOffset=offset.Y;
    }
    public void Show()
    {
        if(disposed)return;
        group="";childPage=0;ClearActiveAppFilesV380();
        Render();arc.IsOpen=owner.IsVisible;
    }
    public void HideArc(){arc.IsOpen=false;group="";childPage=0;ClearActiveAppFilesV380();}
    private int Slot(Point point)
    {
        for(int i=hitZones.Count-1;i>=0;i--)
            if(hitZones[i].Hit.FillContains(point))return i;
        return -1;
    }
    private void Hover(Point point)
    {
        int i=Slot(point);
        if(i==hovered)return;
        hovered=i;
        for(int j=0;j<fills.Count;j++)
        {
            bool activeParent=hitZones[j].Kind=="launch" && !string.IsNullOrEmpty(activeAppPath) &&
                string.Equals(hitZones[j].Path,activeAppPath,StringComparison.OrdinalIgnoreCase);
            fills[j].Fill=(j==i || activeParent)
                ?selected
                :(hitZones[j].Kind is "add" or "allfiles"?accent:normal);
        }
        if(i<0)return;
        var kind=hitZones[i].Kind;
        if(kind=="pinned"&&group!=kind)
        {
            group=kind;childPage=0;ClearActiveAppFilesV380();Render();
            return;
        }
        if(kind=="launch")
        {
            double mid=(items[i].A+items[i].B)/2.0;
            SelectAppFilesV380(hitZones[i].Path,mid);
            return;
        }
        if(kind is "launchfile" or "allfiles" or "loading")
            return;
        if(!string.IsNullOrEmpty(activeAppPath))
        {
            ClearActiveAppFilesV380();
            Render();
        }
    }
    private void Move(object sender,MouseEventArgs e)
    {
        if(!disposed&&arc.IsOpen)Hover(e.GetPosition(canvas));
    }
    private void RecentRightClickV262(object sender,MouseButtonEventArgs e)
    {
        if(disposed || !arc.IsOpen)return;
        int index=Slot(e.GetPosition(canvas));
        if(index<0 || hitZones[index].Kind!="launch")return;
        string pinnedPath=hitZones[index].Path;
        if(!IsLaunchable(pinnedPath))return;
        HideArc();
        owner.Dispatcher.BeginInvoke(new Action(()=>
        {
            try{DeskSpacePinnedAppFilesV380.Show(owner,pinnedPath);}
            catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
        }));
        e.Handled=true;
    }
    private void Down(object sender,MouseButtonEventArgs e)
    {
        if(arc.IsOpen&&Activate(e.GetPosition(canvas)))e.Handled=true;
    }
    public bool TryCapturedDragAtScreen(Point screenPoint,bool released)
    {
        if(disposed||!arc.IsOpen||!canvas.IsLoaded)return false;
        try
        {
            var local=canvas.PointFromScreen(screenPoint);
            if(Slot(local)<0)return false;
            if(released)Activate(local);else Hover(local);
            return true;
        }
        catch(InvalidOperationException){return false;}
    }
    private bool Activate(Point point)
    {
        int slot=Slot(point);
        if(slot<0)return false;
        var item=hitZones[slot];
        switch(item.Kind)
        {
            case "pinned":
                group=item.Kind;childPage=0;Render();return true;
            case "launch":
                if(IsLaunchable(item.Path))
                {
                    string pinnedTarget=item.Path;
                    HideArc();
                    owner.Dispatcher.BeginInvoke(new Action(()=>
                    {
                        try{DeskSpacePinnedAppFilesV380.TryLaunchPinnedAppDirect(pinnedTarget,owner);}
                        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                    }));
                }
                return true;
            case "launchfile":
                {
                    string appTarget=activeAppPath;
                    string fileTarget=item.Path;
                    HideArc();
                    owner.Dispatcher.BeginInvoke(new Action(()=>
                    {
                        try{DeskSpacePinnedAppFilesV380.TryOpenQuickArcItem(appTarget,fileTarget,owner);}
                        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                    }));
                }
                return true;
            case "allfiles":
                if(IsLaunchable(item.Path))
                {
                    string appTarget=item.Path;
                    HideArc();
                    owner.Dispatcher.BeginInvoke(new Action(()=>
                    {
                        try{DeskSpacePinnedAppFilesV380.Show(owner,appTarget);}
                        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                    }));
                }
                return true;
            case "pageback":MoveChild(-1);return true;
            case "pagenext":MoveChild(1);return true;
            case "search":HideArc();owner.Dispatcher.BeginInvoke(openSearch);return true;
            case "add":ShowApplicationPinSearch();return true;
        }
        return false;
    }
    private void ShowApplicationPinSearch()
    {
        // Use the circle's EXISTING search-to-bar transformation. A normal
        // WPF TextBox receives actual keyboard focus and filters apps only.
        HideArc();
        owner.Dispatcher.BeginInvoke(new Action(()=>{
            DeskSpaceSearchV11.RequestApplicationPinMode();
            openSearch();
        }),DispatcherPriority.Input);
    }
    // Preserves the shared Search/Applications pin identity.
    private void PinFoundApp(string path)
    {
        if(!IsLaunchable(path))return;
        if(!pinned().Any(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase)))
            addPin(path);
        group="pinned";childPage=0;Render();
    }
    private void Wheel(object sender,MouseWheelEventArgs e)
    {
        if(group!="pinned")return;
        int delta=scrollWheel.Step(e.Delta);
        if(delta!=0)MoveChild(delta);
        e.Handled=true;
    }
    private void Moved(object? sender,EventArgs e)
    {if(arc.IsOpen)Position();}
    private void Resized(object? sender,SizeChangedEventArgs e)
    {
        if(owner.ActualWidth<300||owner.ActualHeight<300)HideArc();
        else if(arc.IsOpen)Position();
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;HideArc();
        owner.LocationChanged-=Moved;owner.SizeChanged-=Resized;
    }
}
