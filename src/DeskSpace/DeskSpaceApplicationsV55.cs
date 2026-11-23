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
    private const double BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=310;
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
                        ? "Tally Co #"+f.TallyCompanyCode
                        : f.DisplayName;
                    d2Items.Add((lbl,"launchfile",f.FullPath));
                }
                if(d2Items.Count==0 && activeAppLoading)
                    d2Items.Add(("Loading…","loading",""));
                d2Items.Add(("All files…","allfiles",activeAppPath));
                double d2Sweep=Math.Clamp(Math.Max(currentChildView.Step,48.0),28.0,56.0);
                double d2Half=DeskSpaceSubArcGeometryV48.Half(d2Items.Count);
                double d2Center=DeskSpaceSubArcGeometryV48.FitAngle(owner,2,activeAppCenter,d2Half);
                var d2View=DeskSpaceSubDonutStandardV62.Plan(d2Items.Count,0,d2Center,d2Sweep);
                for(int i=0;i<d2View.Count;i++)
                {
                    var d2=d2Items[i];
                    Add(d2.Label,d2.Kind,d2.Path,d2View.From(i),d2View.To(i),2);
                }
            }
        }
        var shapes=items.Select(x=>
            DeskSpaceSubArcGeometryV48.Sector(x.Depth,x.A,x.B)).ToArray();
        bounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        canvas.Width=bounds.Width+margin*2;
        canvas.Height=bounds.Height+margin*2;
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];
            var geometry=DeskSpaceSubArcGeometryV48.Clip(shapes[i],bounds);
            bool isAction=item.Kind is "add" or "allfiles";
            bool isSelectedApp=item.Kind=="launch" && !string.IsNullOrEmpty(activeAppPath) &&
                string.Equals(item.Path,activeAppPath,StringComparison.OrdinalIgnoreCase);
            string tip=item.Kind=="launch"
                ? item.Label+"\nClick: Open app directly · Hover: Recent files · Right-click: All files"
                : item.Kind=="launchfile"
                    ? item.Label+"\nClick to open directly in "+System.IO.Path.GetFileNameWithoutExtension(activeAppPath)+"\n"+item.Path
                    : item.Kind=="allfiles"
                        ? "Open full compatible files browser for "+System.IO.Path.GetFileNameWithoutExtension(item.Path)
                        : item.Label;
            var wedge=new System.Windows.Shapes.Path{
                Data=geometry,Stroke=Brushes.White,StrokeThickness=1.5,
                Fill=isAction?accent:(isSelectedApp?selected:normal),
                Cursor=Cursors.Hand,ToolTip=tip,Tag=i};
            fills.Add(wedge);
            hitZones.Add((geometry,item.Kind,item.Path));
            canvas.Children.Add(wedge);
            var p=DeskSpaceSubArcGeometryV48.Polar(
                (DeskSpaceSubArcGeometryV48.Inner(item.Depth)+
                 DeskSpaceSubArcGeometryV48.Outer(item.Depth))/2,
                (item.A+item.B)/2);
            double arcWidth=(DeskSpaceSubArcGeometryV48.Inner(item.Depth)+
                DeskSpaceSubArcGeometryV48.Outer(item.Depth))*.5*
                (item.B-item.A)*Math.PI/180;
            double textWidth=Math.Clamp(arcWidth*.94,34,item.Depth>=2?96:86);
            var label=new TextBlock{Text=item.Label,Width=textWidth,MaxHeight=27,
                FontSize=item.Depth>=2?9.5:10,
                FontWeight=isAction?FontWeights.SemiBold:FontWeights.Normal,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                IsHitTestVisible=false,ToolTip=tip,
                Foreground=isAction?Brushes.White:
                    new SolidColorBrush(Color.FromRgb(37,51,79))};
            Canvas.SetLeft(label,DeskSpaceSubArcGeometryV48.CX+p.X-
                bounds.Left+margin-textWidth/2);
            // Display the actual Windows app or shortcut icon alongside
            // its readable label; never launch the app to discover artwork.
            double labelY=DeskSpaceSubArcGeometryV48.CY+p.Y-
                bounds.Top+margin-13;
            if(item.Kind=="launch")
            {
                var iconSource=DeskSpaceApplicationIconsV65.Get(item.Path);
                if(iconSource!=null)
                {
                    var appIcon=new Image{Source=iconSource,Width=19,Height=19,
                        Stretch=Stretch.Uniform,IsHitTestVisible=false};
                    Canvas.SetLeft(appIcon,DeskSpaceSubArcGeometryV48.CX+p.X-
                        bounds.Left+margin-9.5);
                    Canvas.SetTop(appIcon,labelY-14);
                    canvas.Children.Add(appIcon);
                    labelY+=8;
                }
            }
            Canvas.SetTop(label,labelY);
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
