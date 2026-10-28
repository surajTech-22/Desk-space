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
    private bool disposed;
    public bool IsArcOpen=>arc.IsOpen;
    public int PinnedCount=>pinned().Count;
    // Main parent outer slot #4 starts at this physical direction (the
    // center-of-label angle, not the geometric midpoint).
    internal static double ApplicationsParentCenter=>
        DeskSpaceDonutGeometryV43.OuterSectorCenter(4)-360.0/14.0;

    public DeskSpaceApplicationsV55(Window owner,Func<IReadOnlyList<string>> pinned,
        Action<string> addPin,Action<string> removePin,Action openSearch)
    {
        this.owner=owner;this.pinned=pinned;this.addPin=addPin;
        this.removePin=removePin;this.openSearch=openSearch;
        canvas=new Canvas{Width=1,Height=1,Background=null};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
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
            var wedge=new System.Windows.Shapes.Path{
                Data=geometry,Stroke=Brushes.White,StrokeThickness=1.5,
                Fill=item.Kind=="add"?accent:normal,
                Cursor=Cursors.Hand,ToolTip=item.Label,Tag=i};
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
            double textWidth=Math.Clamp(arcWidth*.94,34,86);
            var label=new TextBlock{Text=item.Label,Width=textWidth,MaxHeight=27,
                FontSize=10,TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                IsHitTestVisible=false,ToolTip=item.Label,
                Foreground=item.Kind=="add"?Brushes.White:
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
    private void MoveChild(int delta)
    {
        if(group!="pinned")return;
        int next=DeskSpaceSubDonutStandardV62.Advance(currentChildView,delta);
        if(next==childPage)return;
        childPage=next;
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
        group="";childPage=0;
        Render();arc.IsOpen=owner.IsVisible;
    }
    public void HideArc(){arc.IsOpen=false;group="";childPage=0;}
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
            fills[j].Fill=j==i?selected:
                hitZones[j].Kind=="add"?accent:normal;
        if(i<0)return;
        var kind=hitZones[i].Kind;
        if(kind=="pinned"&&group!=kind)
        {group=kind;childPage=0;Render();}
    }
    private void Move(object sender,MouseEventArgs e)
    {
        if(!disposed&&arc.IsOpen)Hover(e.GetPosition(canvas));
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
                    try{Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true});}
                    catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
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
