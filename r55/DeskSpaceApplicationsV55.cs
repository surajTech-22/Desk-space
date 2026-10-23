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
    private const double Center=340,BaseInner=217,BaseOuter=268,SubInner=273,SubOuter=322;
    private int page,childPage,hovered=-1;
    private string group="";
    private bool disposed;
    private int placementTick;
    public bool IsArcOpen=>arc.IsOpen;
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
        var text=new TextBlock{Text=label,Width=76,TextAlignment=TextAlignment.Center,
            FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,
            Foreground=kind=="add"?Brushes.White:
                new SolidColorBrush(Color.FromRgb(37,51,79)),
            IsHitTestVisible=false,ToolTip=label};
        Canvas.SetLeft(text,point.X-38);Canvas.SetTop(text,point.Y-9);
        canvas.Children.Add(text);
    }
    private List<(string Label,string Kind,string Path)> Children()
    {
        var items=(group=="pinned"
            ? pinned().Where(IsLaunchable)
            : DeskSpaceSearchIndexV13.SnapshotApplications()
                .Select(x=>x.OpenTarget).Where(IsLaunchable))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x=>System.IO.Path.GetFileNameWithoutExtension(x),
                StringComparer.OrdinalIgnoreCase).ToList();
        return items.Skip(childPage*4).Take(4)
            .Select(p=>(System.IO.Path.GetFileNameWithoutExtension(p),"launch",p))
            .ToList();
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
            ("Installed","installed",""),
            ("+ Pin","add",""),
            ("Search","search","")
        };
        var center=DeskSpaceDonutGeometryV43.OuterSectorCenter(4);
        // The Applications replacement is outer slot 4 (overall index 10).
        double start=center-51;
        for(int i=0;i<roots.Length;i++)
            AddWedge(roots[i].Item1,roots[i].Item2,roots[i].Item3,
                start+i*25.5,start+(i+1)*25.5,BaseInner,BaseOuter);
        if(group.Length>0)
        {
            var children=Children();
            if(children.Count==0)
                AddWedge("No apps","empty","",center-16,center+16,SubInner,SubOuter);
            else
            {
                double width=Math.Min(90,children.Count*23);
                double begin=center-width/2;
                for(int i=0;i<children.Count;i++)
                    AddWedge(children[i].Label,"launch",children[i].Path,
                        begin+i*width/children.Count,begin+(i+1)*width/children.Count,
                        SubInner,SubOuter);
            }
            // Keep page actions attached to the circle, not a separate window.
            if(childPage>0)
                AddWedge("‹ Back","pageback","",center-66,center-47,SubInner,SubOuter);
            if(Children().Count==4)
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
    }
    public void Show()
    {
        if(disposed)return;
        group="";childPage=0;page=0;
        Render();Position();arc.IsOpen=owner.IsVisible;
    }
    public void HideArc()
    {
        arc.IsOpen=false;group="";childPage=0;
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
                // Explicit user-selected executable/shortcut; never automatically
                // execute discovered applications or change registration.
                var chooser=new Microsoft.Win32.OpenFileDialog{
                    Title="Pin an application to DeskSpace",
                    Filter="Apps and shortcuts|*.exe;*.lnk;*.url;*.bat|All files|*.*",
                    CheckFileExists=true,Multiselect=false};
                if(chooser.ShowDialog(owner)==true && IsLaunchable(chooser.FileName))
                {addPin(chooser.FileName);group="pinned";childPage=0;Render();}
                return true;
        }
        return false;
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
        owner.LocationChanged-=Moved;owner.SizeChanged-=Resized;
    }
}
