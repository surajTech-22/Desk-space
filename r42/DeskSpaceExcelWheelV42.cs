using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DeskSpace;

// The Excel segment follows the Information module's contextual child wheel:
// hover = read-only preview; click/drag release = explicit, direct VALUE paste.
// It NEVER opens the Excel mapping editor. Editing lives in Workspace.
internal sealed class DeskSpaceExcelWheelV42 : IDisposable
{
    private readonly Window owner;
    private readonly Action collapseParent;
    private readonly Popup arc,shelf;
    private readonly Canvas canvas;
    private readonly Border shelfBorder;
    private readonly StackPanel shelfBody;
    private readonly TextBlock shelfTitle, shelfMessage;
    private readonly DispatcherTimer pointerTimer,edgeTimer,successTimer,drillTimer;
    private readonly List<(int Index,Guid? Id,Geometry Hit)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> sectors=new();
    private List<DeskSpaceExcelTemplateV16> formats=new();
    private enum ExcelLevel { Functions, Information }
    private ExcelLevel level=ExcelLevel.Functions;
    private string groupPath="";
    private readonly List<(string Label,string Command,string Group,Guid? Id)>
        displayed=new();
    private int start,hovered=-1,pageDirection;
    private double localOriginX,localOriginY;
    private double rootStartAngle,rootStep;
    private Rect rootBounds=Rect.Empty;
    private int drillSlot=-1;
    private int drillDepth=-1; // 0 = Excel parent arc, 1+ = attached rings
    private readonly List<ChildRing> childRings=new();
    private sealed class ChildRing
    {
        internal readonly int Depth;
        internal readonly string Group;
        internal readonly double Angle;
        internal readonly Popup Popup;
        internal readonly Canvas Surface;
        internal readonly List<(string Label,string Command,string Group,Guid? Id)> Items=new();
        internal readonly List<Geometry> Zones=new();
        internal readonly List<System.Windows.Shapes.Path> Wedges=new();
        internal int Start,Hovered=-1;
        internal bool SuppressNextUp;
        internal double OriginX,OriginY,StartAngle,StepAngle;
        internal Rect Bounds=Rect.Empty;
        internal ChildRing(int depth,string group,double angle,Window owner)
        {
            Depth=depth;Group=group;Angle=angle;
            Surface=new Canvas{Background=null,Focusable=true};
            Popup=new Popup{PlacementTarget=owner,
                Placement=PlacementMode.Relative,AllowsTransparency=true,
                StaysOpen=true,Child=Surface};
        }
    }
    // Probe only the already-running Excel process; never start Office.
    // A workbook is validated separately by the existing Excel COM bridge.
    private static bool ExcelProcessExists()
    {
        try
        {
            var processes=Process.GetProcessesByName("EXCEL");
            try { return processes.Length>0; }
            finally { foreach(var process in processes)process.Dispose(); }
        }
        catch { return false; }
    }
    internal static Func<bool> ExcelProbe=ExcelProcessExists;
    public static bool ExcelIsOpen()
    {
        try { return ExcelProbe(); }
        catch { return false; }
    }
    private readonly DispatcherTimer excelTimer;
    private Action<bool>? availabilityChanged;
    private bool? lastExcelState;
    public void SetExcelAvailabilityChanged(Action<bool> callback)
    {
        availabilityChanged=callback;
        RefreshExcelAvailability();
    }
    private void RefreshExcelAvailability()
    {
        if(disposed)return;
        bool open=ExcelIsOpen();
        if(lastExcelState.HasValue && open==lastExcelState.Value)return;
        lastExcelState=open;
        availabilityChanged?.Invoke(open);
        if(shelf.IsOpen && !open)
            shelfMessage.Text="Excel is not open. Saved information can be viewed, "+
                "but capture and paste actions are disabled.";
    }
    private bool disposed,pasting,previewOnly,suppressNextUp;
    private int tick;
    private const int PerPage=4;
    // Excel Functions is the seventh OUTER sector (index 12) of 13.
    // Its child arc follows the outer ring; Information remains at index 6.
    // Align the child arc with that wedge and use the Information wheel
    // positioning, click, pagination and shelf behavior.
    private const double CX=325,CY=325,Outer=260,Inner=207;
    // Main donut positions its OUTER sector label at the sector starting
    // angle, rather than the 7-sector geometric midpoint. Match THAT anchor.
    private static readonly double ArcCenter=
        DeskSpaceDonutGeometryV43.OuterSectorCenter(6)-360.0/14.0;
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(69,88,175));
    private static readonly Brush Idle=new SolidColorBrush(Color.FromRgb(221,234,255));
    private static readonly Brush Selected=new SolidColorBrush(Color.FromRgb(156,184,239));
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(37,51,79));
    public bool IsOpen=>arc.IsOpen || shelf.IsOpen ||
        childRings.Any(r=>r.Popup.IsOpen);
    public bool IsArcOpen=>arc.IsOpen;
    public bool IsPasting=>pasting;
    public int SavedCount=>formats.Count;

    public DeskSpaceExcelWheelV42(Window window,Action onCommit)
    {
        owner=window;collapseParent=onCommit;
        canvas=new Canvas{Width=300,Height=240,
            Background=null,Focusable=true};
        canvas.PreviewMouseLeftButtonDown+=OnDown;
        canvas.PreviewMouseLeftButtonUp+=OnUp;
        canvas.MouseMove+=OnMove;
        canvas.MouseWheel+=OnWheel;
        arc=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas
        };
        shelfTitle=new TextBlock{Foreground=Ink,FontSize=14,
            FontWeight=FontWeights.SemiBold,
            VerticalAlignment=VerticalAlignment.Center,
            TextTrimming=TextTrimming.CharacterEllipsis};
        shelfBody=new StackPanel{Margin=new Thickness(7,3,7,5)};
        shelfMessage=new TextBlock{FontSize=11,
            Foreground=new SolidColorBrush(Color.FromRgb(88,103,126)),
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,3,8,4)};
        var header=new DockPanel{
            LastChildFill=true,Height=33,
            Background=new SolidColorBrush(Color.FromRgb(238,243,253))
        };
        var close=SmallButton("×",Hide);
        close.ToolTip="Close Excel preview";
        DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        header.Children.Add(shelfTitle);
        var shelfScroll=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            MaxHeight=155,Content=shelfBody
        };
        var layout=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        DockPanel.SetDock(shelfMessage,Dock.Bottom);layout.Children.Add(shelfMessage);
        layout.Children.Add(shelfScroll);
        shelfBorder=new Border{
            Width=315,MaxHeight=244,Padding=new Thickness(5),
            Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(194,206,228)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),
            Child=layout,
            Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=13,ShadowDepth=2,Opacity=.17
            }
        };
        shelf=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=shelfBorder
        };
        excelTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        excelTimer.Tick+=(_,_)=>RefreshExcelAvailability();
        pointerTimer=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(
                DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
        pointerTimer.Tick+=(_,_)=>PollPointer();
        drillTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(160)};
        drillTimer.Tick+=(_,_)=>{
            drillTimer.Stop();
            int slot=drillSlot,depth=drillDepth;
            drillSlot=-1;drillDepth=-1;
            if(disposed||pasting||!arc.IsOpen||slot<0)return;
            if(depth==0)
            {
                if(slot>=displayed.Count||hovered!=slot)return;
                if(displayed[slot].Command=="information")
                    ExpandRing(1,"",RootAngle(slot));
            }
            else
            {
                var ring=childRings.FirstOrDefault(x=>x.Depth==depth);
                if(ring==null||!ring.Popup.IsOpen||
                   slot>=ring.Items.Count||ring.Hovered!=slot)return;
                var item=ring.Items[slot];
                if(item.Command=="group")
                    ExpandRing(depth+1,item.Group,RingAngle(ring,slot));
            }
        };
        edgeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(190)};
        edgeTimer.Tick+=(_,_)=>{
            if(!arc.IsOpen || pageDirection==0 || pasting){edgeTimer.Stop();return;}
            Page(pageDirection);
            edgeTimer.Interval=TimeSpan.FromMilliseconds(140);
        };
        successTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
        successTimer.Tick+=(_,_)=>{
            successTimer.Stop();
            if(!disposed)shelf.IsOpen=false;
        };
        owner.LocationChanged+=OnOwnerMoved;
        owner.SizeChanged+=OnOwnerResized;
    }
    private static Button SmallButton(string label,Action action)
    {
        var b=new Button{Content=label,FontSize=12,MinHeight=27,
            MinWidth=28,Padding=new Thickness(6,3,6,3),
            Margin=new Thickness(3,1,3,1),
            Background=new SolidColorBrush(Color.FromRgb(238,243,253)),
            Foreground=Ink,BorderThickness=new Thickness(0),
            Cursor=Cursors.Hand};
        b.Click+=(_,_)=>action();return b;
    }
    // Follow DeskSpaceInformationV36's circle: a bounded partial arc at
    // radial depth zero, then successive partial arcs at adjacent radii.
    // The parent and all descendants share ONE logical circle centre.
    private static double Span(int count)=>DeskSpaceSubArcGeometryV48.Half(count);
    private double RootAngle(int slot)=>rootStartAngle+
        (slot+.5)*rootStep;
    private static double RingAngle(ChildRing ring,int slot)=>
        ring.StartAngle+(slot+.5)*ring.StepAngle;

    private void CloseFrom(int depth)
    {
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
        foreach(var ring in childRings.Where(r=>r.Depth>=depth).ToArray())
        {
            ring.Popup.IsOpen=false;
            ring.Surface.Children.Clear();
            childRings.Remove(ring);
        }
    }
    private List<(string Label,string Command,string Group,Guid? Id)>
        BranchItems(string selectedGroup)
    {
        var entries=new List<(string Label,string Command,string Group,Guid? Id)>();
        var groups=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var format in formats)
        {
            var path=Group(format);
            string tail;
            if(selectedGroup.Length==0)tail=path;
            else if(path.StartsWith(selectedGroup+"/",StringComparison.OrdinalIgnoreCase))
                tail=path.Substring(selectedGroup.Length+1);
            else continue;
            if(tail.Length>0)groups.Add(tail.Split('/')[0]);
        }
        foreach(var group in groups)
            entries.Add(("▸ "+group,"group",
                selectedGroup.Length==0?group:selectedGroup+"/"+group,null));
        foreach(var format in formats.Where(x=>
            string.Equals(Group(x),selectedGroup,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase))
            entries.Add((format.Name,"mapping",selectedGroup,format.Id));
        entries.Add(("+ Manage","manage",selectedGroup,null));
        return entries;
    }
    private void ExpandRing(int depth,string group,double angle)
    {
        if(disposed||!arc.IsOpen||pasting)return;
        CloseFrom(depth);
        // Opening a submenu must NEVER move the main donut or the user's
        // pointer. The child arc is positioned in its parent's coordinates.
        // Never silently flatten deep groups into the preceding donut.
        var ring=new ChildRing(depth,group,angle,owner);
        childRings.Add(ring);
        RenderRing(ring);
        ring.Surface.MouseMove+=(_,e)=>HoverRing(ring,e.GetPosition(ring.Surface));
        ring.Surface.PreviewMouseLeftButtonDown+=(_,e)=>{
            int slot=RingSlot(ring,e.GetPosition(ring.Surface));
            if(slot<0)return;
            ring.SuppressNextUp=true;
            ActivateRing(ring,slot);e.Handled=true;
        };
        ring.Surface.PreviewMouseLeftButtonUp+=(_,e)=>{
            if(ring.SuppressNextUp){ring.SuppressNextUp=false;e.Handled=true;return;}
            int slot=RingSlot(ring,e.GetPosition(ring.Surface));
            if(slot>=0){ActivateRing(ring,slot);e.Handled=true;}
        };
        ring.Surface.MouseWheel+=(_,e)=>{
            PageRing(ring,e.Delta<0?1:-1);e.Handled=true;
        };
        ring.Popup.IsOpen=true;
    }
    private void RenderRing(ChildRing ring)
    {
        var all=BranchItems(ring.Group);
        if(ring.Depth>8)
            throw new InvalidOperationException(
                "Deep Excel groups require opening Workspace for editing.");
        ring.Start=Math.Clamp(ring.Start,0,Math.Max(0,all.Count-PerPage));
        ring.Items.Clear();
        ring.Items.AddRange(all.Skip(ring.Start).Take(PerPage));
        ring.Surface.Children.Clear();ring.Zones.Clear();ring.Wedges.Clear();
        ring.Hovered=-1;
        int count=ring.Items.Count;
        if(count==0)return;
        // Information's exact half-sweep and relative WPF Popup positioning.
        double half=DeskSpaceSubArcGeometryV48.Half(count);
        double centre=DeskSpaceSubArcGeometryV48.FitAngle(
            owner,ring.Depth,ring.Angle,half);
        ring.StartAngle=centre-half;
        ring.StepAngle=2*half/count;
        var shapes=new List<Geometry>();
        for(int i=0;i<count;i++)
            shapes.Add(DeskSpaceSubArcGeometryV48.Sector(
                ring.Depth,ring.StartAngle+i*ring.StepAngle,
                ring.StartAngle+(i+1)*ring.StepAngle));
        ring.Bounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        ring.OriginX=ring.Bounds.Left;ring.OriginY=ring.Bounds.Top;
        ring.Surface.Width=ring.Bounds.Width+2*margin;
        ring.Surface.Height=ring.Bounds.Height+2*margin;
        for(int i=0;i<count;i++)
        {
            var item=ring.Items[i];
            var hit=DeskSpaceSubArcGeometryV48.Clip(shapes[i],ring.Bounds);
            var wedge=new System.Windows.Shapes.Path{
                Data=hit,Stroke=Brushes.White,StrokeThickness=2,
                Fill=item.Command=="mapping"?Idle:Accent,
                Cursor=Cursors.Hand,ToolTip=item.Label
            };
            ring.Surface.Children.Add(wedge);
            ring.Wedges.Add(wedge);ring.Zones.Add(hit);
            double middle=ring.StartAngle+(i+.5)*ring.StepAngle;
            var p=DeskSpaceSubArcGeometryV48.Polar(
                (DeskSpaceSubArcGeometryV48.Inner(ring.Depth)+
                 DeskSpaceSubArcGeometryV48.Outer(ring.Depth))/2,middle);
            var label=new TextBlock{
                Text=item.Label,Width=80,FontSize=10,
                Foreground=item.Command=="mapping"?Ink:Brushes.White,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=item.Label,IsHitTestVisible=false
            };
            Canvas.SetLeft(label,DeskSpaceSubArcGeometryV48.CX+p.X-
                ring.Bounds.Left+margin-40);
            Canvas.SetTop(label,DeskSpaceSubArcGeometryV48.CY+p.Y-
                ring.Bounds.Top+margin-9);
            ring.Surface.Children.Add(label);
        }
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
        PositionRing(ring);
    }
    private void PositionRing(ChildRing ring)
    {
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        ring.Surface.LayoutTransform=new ScaleTransform(scale,scale);
        var p=DeskSpaceSubArcGeometryV48.PopupOffset(owner,ring.Bounds);
        ring.Popup.HorizontalOffset=p.X;
        ring.Popup.VerticalOffset=p.Y;
    }
    private int RingSlot(ChildRing ring,Point p)
    {
        for(int i=0;i<ring.Zones.Count;i++)
            if(ring.Zones[i].FillContains(p))return i;
        return -1;
    }
    private void PageRing(ChildRing ring,int dir)
    {
        ring.Start=Math.Clamp(ring.Start+dir,0,
            Math.Max(0,BranchItems(ring.Group).Count-PerPage));
        CloseFrom(ring.Depth+1);
        RenderRing(ring);
    }
    private void HoverRing(ChildRing ring,Point p)
    {
        if(!ring.Popup.IsOpen||pasting)return;
        int slot=RingSlot(ring,p);
        if(slot==ring.Hovered)return;
        // Hovering a sibling only closes deeper descendants of this ring.
        CloseFrom(ring.Depth+1);
        ring.Hovered=slot;
        for(int i=0;i<ring.Wedges.Count;i++)
            ring.Wedges[i].Fill=i==slot?Selected:
                (ring.Items[i].Command=="mapping"?Idle:Accent);
        if(slot<0)return;
        var entry=ring.Items[slot];
        if(entry.Command=="group")
        {
            drillSlot=slot;drillDepth=ring.Depth;
            drillTimer.Start();
        }
        else if(entry.Command=="mapping")
        {
            var item=formats.FirstOrDefault(x=>x.Id==entry.Id);
            if(item!=null)Preview(item);
        }
    }
    private void ActivateRing(ChildRing ring,int slot)
    {
        if(!ring.Popup.IsOpen||slot<0||slot>=ring.Items.Count||pasting)return;
        var item=ring.Items[slot];
        if(item.Command=="group")
        {
            ExpandRing(ring.Depth+1,item.Group,RingAngle(ring,slot));
            return;
        }
        if(item.Command=="manage")
        {
            Status("Manage Excel Information",
                "Use Workspace → Excel Mapping to create or edit collections and data.");
            return;
        }
        if(item.Command!="mapping" || !item.Id.HasValue)return;
        var format=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(format==null)return;
        if(!ExcelIsOpen()){Preview(format);return;}
        HideArc();collapseParent();_=PasteAsync(format);
    }

    private void Position()
    {
        if(disposed)return;
        double w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        if(w<=0||h<=0||rootBounds.IsEmpty)return;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        canvas.LayoutTransform=new ScaleTransform(scale,scale);
        var p=DeskSpaceSubArcGeometryV48.PopupOffset(owner,rootBounds);
        arc.HorizontalOffset=p.X;arc.VerticalOffset=p.Y;
        // Identical reference behavior to Information: shelf BELOW the
        // whole circle, NOT between branch levels or on top of the wheel.
        Rect screen=SystemParameters.WorkArea;
        double panelWidth=shelfBorder.Width;
        double left=(w-panelWidth)/2;
        if(owner.Left+left<screen.Left+8)
            left+=screen.Left+8-(owner.Left+left);
        if(owner.Left+left+panelWidth>screen.Right-8)
            left-=owner.Left+left+panelWidth-(screen.Right-8);
        double bottom=owner.Top+h+5+shelfBorder.MaxHeight;
        double top=bottom>screen.Bottom-8?
            -shelfBorder.MaxHeight-6:h+5;
        shelf.HorizontalOffset=left+(++tick%2)*.001;
        shelf.VerticalOffset=top+(tick%2)*.001;
        foreach(var ring in childRings)PositionRing(ring);
    }
    private void OnOwnerMoved(object? sender,EventArgs e)=>Position();
    private void OnOwnerResized(object? sender,SizeChangedEventArgs e)
    {
        if(owner.ActualWidth<300 || owner.ActualHeight<300)
        {
            HideArc(); // Parent collapsed: no detached Popup left on desktop.
            return;
        }
        Position();
    }

    public void Show()
    {
        if(disposed||pasting)return;
        try
        {
            formats=DeskSpaceExcelStoreV16.Load()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
            level=ExcelLevel.Functions;
            groupPath="";
            start=0;
            suppressNextUp=false;
            Render();
            Position();
            arc.IsOpen=true;
            pointerTimer.Start();
            excelTimer.Start();
            lastExcelState=null;
            RefreshExcelAvailability();
        }
        catch(Exception ex)
        {
            Status("Unable to load Excel formats",ex.Message);
        }
    }
    public void HideArc()
    {
        pointerTimer.Stop();edgeTimer.Stop();drillTimer.Stop();excelTimer.Stop();drillSlot=-1;drillDepth=-1;pageDirection=0;
        CloseFrom(1);
        suppressNextUp=false;
        arc.IsOpen=false;hovered=-1;
    }
    public void Hide()
    {
        if(pasting)return;
        HideArc();shelf.IsOpen=false;
    }
    private static string Group(DeskSpaceExcelTemplateV16 entry)=>
        string.Join("/",(entry.GroupPath??"").Replace('\\','/')
            .Split('/',StringSplitOptions.RemoveEmptyEntries|
                StringSplitOptions.TrimEntries));

    private List<(string Label,string Command,string Group,Guid? Id)> Entries()
    {
        var entries=new List<(string Label,string Command,string Group,Guid? Id)>();
        if(level==ExcelLevel.Functions)
        {
            entries.Add(("Clipboard","clipboard","",null));
            entries.Add(("Information","information","",null));
            return entries;
        }
        var childGroups=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var format in formats)
        {
            var g=Group(format);
            string remainder;
            if(groupPath.Length==0)remainder=g;
            else if(g.StartsWith(groupPath+"/",StringComparison.OrdinalIgnoreCase))
                remainder=g.Substring(groupPath.Length+1);
            else continue;
            if(remainder.Length>0)
                childGroups.Add(remainder.Split('/')[0]);
        }
        foreach(var group in childGroups)
            entries.Add(("▸ "+group,"group",
                groupPath.Length==0?group:groupPath+"/"+group,null));
        foreach(var entry in formats.Where(x=>
            string.Equals(Group(x),groupPath,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase))
            entries.Add((entry.Name,"mapping",groupPath,entry.Id));
        entries.Add(("← Back","back","",null));
        entries.Add(("+ Manage","manage","",null));
        return entries;
    }
    private void Render()
    {
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
        CloseFrom(1);
        canvas.Children.Clear();sectors.Clear();hitZones.Clear();displayed.Clear();
        hovered=-1;
        var all=Entries();
        start=Math.Clamp(start,0,Math.Max(0,all.Count-PerPage));
        displayed.AddRange(all.Skip(start).Take(PerPage));
        int count=displayed.Count;
        if(count==0)return;
        // Same small partial sector arc used by the working Information
        // submenu, centered precisely on Excel's existing outer wedge.
        double half=DeskSpaceSubArcGeometryV48.Half(count);
        rootStartAngle=ArcCenter-half;
        rootStep=2*half/count;
        var shapes=new List<Geometry>();
        for(int i=0;i<count;i++)
            shapes.Add(DeskSpaceSubArcGeometryV48.Sector(
                0,rootStartAngle+i*rootStep,
                rootStartAngle+(i+1)*rootStep));
        rootBounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        canvas.Width=rootBounds.Width+2*margin;
        canvas.Height=rootBounds.Height+2*margin;
        for(int i=0;i<count;i++)
        {
            var item=displayed[i];
            var hit=DeskSpaceSubArcGeometryV48.Clip(shapes[i],rootBounds);
            var wedge=new System.Windows.Shapes.Path{
                Data=hit,Stroke=Brushes.White,StrokeThickness=2,
                Fill=item.Command=="mapping"?Idle:Accent,
                Tag=i,Cursor=Cursors.Hand,ToolTip=item.Label
            };
            canvas.Children.Add(wedge);sectors.Add(wedge);
            hitZones.Add((i,item.Id,hit));
            double angle=rootStartAngle+(i+.5)*rootStep;
            var p=DeskSpaceSubArcGeometryV48.Polar(
                (DeskSpaceSubArcGeometryV48.Inner(0)+
                 DeskSpaceSubArcGeometryV48.Outer(0))/2,angle);
            var label=new TextBlock{
                Text=item.Label,Width=80,FontSize=10,
                Foreground=item.Command=="mapping"?Ink:Brushes.White,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=item.Label,IsHitTestVisible=false
            };
            Canvas.SetLeft(label,DeskSpaceSubArcGeometryV48.CX+
                p.X-rootBounds.Left+margin-40);
            Canvas.SetTop(label,DeskSpaceSubArcGeometryV48.CY+
                p.Y-rootBounds.Top+margin-9);
            canvas.Children.Add(label);
        }
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
        Position();
    }
    private int Slot(Point p)
    {
        foreach(var zone in hitZones)
            if(zone.Hit.FillContains(p))return zone.Index;
        return -1;
    }
    private bool CanPage(int dir)=>dir<0
        ?start>0:start+PerPage<Entries().Count;
    private void Page(int dir)
    {
        if(!CanPage(dir)){edgeTimer.Stop();pageDirection=0;return;}
        start=Math.Clamp(start+dir,0,Math.Max(0,Entries().Count-PerPage));
        Render();
        if(!CanPage(dir))edgeTimer.Stop();
    }
    private void OnWheel(object sender,MouseWheelEventArgs e)
    {
        if(!arc.IsOpen || pasting)return;
        Page(e.Delta<0?1:-1);e.Handled=true;
    }
    private void PollPointer()
    {
        if(!arc.IsOpen || !canvas.IsLoaded || disposed)return;
        try
        {
            // Deepest visible child gets pointer priority; the main wedge
            // must not close a branch merely because the pointer left it.
            foreach(var ring in childRings.OrderByDescending(x=>x.Depth).ToArray())
            {
                if(!ring.Popup.IsOpen||!ring.Surface.IsLoaded)continue;
                Point p=DeskSpaceWheelHoverV35.MouseIn(ring.Surface);
                if(RingSlot(ring,p)>=0){HoverRing(ring,p);return;}
            }
            Hover(DeskSpaceWheelHoverV35.MouseIn(canvas));
        }
        catch(InvalidOperationException){}
    }
    private void OnMove(object sender,MouseEventArgs e)
    {
        if(arc.IsOpen)Hover(e.GetPosition(canvas));
    }
    private void Hover(Point p)
    {
        if(disposed||!arc.IsOpen||pasting)return;
        int slot=Slot(p);
        if(slot==hovered)return;
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
        if(slot>=0)CloseFrom(1);
        hovered=slot;
        for(int i=0;i<sectors.Count;i++)
        {
            bool command=displayed[i].Command!="mapping";
            sectors[i].Fill=i==slot?Selected:(command?Accent:Idle);
        }
        if(slot<0||slot>=displayed.Count)return;
        var item=displayed[slot];
        // Information and grouped categories are navigable on HOVER:
        // same behavior as the rest of the DeskSpace parent/child donut.
        // Never paste merely because the pointer hovers a mapping.
        if(item.Command=="information"||item.Command=="group")
        {
            shelf.IsOpen=false;
            drillSlot=slot;drillDepth=0;
            drillTimer.Start();
        }
        else if(item.Command=="mapping")
        {
            var format=formats.FirstOrDefault(x=>x.Id==item.Id);
            if(format!=null)Preview(format);
        }
        else if(item.Command=="clipboard")
            shelf.IsOpen=false; // leaf action still requires explicit selection
        else if(item.Command=="back")
            shelf.IsOpen=false; // click Back; hover must not bounce up/down
        else
            shelf.IsOpen=false;
    }
    private void OnDown(object sender,MouseButtonEventArgs e)
    {
        if(!arc.IsOpen||pasting)return;
        int slot=Slot(e.GetPosition(canvas));
        if(slot<0)return;
        // Navigation redraws the donut on mouse-down. The matching mouse-up
        // must never activate the newly drawn child mapping at the same
        // coordinate, which would be an unintended Excel paste.
        suppressNextUp=true;
        Activate(slot);
        e.Handled=true;
    }
    private void OnUp(object sender,MouseButtonEventArgs e)
    {
        if(suppressNextUp)
        {
            suppressNextUp=false;
            e.Handled=true;
            return;
        }
        // Touch and accessibility devices can raise an up-only gesture.
        if(!arc.IsOpen||pasting||e.Handled)return;
        int slot=Slot(e.GetPosition(canvas));
        if(slot<0)return;
        Activate(slot);
        e.Handled=true;
    }
    public bool TryCapturedDragAtScreen(Point p,bool released)
    {
        if(disposed||!arc.IsOpen||!canvas.IsLoaded||pasting)return false;
        Point local;
        try{local=canvas.PointFromScreen(p);}
        catch(InvalidOperationException){return false;}
        foreach(var ring in childRings.OrderByDescending(x=>x.Depth).ToArray())
        {
            if(!ring.Popup.IsOpen||!ring.Surface.IsLoaded)continue;
            Point childPoint;
            try {childPoint=ring.Surface.PointFromScreen(p);}
            catch(InvalidOperationException){continue;}
            int selected=RingSlot(ring,childPoint);
            if(selected<0)continue;
            HoverRing(ring,childPoint);
            if(released)ActivateRing(ring,selected);
            return true;
        }
        int slot=Slot(local);
        if(slot<0)return false;
        Hover(local);
        if(released)Activate(slot);
        return true;
    }
    private void Activate(int slot)
    {
        if(disposed||pasting||slot<0||slot>=displayed.Count)return;
        var item=displayed[slot];
        if(item.Command=="clipboard")
        {
            if(!ExcelIsOpen())
            {
                Status("Excel is not open",
                    "Excel Clipboard actions are disabled. You can still "+
                    "open Information to view saved mappings and values.");
                return;
            }
            HideArc();collapseParent();
            DeskSpaceExcelClipboardV39.ShowActive();
            return;
        }
        if(item.Command=="information")
        {
            ExpandRing(1,"",RootAngle(slot));
            return;
        }
        if(item.Command=="group")
        {
            groupPath=item.Group;start=0;Render();
            return;
        }
        if(item.Command=="back")
        {
            if(groupPath.Length>0)
            {
                int cut=groupPath.LastIndexOf('/');
                groupPath=cut<0?"":groupPath.Substring(0,cut);
            }
            else level=ExcelLevel.Functions;
            start=0;Render();
            return;
        }
        if(item.Command=="manage")
        {
            Status("Manage Excel Information",
                "Open Workspace → Excel Mapping to add, edit or group mappings.");
            return;
        }
        if(item.Command!="mapping" || !item.Id.HasValue)return;
        var entry=formats.FirstOrDefault(f=>f.Id==item.Id.Value);
        if(entry==null)return;
        if(!ExcelIsOpen()){Preview(entry);return;}
        HideArc();collapseParent();
        _=PasteAsync(entry);
    }
    private void Preview(DeskSpaceExcelTemplateV16 entry)
    {
        shelfTitle.Text=entry.Name;
        shelfBody.Children.Clear();
        var count=0;
        foreach(var value in entry.Cells.Where(c=>
            c.Role=="Value" && c.ValueKind!="formula" &&
            c.ValueKind!="blank").Take(5))
        {
            var header=entry.Cells.FirstOrDefault(h=>
                h.Role=="Reference"&&
                (string.Equals(h.CellAddress,value.HeaderCellAddress,
                    StringComparison.OrdinalIgnoreCase) ||
                (h.ColumnOffset==value.ColumnOffset &&
                 h.RowOffset<value.RowOffset)))?.RawValue??"Input";
            var row=new Grid{Margin=new Thickness(2,2,2,4)};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(120)});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var label=new TextBlock{
                Text=header,Foreground=Ink,FontWeight=FontWeights.SemiBold,
                FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=header
            };
            row.Children.Add(label);
            var valueText=new TextBlock{
                Text=DeskSpaceExcelActionsV46.PreviewText(value,DateTime.Now),
                FontSize=11,Foreground=Ink,TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=value.CellAddress+" = "+value.Display};
            Grid.SetColumn(valueText,1);row.Children.Add(valueText);
            shelfBody.Children.Add(row);count++;
        }
        shelfMessage.Text=!ExcelIsOpen()
            ? "Excel is not open. Viewing saved data only; capture and paste "+
              "require a running Excel application."
            : entry.CoordinatesVerified
            ? "Click this saved format to paste its "+entry.Cells.Count+
              " mapped cells as VALUES into the matching open Excel sheet. "+
              "Existing formulas and styles stay intact; a backup is created."
            : "This screenshot-only format has no verified Excel addresses. "+
              "Link it to live Excel cells in Workspace before using direct paste.";
        Position();
        shelf.IsOpen=true;
    }
    private void Status(string title,string message)
    {
        successTimer.Stop();
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        shelfMessage.Text=message;
        Position();
        shelf.IsOpen=true;
    }
    private async System.Threading.Tasks.Task PasteAsync(DeskSpaceExcelTemplateV16 entry)
    {
        if(disposed)return;
        if(!ExcelIsOpen()){Preview(entry);return;}
        pasting=true;
        Status("Excel paste · "+entry.Name,
            "Locating matching labels and protecting Excel formulas…");
        bool applying=false;
        try
        {
            if(!entry.CoordinatesVerified)
                throw new InvalidOperationException(
                    "This saved format came from a screenshot and has no "+
                    "verified destination addresses. Link it in Workspace first.");
            if(entry.Cells.All(c=>c.Role!="Value" ||
                c.ValueKind=="formula" || c.ValueKind=="blank"))
                throw new InvalidOperationException(
                    "This format has no approved input values to paste.");
            // Automatic matching is strictly required. Never assume the
            // currently selected Excel cell is the right destination.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
                selection:false,overwrite:true);
            if(!preview.Verified)
                throw new InvalidOperationException(
                    "The open Excel sheet did not uniquely match the "+
                    "saved reference cells. No changes were made. "+
                    preview.Reason);
            int ready=preview.Changes.Count(x=>x.CanApply &&
                x.Source.Role=="Value" &&
                x.Source.ValueKind!="formula" &&
                x.Source.ValueKind!="blank");
            if(ready==0)
                throw new InvalidOperationException(
                    "No safe input cells found. Existing formulas and "+
                    "protected cells were not changed.");
            Status("Excel paste · "+entry.Name,
                "Matched "+preview.SheetName+". Backing up the workbook and "+
                "pasting "+ready+" values…");
            applying=true;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                preview,entry,overwrite:true);
            if(disposed)return;
            Status("Excel values pasted",
                ready+" input values were pasted into "+preview.SheetName+
                ". Excel formatting and formulas were retained. "+
                "Original workbook not auto-saved; review and save in Excel. "+
                "Backup: "+backup);
            // Three seconds only for confirmed Excel paste success.
            // Blocked paste and other modules' messages are never timed out.
            successTimer.Start();
        }
        catch(Exception ex)
        {
            if(!disposed)
                Status("Excel paste blocked",
                    ex.Message+(applying
                        ? " Check the target workbook and its backup. "+
                          "A partially completed write may require recovery."
                        : " No workbook changes were made."));
        }
        finally {pasting=false;}
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        pointerTimer.Stop();edgeTimer.Stop();drillTimer.Stop();successTimer.Stop();excelTimer.Stop();
        owner.LocationChanged-=OnOwnerMoved;
        owner.SizeChanged-=OnOwnerResized;
        CloseFrom(1);
        arc.IsOpen=false;shelf.IsOpen=false;
    }
}
