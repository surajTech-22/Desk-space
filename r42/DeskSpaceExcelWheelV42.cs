using System;
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
    private readonly DispatcherTimer pointerTimer,edgeTimer;
    private readonly List<(int Index,Guid? Id,Geometry Hit)> hitZones=new();
    private readonly List<Path> sectors=new();
    private List<DeskSpaceExcelTemplateV16> formats=new();
    private int start,hovered=-1,pageDirection;
    private bool disposed,pasting,previewOnly;
    private int tick;
    private const int PerPage=4;
    // The Excel wedge is the seventh inner sector of the 14-sector donut.
    // Align the child arc with that wedge and use the Information wheel
    // positioning, click, pagination and shelf behavior.
    private const double CX=325,CY=325,Outer=260,Inner=207;
    private const double ArcCenter=-90+6*(360.0/7.0);
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(69,88,175));
    private static readonly Brush Idle=new SolidColorBrush(Color.FromRgb(221,234,255));
    private static readonly Brush Selected=new SolidColorBrush(Color.FromRgb(156,184,239));
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(37,51,79));
    public bool IsOpen=>arc.IsOpen || shelf.IsOpen;
    public bool IsArcOpen=>arc.IsOpen;
    public bool IsPasting=>pasting;
    public int SavedCount=>formats.Count;

    public DeskSpaceExcelWheelV42(Window window,Action onCommit)
    {
        owner=window;collapseParent=onCommit;
        canvas=new Canvas{Width=650,Height=650,
            Background=Brushes.Transparent,Focusable=true};
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
        pointerTimer=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(
                DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
        pointerTimer.Tick+=(_,_)=>PollPointer();
        edgeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(190)};
        edgeTimer.Tick+=(_,_)=>{
            if(!arc.IsOpen || pageDirection==0 || pasting){edgeTimer.Stop();return;}
            Page(pageDirection);
            edgeTimer.Interval=TimeSpan.FromMilliseconds(140);
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
    private static Geometry Sector(double a,double b)
    {
        static Point Pt(double radius,double degrees)
        {
            double rad=degrees*Math.PI/180;
            return new Point(CX+radius*Math.Cos(rad),
                             CY+radius*Math.Sin(rad));
        }
        var f=new PathFigure{StartPoint=Pt(Outer,a),IsClosed=true};
        f.Segments.Add(new ArcSegment{
            Point=Pt(Outer,b),Size=new Size(Outer,Outer),
            SweepDirection=SweepDirection.Clockwise});
        f.Segments.Add(new LineSegment(Pt(Inner,b),true));
        f.Segments.Add(new ArcSegment{
            Point=Pt(Inner,a),Size=new Size(Inner,Inner),
            SweepDirection=SweepDirection.Counterclockwise});
        return new PathGeometry(new[]{f});
    }
    private void Position()
    {
        if(disposed)return;
        var w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        var h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        if(w<=0||h<=0)return;
        arc.HorizontalOffset=w/2-CX;
        arc.VerticalOffset=h/2-CY;
        shelf.HorizontalOffset=(w-shelfBorder.Width)/2+(++tick%2)*.001;
        shelf.VerticalOffset=h+5;
    }
    private void OnOwnerMoved(object? sender,EventArgs e)=>Position();
    private void OnOwnerResized(object? sender,SizeChangedEventArgs e)=>Position();

    public void Show()
    {
        if(disposed||pasting)return;
        try
        {
            formats=DeskSpaceExcelStoreV16.Load()
                .OrderByDescending(x=>x.CreatedUtc).ThenBy(x=>x.Name).ToList();
            start=Math.Clamp(start,0,Math.Max(0,formats.Count-PerPage));
            Render();
            Position();
            arc.IsOpen=true;
            pointerTimer.Start();
        }
        catch(Exception ex)
        {
            Status("Unable to load Excel formats",ex.Message);
        }
    }
    public void HideArc()
    {
        pointerTimer.Stop();edgeTimer.Stop();pageDirection=0;
        arc.IsOpen=false;hovered=-1;
    }
    public void Hide()
    {
        if(pasting)return;
        HideArc();shelf.IsOpen=false;
    }
    private void Render()
    {
        canvas.Children.Clear();sectors.Clear();hitZones.Clear();hovered=-1;
        var visible=formats.Skip(start).Take(PerPage).ToArray();
        int count=visible.Length+1;
        double half=count==1?15:Math.Min(57,12.0*count);
        double step=half*2/count;
        for(int i=0;i<count;i++)
        {
            bool newItem=i==visible.Length;
            Guid? id=newItem?null:visible[i].Id;
            string full=newItem?"+ Manage":visible[i].Name;
            double a=ArcCenter-half+i*step,b=a+step;
            var geometry=Sector(a,b);
            var shape=new Path{
                Data=geometry,Stroke=Brushes.White,StrokeThickness=2,
                Fill=newItem?Accent:Idle,Tag=i,
                Cursor=Cursors.Hand,ToolTip=newItem?
                   "Edit or create mappings in the Workspace's Excel Mapping tab":
                   full+" — click to paste values into open Excel"
            };
            canvas.Children.Add(shape);sectors.Add(shape);
            hitZones.Add((i,id,geometry));
            double mid=(a+b)*Math.PI/360;
            double radius=(Inner+Outer)/2;
            double x=CX+radius*Math.Cos(mid),
                   y=CY+radius*Math.Sin(mid);
            var text=new TextBlock{
                Text=full,Width=83,FontSize=11,
                Foreground=newItem?Brushes.White:Ink,
                FontWeight=newItem?FontWeights.SemiBold:FontWeights.Normal,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=full,IsHitTestVisible=false
            };
            Canvas.SetLeft(text,x-41.5);Canvas.SetTop(text,y-10);
            canvas.Children.Add(text);
        }
        if(start>0)
        {
            var previous=SmallButton("‹",()=>Page(-1));
            previous.ToolTip="Previous saved Excel mappings";
            Canvas.SetLeft(previous,20);Canvas.SetTop(previous,185);
            canvas.Children.Add(previous);
        }
        if(start+PerPage<formats.Count)
        {
            var next=SmallButton("›",()=>Page(1));
            next.ToolTip="More saved Excel mappings";
            Canvas.SetLeft(next,25);Canvas.SetTop(next,273);
            canvas.Children.Add(next);
        }
    }
    private int Slot(Point p)
    {
        foreach(var zone in hitZones)
            if(zone.Hit.FillContains(p))return zone.Index;
        return -1;
    }
    private bool CanPage(int dir)=>dir<0
        ?start>0:start+PerPage<formats.Count;
    private void Page(int dir)
    {
        if(!CanPage(dir)){edgeTimer.Stop();return;}
        start=Math.Clamp(start+dir,0,Math.Max(0,formats.Count-PerPage));
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
        try {Hover(DeskSpaceWheelHoverV35.MouseIn(canvas));}
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
        hovered=slot;
        for(int i=0;i<sectors.Count;i++)
        {
            bool manage=i==sectors.Count-1;
            sectors[i].Fill=i==slot
                ? (manage?new SolidColorBrush(Color.FromRgb(48,60,139)):Selected)
                : (manage?Accent:Idle);
        }
        // Preview is read-only. Hovering MUST NEVER modify an Excel workbook.
        if(slot<0)return;
        if(slot==hitZones.Count-1)
            Status("Manage Excel formats","Rename and edit saved mapping rows from "+
                "DeskSpace Workspace → Excel Mapping. Select a saved child "+
                "in this wheel to paste values directly into the open workbook.");
        else
        {
            var format=formats.FirstOrDefault(x=>x.Id==hitZones[slot].Id);
            if(format!=null)Preview(format);
        }
    }
    private void OnDown(object sender,MouseButtonEventArgs e)
    {
        if(!arc.IsOpen||pasting)return;
        int slot=Slot(e.GetPosition(canvas));
        if(slot<0)return;
        Activate(slot);
        e.Handled=true;
    }
    private void OnUp(object sender,MouseButtonEventArgs e)
    {
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
        int slot=Slot(local);
        if(slot<0)return false;
        Hover(local);
        if(released)Activate(slot);
        return true;
    }
    private void Activate(int slot)
    {
        if(disposed||pasting||slot<0||slot>=hitZones.Count)return;
        var id=hitZones[slot].Id;
        if(!id.HasValue)
        {
            Status("Manage Excel mappings",
                "Open DeskSpace Workspace → Excel Mapping to capture, rename "+
                "or edit saved formats. Nothing was pasted.");
            return;
        }
        var entry=formats.FirstOrDefault(f=>f.Id==id.Value);
        if(entry==null)return;
        HideArc();
        collapseParent();
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
            var valueText=new TextBlock{Text=value.Display,
                FontSize=11,Foreground=Ink,TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=value.CellAddress+" = "+value.Display};
            Grid.SetColumn(valueText,1);row.Children.Add(valueText);
            shelfBody.Children.Add(row);count++;
        }
        shelfMessage.Text=entry.CoordinatesVerified
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
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        shelfMessage.Text=message;
        Position();
        shelf.IsOpen=true;
    }
    private async System.Threading.Tasks.Task PasteAsync(DeskSpaceExcelTemplateV16 entry)
    {
        if(disposed)return;
        pasting=true;
        Status("Excel paste · "+entry.Name,
            "Locating matching labels and protecting Excel formulas…");
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
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                preview,entry,overwrite:true);
            if(disposed)return;
            Status("Excel values pasted",
                ready+" input values were pasted into "+preview.SheetName+
                ". Excel formatting and formulas were retained. "+
                "Original workbook not auto-saved; review and save in Excel. "+
                "Backup: "+backup);
        }
        catch(Exception ex)
        {
            if(!disposed)
                Status("Excel paste blocked",
                    ex.Message+" Nothing was pasted if destination "+
                    "verification or backup failed.");
        }
        finally {pasting=false;}
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        pointerTimer.Stop();edgeTimer.Stop();
        owner.LocationChanged-=OnOwnerMoved;
        owner.SizeChanged-=OnOwnerResized;
        arc.IsOpen=false;shelf.IsOpen=false;
    }
}
