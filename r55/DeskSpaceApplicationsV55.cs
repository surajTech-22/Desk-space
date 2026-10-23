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

// A single parent-attached contextual application donut. Same shared pinned
// paths as Search/WidgetSettingsV3; no independent top-level window.
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
    private readonly Brush normal=new SolidColorBrush(Color.FromRgb(217,229,248));
    private readonly Brush selected=new SolidColorBrush(Color.FromRgb(143,172,235));
    private readonly Brush accent=new SolidColorBrush(Color.FromRgb(78,98,180));
    private const double Center=340,BaseInner=197,BaseOuter=242,SubInner=244,SubOuter=288;
    private int childPage,hovered=-1;
    private string group="";
    private bool disposed;
    private int placementTick;
    // Parent drawing starts each outer wedge at -90 + slot * (360/7).
    // The old helper returned the wedge midpoint half a sector too far left.
    internal static double ApplicationsParentCenter=>
        DeskSpaceDonutGeometryV43.OuterSectorCenter(4)-360.0/14.0;
    private Popup? pinPalette;
    private TextBox? pinSearch;
    private StackPanel? pinMatches;
    private DispatcherTimer? pinRefresh;
    public bool IsArcOpen=>arc.IsOpen || pinPalette?.IsOpen==true;
    public int PinnedCount=>pinned().Count;
    public DeskSpaceApplicationsV55(Window owner,Func<IReadOnlyList<string>> pinned,
        Action<string> addPin,Action<string> removePin,Action openSearch)
    {
        this.owner=owner;this.pinned=pinned;this.addPin=addPin;
        this.removePin=removePin;this.openSearch=openSearch;
        canvas=new Canvas{Width=680,Height=680,Background=null};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
        canvas.MouseWheel+=Wheel;
        arc=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas};
        owner.LocationChanged+=Moved;owner.SizeChanged+=Resized;
    }
    private static Point At(double r,double angle)
    {
        var rad=angle*Math.PI/180.0;
        return new Point(Center+r*Math.Cos(rad),Center+r*Math.Sin(rad));
    }
    private static Geometry Sector(double a,double b,double inner,double outer)
    {
        var figure=new PathFigure{StartPoint=At(outer,a),IsClosed=true};
        figure.Segments.Add(new ArcSegment{Point=At(outer,b),
            Size=new Size(outer,outer),SweepDirection=SweepDirection.Clockwise});
        figure.Segments.Add(new LineSegment(At(inner,b),true));
        figure.Segments.Add(new ArcSegment{Point=At(inner,a),
            Size=new Size(inner,inner),SweepDirection=SweepDirection.Counterclockwise});
        return new PathGeometry(new[]{figure});
    }
    private void AddWedge(string label,string kind,string path,
        double a,double b,double r0,double r1)
    {
        var geom=Sector(a,b,r0,r1);
        int slot=hitZones.Count;
        var shape=new System.Windows.Shapes.Path{Data=geom,
            Fill=kind=="add"?accent:normal,Stroke=Brushes.White,
            StrokeThickness=1.5,Cursor=Cursors.Hand,ToolTip=label,Tag=slot};
        fills.Add(shape);hitZones.Add((geom,kind,path));
        canvas.Children.Add(shape);
        var point=At((r0+r1)*.5,(a+b)*.5);
        double arcWidth=(r0+r1)*.5*Math.Abs(b-a)*Math.PI/180.0;
        double textWidth=Math.Clamp(arcWidth*.91,40.0,90.0);
        var text=new TextBlock{Text=label,Width=textWidth,MaxHeight=30,
            TextWrapping=TextWrapping.Wrap,
            TextAlignment=TextAlignment.Center,
            FontSize=10.5,TextTrimming=TextTrimming.CharacterEllipsis,
            Foreground=kind=="add"?Brushes.White:
                new SolidColorBrush(Color.FromRgb(37,51,79)),
            IsHitTestVisible=false,ToolTip=label};
        Canvas.SetLeft(text,point.X-textWidth/2);Canvas.SetTop(text,point.Y-14);
        canvas.Children.Add(text);
    }
    private List<(string Label,string Kind,string Path)> Children()
    {
        // Use the actual Windows app display name, not the ambiguous EXE
        // basename shown multiple times in the reported screenshots.
        var applications=group=="pinned"
            ? pinned().Where(IsLaunchable)
                .Select(p=>(Name:System.IO.Path.GetFileNameWithoutExtension(p),Path:p))
            : DeskSpaceSearchIndexV13.SnapshotApplications()
                .Where(h=>IsLaunchable(h.OpenTarget))
                .Select(h=>(Name:string.IsNullOrWhiteSpace(h.Name)?
                    System.IO.Path.GetFileNameWithoutExtension(h.OpenTarget):h.Name,
                    Path:h.OpenTarget));
        var distinct=applications
            .GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase)
            .Select(g=>g.First())
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
        return distinct.Skip(childPage*4).Take(4)
            .Select(x=>{
                bool ambiguous=distinct.Count(y=>string.Equals(y.Name,x.Name,
                    StringComparison.OrdinalIgnoreCase))>1;
                var parent=System.IO.Path.GetFileName(
                    System.IO.Path.GetDirectoryName(x.Path)??"");
                var title=ambiguous?x.Name+" ("+parent+")":x.Name;
                return (Label:title,Kind:"launch",Path:x.Path);
            }).ToList();
    }
    private static bool IsLaunchable(string p)
    {
        if(string.IsNullOrWhiteSpace(p)||!File.Exists(p))return false;
        var extension=Path.GetExtension(p);
        return extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".url",StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat",StringComparison.OrdinalIgnoreCase);
    }
    private void Render()
    {
        canvas.Children.Clear();hitZones.Clear();fills.Clear();hovered=-1;
        var roots=new[]{
            ("Pinned","pinned",""),
            ("All apps","installed",""),
            ("+ Pin","add",""),
            ("Search","search","")
        };
        var center=ApplicationsParentCenter;
        // The Applications replacement is outer slot 4 (overall index 10).
        // Wedges are immediately adjacent to the parent's 195-DIP outer
        // radius and share its exact origin; the former 217-DIP opening
        // produced a visibly detached arc, especially on scaled screens.
        // Exactly cover the same 360/7-degree angular sector as the
        // Applications parent. No overlap with Tasks or Clipboard.
        double step=360.0/(7*4);
        double start=center-360.0/14.0;
        for(int i=0;i<roots.Length;i++)
            AddWedge(roots[i].Item1,roots[i].Item2,roots[i].Item3,
                start+i*step,start+(i+1)*step,BaseInner,BaseOuter);
        if(group.Length>0)
        {
            var children=Children();
            if(children.Count==0)
                AddWedge("No apps","empty","",center-16,center+16,SubInner,SubOuter);
            else
            {
                double width=Math.Min(84,children.Count*21);
                double begin=center-width/2;
                for(int i=0;i<children.Count;i++)
                    AddWedge(children[i].Label,"launch",children[i].Path,
                        begin+i*width/children.Count,begin+(i+1)*width/children.Count,
                        SubInner,SubOuter);
            }
            // Keep page actions attached to the circle, not a separate window.
            if(childPage>0)
                AddWedge("‹ Back","pageback","",center-66,center-47,SubInner,SubOuter);
            if((group=="pinned"?pinned().Count:
                DeskSpaceSearchIndexV13.SnapshotApplications().Count)>(childPage+1)*4)
                AddWedge("More ›","pagenext","",center+47,center+66,SubInner,SubOuter);
        }
    }
    private void Position()
    {
        if(disposed)return;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        canvas.LayoutTransform=new ScaleTransform(scale,scale);
        var offset=DeskSpaceDonutGeometryV43.PopupOffset(owner,Center,Center,0,0);
        arc.HorizontalOffset=offset.X+(++placementTick%2)*.001;
        arc.VerticalOffset=offset.Y;
        if(pinPalette?.IsOpen==true)
            PositionPinPalette();
    }
    public void Show()
    {
        if(disposed)return;
        group="";childPage=0;
        Render();Position();arc.IsOpen=owner.IsVisible;
    }
    public void HideArc()
    {
        arc.IsOpen=false;group="";childPage=0;
        if(pinPalette!=null)pinPalette.IsOpen=false;
        pinRefresh?.Stop();
    }
    private int Slot(Point point)
    {
        for(int i=hitZones.Count-1;i>=0;i--)
            if(hitZones[i].Hit.FillContains(point))return i;
        return -1;
    }
    private void Move(object sender,MouseEventArgs e)
    {
        if(disposed||!arc.IsOpen)return;
        Hover(e.GetPosition(canvas));
    }
    private void Hover(Point p)
    {
        int slot=Slot(p);
        if(slot==hovered)return;
        hovered=slot;
        for(int i=0;i<fills.Count;i++)
            fills[i].Fill=i==slot?selected:
                (hitZones[i].Kind=="add"?accent:normal);
        if(slot<0)return;
        string kind=hitZones[slot].Kind;
        if(kind=="pinned"||kind=="installed")
        {
            if(group==kind)return;
            group=kind;childPage=0;
            Render(); // draw second concentric child ring on hover
        }
    }
    private void Down(object sender,MouseButtonEventArgs e)
    {
        if(!arc.IsOpen)return;
        if(Activate(e.GetPosition(canvas)))e.Handled=true;
    }
    public bool TryCapturedDragAtScreen(Point screenPoint,bool released)
    {
        if(disposed||!arc.IsOpen||!canvas.IsLoaded)return false;
        Point local;
        try{local=canvas.PointFromScreen(screenPoint);}
        catch(InvalidOperationException){return false;}
        if(Slot(local)<0)return false;
        if(released)Activate(local);else Hover(local);
        return true;
    }
    private bool Activate(Point point)
    {
        int slot=Slot(point);
        if(slot<0)return false;
        var item=hitZones[slot];
        switch(item.Kind)
        {
            case "pinned":case "installed":
                group=item.Kind;childPage=0;Render();return true;
            case "launch":
                if(IsLaunchable(item.Path))
                {
                    try{Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true});}
                    catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                }
                return true;
            case "pageback":childPage=Math.Max(0,childPage-1);Render();return true;
            case "pagenext":childPage++;Render();return true;
            case "search":HideArc();owner.Dispatcher.BeginInvoke(openSearch);return true;
            case "add":
                // Search discovered applications in our own compact widget
                // panel. Never navigate users into a raw file-picker dialog.
                ShowApplicationPinSearch();
                return true;
        }
        return false;
    }
    private void PositionPinPalette()
    {
        if(pinPalette==null)return;
        double w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        pinPalette.HorizontalOffset=w/2-156;
        pinPalette.VerticalOffset=h/2+140;
    }
    private void EnsurePinPalette()
    {
        if(pinPalette!=null)return;
        var surface=new StackPanel{Width=312,Margin=new Thickness(9,9,9,8)};
        var heading=new DockPanel{LastChildFill=true,
            Margin=new Thickness(0,0,0,8)};
        var close=new Button{Content="×",Width=27,Height=27,
            Padding=new Thickness(0),FontSize=17,
            ToolTip="Close application search"};
        close.Click+=(_,_)=>{if(pinPalette!=null)pinPalette.IsOpen=false;};
        DockPanel.SetDock(close,Dock.Right);
        heading.Children.Add(close);
        heading.Children.Add(new TextBlock{
            Text="Find an application to pin",
            FontSize=14,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});
        surface.Children.Add(heading);
        pinSearch=new TextBox{FontSize=13,Height=33,
            Padding=new Thickness(8,6,6,4),
            ToolTip="Search installed applications by name",
            Background=new SolidColorBrush(Color.FromRgb(248,249,255)),
            Foreground=new SolidColorBrush(Color.FromRgb(28,38,64))};
        pinSearch.TextChanged+=(_,_)=>RefreshPinMatches();
        pinSearch.KeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){if(pinPalette!=null)pinPalette.IsOpen=false;e.Handled=true;}
            else if(e.Key==Key.Enter)
            {
                // Enter chooses the first visible match, same as click.
                var found=ApplicationMatches().FirstOrDefault();
                if(found!=null){PinFoundApp(found.OpenTarget);e.Handled=true;}
            }
        };
        surface.Children.Add(pinSearch);
        pinMatches=new StackPanel{Margin=new Thickness(0,4,0,3)};
        surface.Children.Add(new ScrollViewer{Content=pinMatches,
            MaxHeight=265,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        surface.Children.Add(new TextBlock{
            Text="Choose an installed application. Your files are not opened.",
            FontSize=10,Foreground=Brushes.LightGray,
            Margin=new Thickness(2,6,2,0),
            TextWrapping=TextWrapping.Wrap});
        pinPalette=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,PopupAnimation=PopupAnimation.Fade,
            Child=new Border{Background=new SolidColorBrush(
                    Color.FromRgb(30,40,73)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(136,157,211)),
                BorderThickness=new Thickness(1.2),
                CornerRadius=new CornerRadius(12),Child=surface}};
        pinRefresh=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        pinRefresh.Tick+=(_,_)=>{if(pinPalette?.IsOpen==true)RefreshPinMatches();};
        pinPalette.Opened+=(_,_)=>{pinSearch.Focus();Keyboard.Focus(pinSearch);};
        pinPalette.Closed+=(_,_)=>pinRefresh?.Stop();
    }
    private List<SearchHit> ApplicationMatches()
    {
        var term=pinSearch?.Text.Trim()??"";
        return DeskSpaceSearchIndexV13.SnapshotApplications()
            .Where(a=>IsLaunchable(a.OpenTarget) &&
                (term.Length==0 ||
                 a.Name.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                 a.OpenTarget.Contains(term,StringComparison.OrdinalIgnoreCase)))
            .GroupBy(a=>a.OpenTarget,StringComparer.OrdinalIgnoreCase)
            .Select(g=>g.First())
            .OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase)
            .Take(30).ToList();
    }
    private void RefreshPinMatches()
    {
        if(pinMatches==null)return;
        pinMatches.Children.Clear();
        var matches=ApplicationMatches();
        if(matches.Count==0)
        {
            pinMatches.Children.Add(new TextBlock{
                Text="Searching installed applications… or no matching apps found.",
                Margin=new Thickness(6,12,5,12),
                Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap});
            return;
        }
        var pinnedPaths=pinned();
        foreach(var app in matches)
        {
            bool already=pinnedPaths.Any(p=>string.Equals(p,app.OpenTarget,
                StringComparison.OrdinalIgnoreCase));
            string name=string.IsNullOrWhiteSpace(app.Name)?
                System.IO.Path.GetFileNameWithoutExtension(app.OpenTarget):app.Name;
            var button=new Button{
                Content=(already?"✓  ":"＋  ")+name,
                ToolTip=app.OpenTarget,Height=33,
                FontSize=12,HorizontalContentAlignment=HorizontalAlignment.Left,
                Padding=new Thickness(9,2,8,2),
                Margin=new Thickness(0,2,0,2),
                IsEnabled=!already,
                Foreground=Brushes.White,
                Background=new SolidColorBrush(Color.FromRgb(58,74,122)),
                BorderThickness=new Thickness(0)};
            var path=app.OpenTarget;
            button.Click+=(_,_)=>PinFoundApp(path);
            pinMatches.Children.Add(button);
        }
    }
    private void PinFoundApp(string path)
    {
        if(!IsLaunchable(path))return;
        if(!pinned().Any(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase)))
            addPin(path);
        if(pinPalette!=null)pinPalette.IsOpen=false;
        pinRefresh?.Stop();
        group="pinned";childPage=0;
        Render();Position();arc.IsOpen=owner.IsVisible;
    }
    private void ShowApplicationPinSearch()
    {
        DeskSpaceSearchIndexV13.WarmStart();
        EnsurePinPalette();
        arc.IsOpen=false;
        pinSearch!.Text="";
        RefreshPinMatches();
        PositionPinPalette();
        pinPalette!.IsOpen=owner.IsVisible;
        pinRefresh?.Start();
        pinSearch.Focus();
    }

    private void Wheel(object sender,MouseWheelEventArgs e)
    {
        if(group.Length==0)return;
        childPage=Math.Max(0,childPage+(e.Delta<0?1:-1));Render();e.Handled=true;
    }
    private void Moved(object? sender,EventArgs e){if(arc.IsOpen)Position();}
    private void Resized(object? sender,SizeChangedEventArgs e)
    {
        if(owner.ActualWidth<300||owner.ActualHeight<300)HideArc();
        else if(arc.IsOpen)Position();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;HideArc();
        pinRefresh?.Stop();
        owner.LocationChanged-=Moved;owner.SizeChanged-=Resized;
    }
}
