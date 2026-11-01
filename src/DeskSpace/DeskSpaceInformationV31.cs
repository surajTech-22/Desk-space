using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// A contextual PARTIAL arc and one compact shelf; never a new application
// window. Password rows reference the existing authenticated AES-GCM vault.
// Original V22/V25 information remains unmodified and is imported read-only.
internal sealed class DeskSpaceInformationV31 : IDisposable
{
    internal sealed class Item
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Label {get;set;}="";
        public string Value {get;set;}="";
        public Guid? VaultId {get;set;}
    }
    internal sealed class Collection
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Name {get;set;}="";
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
        public List<Item> Rows {get;set;}=new();
    }
    private sealed class LegacyRow
    {
        public Guid Id {get;set;}
        public string Title {get;set;}="";
        public string Category {get;set;}="";
        public string Value {get;set;}="";
        public string Notes {get;set;}="";
        public DateTime UpdatedUtc {get;set;}
    }
    private sealed class RowEditor
    {
        public ComboBox Choice=null!;
        public TextBox Custom=null!;
        public TextBox Value=null!;
        public PasswordBox Password=null!;
        public Guid? VaultId;
    }

    private readonly Window owner;
    private readonly Action changed;
    private readonly DeskSpaceVaultV10 vault=new();
    private readonly DispatcherTimer lockTimer;
    private readonly DispatcherTimer newHoverTimer;
    private readonly Popup arc;
    private readonly Popup shelf;
    private readonly Canvas arcCanvas;
    private readonly Border shelfBorder;
    private readonly StackPanel shelfBody;
    private readonly TextBlock shelfTitle;
    private readonly TextBlock warning;
    private readonly PasswordBox master;
    private readonly StackPanel unlockBar;
    private static readonly string Folder=WorkspaceStore.Root;
    private static readonly string CollectionsPath=Path.Combine(Folder,"information-collections-v26.json");
    private static readonly string LegacyPath=Path.Combine(Folder,"information-items-v22.json");
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(41,48,78));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(97,82,180));
    private static readonly Brush Pale=new SolidColorBrush(Color.FromRgb(239,237,251));
    private int startIndex;
    private DateTime lastVaultAccess=DateTime.UtcNow;
    private bool disposed;
    private List<Collection> all=new();
    private readonly List<RowEditor> editorRows=new();
    private readonly List<TextBlock> revealedValues=new();
    private bool addingRow;
    private Guid? editingId;
    private Guid? shownCollectionId;
    private bool editorActive;
    private int hoveredChild=-1;
    private readonly List<System.Windows.Shapes.Path> childWedges=new();
    private readonly List<Button> childLabels=new();
    private int keyboardIndex=-1;

    public bool IsOpen => arc.IsOpen || shelf.IsOpen;
    public bool IsArcOpen => arc.IsOpen;
    public bool IsEditing => editorActive;
    public int SavedCount => all.Count;
    public bool ConfirmNavigation()
    {
        if(!editorActive)return true;
        return MessageBox.Show(owner,
            "Close the Information editor without saving these changes?",
            "Unsaved information",MessageBoxButton.YesNo,
            MessageBoxImage.Warning)==MessageBoxResult.Yes;
    }
    public DeskSpaceInformationV31(Window window,Action onChanged)
    {
        owner=window;changed=onChanged;
        arcCanvas=new Canvas{Width=488,Height=222,Focusable=true};
        arcCanvas.PreviewKeyDown+=OnSubmenuKeyDown;
        // The popup has its own HWND. Dispatch gestures from the CANVAS,
        // not from labels or individual wedge mouse events: WPF popup
        // capture and overlapping controls otherwise lose selections.
        arcCanvas.MouseMove+=OnArcMouseMove;
        arcCanvas.MouseLeave+=(_,_)=>EndArcHover();
        arcCanvas.PreviewMouseLeftButtonDown+=OnArcPointerDown;
        arcCanvas.PreviewMouseLeftButtonUp+=OnArcPointerUp;
        newHoverTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(260)};
        newHoverTimer.Tick+=(_,_)=>{
            newHoverTimer.Stop();
            if(!disposed && arc.IsOpen &&
               hoveredChild==arcHitZones.Count-1 && !editorActive)
                NewCollection();
        };
        arc=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=-39,VerticalOffset=-102,
            AllowsTransparency=true,StaysOpen=true,Child=arcCanvas};
        shelfBody=new StackPanel{Margin=new Thickness(11,6,11,10)};
        shelfTitle=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,VerticalAlignment=VerticalAlignment.Center};
        var header=new DockPanel{
            LastChildFill=true,Background=Pale,Height=35,
            Margin=new Thickness(0,0,0,3)};
        var x=SmallButton("×",CloseShelf);
        x.FontSize=16;x.MinWidth=30;
        x.Margin=new Thickness(4,3,6,3);
        DockPanel.SetDock(x,Dock.Right);
        header.Children.Add(x);
        var h=new Border{Padding=new Thickness(10,7,0,0),Child=shelfTitle};
        header.Children.Add(h);
        var scroll=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            MaxHeight=285,Content=shelfBody};
        warning=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
            FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,2,8,0)};
        var frame=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(header,Dock.Top);frame.Children.Add(header);
        DockPanel.SetDock(warning,Dock.Bottom);frame.Children.Add(warning);
        frame.Children.Add(scroll);
        shelfBorder=new Border{Width=398,MaxHeight=356,
            Focusable=true,
            Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(201,208,230)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),
            Child=frame,Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=14,Opacity=.17,ShadowDepth=3}};
        shelfBorder.PreviewKeyDown+=OnSubmenuKeyDown;
        shelf=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=5,VerticalOffset=owner.Height+7,
            AllowsTransparency=true,StaysOpen=true,Child=shelfBorder};
        master=new PasswordBox{MinWidth=155,Height=29,Padding=new Thickness(6,3,6,3)};
        unlockBar=new StackPanel{Visibility=Visibility.Collapsed,
            Margin=new Thickness(2,5,2,7)};
        lockTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};
        lockTimer.Tick+=(_,_)=>{
            if(vault.IsUnlocked && DateTime.UtcNow-lastVaultAccess>TimeSpan.FromMinutes(3)){
                vault.Lock();MaskRevealed();master.Clear();
                if(shelf.IsOpen)warning.Text="Vault locked after inactivity.";
            }
        };
        lockTimer.Start();
    }
    private static Button SmallButton(string text,Action action)
    {
        var b=new Button{
            Content=text,MinHeight=27,Padding=new Thickness(8,3,8,3),
            Margin=new Thickness(3,2,3,2),
            Background=Pale,Foreground=Ink,
            BorderThickness=new Thickness(0),
            Cursor=Cursors.Hand,FontSize=12};
        b.Click+=(_,_)=>action();
        return b;
    }
    private static List<Collection> Read()
    {
        if(File.Exists(CollectionsPath))
        {
            try{
                var data=JsonSerializer.Deserialize<List<Collection>>(File.ReadAllText(CollectionsPath))
                    ??throw new InvalidDataException("Invalid information collections.");
                foreach(var c in data)c.Rows??=new List<Item>();
                return data;
            }
            catch(Exception ex){throw new InvalidDataException(
                "Saved information cannot be read. Existing files were not changed.",ex);}
        }
        // Read-only, lossless backward compatibility for one-record-per-value
        // schema. Do not overwrite, delete or relocate V22/V25 entries.
        if(!File.Exists(LegacyPath))return new();
        try
        {
            var legacy=JsonSerializer.Deserialize<List<LegacyRow>>(File.ReadAllText(LegacyPath))
                ??new List<LegacyRow>();
            return legacy.Select(x=>new Collection{
                Id=x.Id==Guid.Empty?Guid.NewGuid():x.Id,
                Name=string.IsNullOrWhiteSpace(x.Title)?"Imported information":x.Title,
                UpdatedUtc=x.UpdatedUtc,
                Rows=new List<Item>{
                    new(){Label="Value",Value=x.Value},
                    new(){Label="Category",Value=x.Category},
                    new(){Label="Notes",Value=x.Notes}
                }.Where(row=>!string.IsNullOrWhiteSpace(row.Value)).ToList()
            }).ToList();
        }
        catch(Exception ex){throw new InvalidDataException(
            "Legacy information cannot be read. Original data was not changed.",ex);}
    }
    private static void Write(List<Collection> data)
    {
        Directory.CreateDirectory(Folder);
        string temp=CollectionsPath+".pending";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(data,new JsonSerializerOptions{
                WriteIndented=true}),new UTF8Encoding(false));
            if(File.Exists(CollectionsPath))
                File.Replace(temp,CollectionsPath,CollectionsPath+".previous",true);
            else File.Move(temp,CollectionsPath);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Show()
    {
        if(disposed)return;
        try
        {
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
            startIndex=Math.Min(startIndex,Math.Max(0,all.Count-4));
            RenderArc();
            arc.IsOpen=true;
            changed();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace information",
            MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    public void HideArc()
    {
        newHoverTimer.Stop();
        if(!arc.IsOpen)return;
        arc.IsOpen=false;
        hoveredChild=-1;keyboardIndex=-1;
        changed();
    }
    public void Hide()
    {
        HideArc();
        CloseShelf();
        warning.Text="";
        changed();
    }
    private void CloseShelf()
    {
        editorActive=false;shownCollectionId=null;
        shelf.IsOpen=false;
        MaskRevealed();vault.Lock();master.Clear();
        changed(); // restore the correct centre preview when shelf closes
    }
    // Radially connected wedges instead of R26's detached horizontal
    // arc. Both radii exceed the parent donut's 195-DIP outer edge.
    private static Geometry ChildSector(double first, double last)
    {
        const double cx=244,cy=306,r0=207,r1=260;
        static Point At(double x,double y,double radius,double angle)
        {
            double a=angle*Math.PI/180;
            return new Point(x+radius*Math.Cos(a),y+radius*Math.Sin(a));
        }
        var figure=new PathFigure{
            StartPoint=At(cx,cy,r1,first),IsClosed=true};
        figure.Segments.Add(new ArcSegment{
            Point=At(cx,cy,r1,last),Size=new Size(r1,r1),
            SweepDirection=SweepDirection.Clockwise});
        figure.Segments.Add(new LineSegment(At(cx,cy,r0,last),true));
        figure.Segments.Add(new ArcSegment{
            Point=At(cx,cy,r0,first),Size=new Size(r0,r0),
            SweepDirection=SweepDirection.Counterclockwise});
        return new PathGeometry(new[]{figure});
    }
    private const double ArcCenterX=244;
    private const double ArcCenterY=306;
    private const double ArcInnerRadius=207;
    private const double ArcOuterRadius=260;
    private const int PageSize=4;
    private readonly List<(int index, Guid? id, Geometry hit)> arcHitZones=new();
    private bool arcPressed;

    // Child sectors are placed ONLY across a partial arc centered over the
    // Information parent at the top of the existing outer ring. For an
    // empty collection list, + New occupies the top-middle, not the far
    // right edge of a five-slot arc with four invisible empty sectors.
    private void RenderArc()
    {
        newHoverTimer.Stop();
        arcCanvas.Children.Clear();
        childWedges.Clear();childLabels.Clear();arcHitZones.Clear();
        hoveredChild=-1;
        var visible=all.Skip(startIndex).Take(PageSize).ToArray();
        int count=visible.Length+1;
        double halfSweep=count==1?17:Math.Min(57,12.0*count);
        double start=-90-halfSweep;
        double step=halfSweep*2/count;
        for(int i=0;i<count;i++)
        {
            bool add=i==visible.Length;
            Guid? id=add?null:visible[i].Id;
            string name=add?"+ New":visible[i].Name;
            double a=start+step*i,b=start+step*(i+1);
            Geometry hit=ChildSector(a,b);
            var sector=new System.Windows.Shapes.Path{
                Data=hit,
                Stroke=Brushes.White,StrokeThickness=2,
                Fill=add?Accent:ChildIdle,
                Tag=i,Cursor=Cursors.Hand,
                ToolTip=add?"Create a named information collection":name};
            // Every visible sector is a true hit target; a single shared
            // Canvas pointer dispatcher handles all child selection.
            arcCanvas.Children.Add(sector);
            childWedges.Add(sector);
            arcHitZones.Add((i,id,hit));
            double angle=(a+b)*Math.PI/360;
            double radius=(ArcInnerRadius+ArcOuterRadius)/2;
            double x=ArcCenterX+radius*Math.Cos(angle);
            double y=ArcCenterY+radius*Math.Sin(angle);
            var label=new TextBlock{
                Text=name,
                Width=78,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                FontSize=11,
                FontWeight=add?FontWeights.SemiBold:FontWeights.Normal,
                Foreground=add?Brushes.White:Ink,
                ToolTip=name,
                IsHitTestVisible=false};
            Canvas.SetLeft(label,x-39);
            Canvas.SetTop(label,y-9);
            arcCanvas.Children.Add(label);
        }
        // Only offer pagination when there is actually something to page.
        if(startIndex>0)
        {
            var left=SmallButton("‹",()=>Slide(-1));
            left.Width=32;left.Height=29;left.FontSize=20;
            left.ToolTip="Previous collections";
            Canvas.SetLeft(left,0);Canvas.SetTop(left,166);
            arcCanvas.Children.Add(left);
        }
        if(startIndex+PageSize<all.Count)
        {
            var right=SmallButton("›",()=>Slide(1));
            right.Width=32;right.Height=29;right.FontSize=20;
            right.ToolTip="Next collections";
            Canvas.SetLeft(right,456);Canvas.SetTop(right,166);
            arcCanvas.Children.Add(right);
        }
        arcCanvas.MouseWheel-=ScrollArc;
        arcCanvas.MouseWheel+=ScrollArc;
    }
    private int SlotAtPoint(Point point)
    {
        foreach(var entry in arcHitZones)
            if(entry.hit.FillContains(point))return entry.index;
        return -1;
    }
    private void OnArcMouseMove(object sender,MouseEventArgs e)
    {
        if(!arc.IsOpen || disposed)return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0){EndArcHover();return;}
        // Stay in the current child while moving from its wedge to label.
        if(slot==hoveredChild)return;
        HoverChild(slot,arcHitZones[slot].id);
    }
    private void EndArcHover()
    {
        newHoverTimer.Stop();
        if(hoveredChild>=0 && hoveredChild<childWedges.Count)
        {
            // Re-render hover highlights without touching the active shelf.
            for(int i=0;i<childWedges.Count;i++)
                childWedges[i].Fill=i==childWedges.Count-1?Accent:ChildIdle;
        }
        hoveredChild=-1;
    }
    private void OnArcPointerDown(object sender,MouseButtonEventArgs e)
    {
        if(!arc.IsOpen)return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0)return;
        arcPressed=true;
        // Activation occurs before the release can be lost to the
        // transparent Popup's separate Windows input surface.
        ActivateChild(slot,arcHitZones[slot].id);
        e.Handled=true;
    }
    private void OnArcPointerUp(object sender,MouseButtonEventArgs e)
    {
        if(arcPressed){arcPressed=false;e.Handled=true;return;}
        if(!arc.IsOpen)return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0)return;
        // Fallback for touch/alternative input paths that route a release.
        ActivateChild(slot,arcHitZones[slot].id);
        e.Handled=true;
    }

    private static readonly Brush ChildIdle =
        new SolidColorBrush(Color.FromRgb(220,229,251));
    private static readonly Brush ChildHover =
        new SolidColorBrush(Color.FromRgb(159,185,237));

    private void HoverChild(int sector,Guid? id)
    {
        if(disposed || !arc.IsOpen)return;
        bool enteredNewSector=hoveredChild!=sector;
        if(enteredNewSector)newHoverTimer.Stop();
        hoveredChild=sector;
        keyboardIndex=sector==arcHitZones.Count-1?all.Count:startIndex+sector;
        for(int i=0;i<childWedges.Count;i++)
        {
            var w=childWedges[i];
            bool selected=w.Tag is int tag && tag==sector;
            bool isNew=i==childWedges.Count-1;
            w.Fill=selected?(isNew?
                new SolidColorBrush(Color.FromRgb(73,58,153)):ChildHover):
                (isNew?Accent:ChildIdle);
        }
        // Hovering a saved collection SELECTS and PREVIEWS it immediately.
        // Never reveal stored PW on hover: encrypted values stay masked.
        if(editorActive)return; // do not destroy unsaved editing.
        // Hover on saved wedges previews immediately. + New is also
        // hover-selectable, with a short dwell to avoid opening a draft
        // merely by passing over it while scrolling along the arc.
        if(sector==arcHitZones.Count-1 && enteredNewSector)
        {
            if(!editorActive)newHoverTimer.Start();
        }
        else if(sector<arcHitZones.Count-1 && id.HasValue)
            OpenCollection(id.Value);
    }
    private void EndChildHover(int sector)
    {
        if(hoveredChild!=sector)return;
        if(sector==arcHitZones.Count-1 && !editorActive)
        {
            newHoverTimer.Stop();
            hoveredChild=-1;
        }
    }
    private void ActivateChild(int sector,Guid? id)
    {
        // Standard clicking, keyboard activation and touch still work.
        if(disposed)return;
        if(sector==arcHitZones.Count-1)
        {
            newHoverTimer.Stop();
            if(!editorActive)NewCollection();
            return;
        }
        if(id.HasValue && !editorActive)
            OpenCollection(id.Value);
    }
    private void OnSubmenuKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape)
        {
            if(shelf.IsOpen)CloseShelf();
            else Hide();
            e.Handled=true;return;
        }
        if(editorActive)return; // arrow keys belong to text inputs.
        if(e.Key==Key.Left || e.Key==Key.Right ||
           e.Key==Key.Up || e.Key==Key.Down)
        {
            if(all.Count==0){HoverChild(0,null);e.Handled=true;return;}
            int delta=e.Key==Key.Left || e.Key==Key.Up?-1:1;
            keyboardIndex=Math.Clamp(keyboardIndex+delta,0,all.Count);
            if(keyboardIndex>=all.Count)
            {
                HoverChild(arcHitZones.Count-1,null);
            }
            else
            {
                startIndex=Math.Clamp(keyboardIndex-2,0,Math.Max(0,all.Count-4));
                RenderArc();
                HoverChild(keyboardIndex-startIndex,all[keyboardIndex].Id);
            }
            e.Handled=true;return;
        }
        if(e.Key==Key.Enter || e.Key==Key.Space)
        {
            if(keyboardIndex==all.Count)ActivateChild(arcHitZones.Count-1,null);
            else if(keyboardIndex>=0 && keyboardIndex<all.Count)
                ActivateChild(keyboardIndex-startIndex,all[keyboardIndex].Id);
            e.Handled=true;
        }
    }
    private void ScrollArc(object sender,MouseWheelEventArgs e)
    {
        Slide(e.Delta<0?1:-1);e.Handled=true;
    }
    private void Slide(int delta)
    {
        startIndex=Math.Clamp(startIndex+delta,0,Math.Max(0,all.Count-4));
        RenderArc();
    }
    private void Shelf(string title)
    {
        MaskRevealed();
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        warning.Text="";
        shelf.VerticalOffset=owner.Height+7;
        shelf.IsOpen=true;
    }
    private void NewCollection()
    {
        if(editorActive)return;
        editingId=null;shownCollectionId=null;
        OpenEditor(new Collection());
    }
    private void OpenCollection(Guid id)
    {
        if(editorActive)return;
        if(shownCollectionId==id && shelf.IsOpen)return;
        var collection=all.FirstOrDefault(x=>x.Id==id);
        if(collection==null)return;
        shownCollectionId=id;
        Shelf(collection.Name);
        var tools=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        tools.Children.Add(SmallButton("Edit",()=>OpenEditor(collection)));
        tools.Children.Add(SmallButton("Delete",()=>DeleteCollection(collection)));
        shelfBody.Children.Add(tools);
        foreach(var row in collection.Rows)
        {
            var line=new Grid{Margin=new Thickness(0,2,0,3)};
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(106)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var label=new TextBlock{Text=row.Label,FontSize=12,
                FontWeight=FontWeights.SemiBold,Foreground=Ink,
                VerticalAlignment=VerticalAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis};
            line.Children.Add(label);
            var actual=row.VaultId!=null?"••••••••":row.Value;
            var val=new TextBlock{Text=actual,FontSize=12,Foreground=Ink,
                TextTrimming=TextTrimming.CharacterEllipsis,
                VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,4,0)};
            Grid.SetColumn(val,1);line.Children.Add(val);
            var button=SmallButton(row.VaultId!=null?"◉":"⧉",()=>{
                if(row.VaultId!=null)
                {
                    if(!vault.IsUnlocked){OfferVaultUnlock();return;}
                    var s=vault.Entries.FirstOrDefault(z=>z.Id==row.VaultId);
                    if(s==null){warning.Text="The linked vault entry was not found.";return;}
                    TouchVault();
                    if(val.Text=="••••••••") {val.Text=s.Password;revealedValues.Add(val);}
                    else {val.Text="••••••••";revealedValues.Remove(val);}
                }
                else if(row.Value.Length>0)Clipboard.SetText(row.Value);
            });
            button.ToolTip=row.VaultId!=null?"Reveal after vault unlock":"Copy exact value";
            Grid.SetColumn(button,2);line.Children.Add(button);
            shelfBody.Children.Add(line);
        }
    }
    private void DeleteCollection(Collection collection)
    {
        if(MessageBox.Show(owner,
            "Delete '"+collection.Name+"' and its saved information rows? "+
            "Any linked password remains separately in the encrypted vault.",
            "Delete information",MessageBoxButton.YesNo,
            MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try
        {
            var data=Read();
            data.RemoveAll(x=>x.Id==collection.Id);
            Write(data);
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
            startIndex=Math.Min(startIndex,Math.Max(0,all.Count-4));
            CloseShelf();RenderArc();changed();
        }
        catch(Exception ex){warning.Text=ex.Message;}
    }
    private void OfferVaultUnlock()
    {
        if(vault.IsUnlocked)return;
        if(unlockBar.Parent is Panel p)p.Children.Remove(unlockBar);
        unlockBar.Children.Clear();
        unlockBar.Children.Add(new TextBlock{Text=vault.IsInitialized?
            "Unlock Security Vault to use PW":"Create Security Vault (master password, 12+ characters)",
            Foreground=Ink,FontSize=11,Margin=new Thickness(0,0,0,3)});
        if(master.Parent is Panel previous)previous.Children.Remove(master);
        unlockBar.Children.Add(master);
        unlockBar.Children.Add(SmallButton("Unlock vault",()=>{
            try
            {
                vault.Unlock(master.Password);master.Clear();
                TouchVault();unlockBar.Visibility=Visibility.Collapsed;
                warning.Text="";
            }
            catch(Exception){warning.Text="Unable to unlock vault. Check the master password.";}
        }));
        unlockBar.Visibility=Visibility.Visible;
        shelfBody.Children.Insert(0,unlockBar);
    }
    private void TouchVault()=>lastVaultAccess=DateTime.UtcNow;
    private void OpenEditor(Collection collection)
    {
        if(editorActive)return;
        editorActive=true;shownCollectionId=null;
        editingId=all.Any(x=>x.Id==collection.Id)?collection.Id:null;
        Shelf(editingId==null?"New information":"Edit information");
        var name=new TextBox{Text=collection.Name,Height=30,
            Padding=new Thickness(7,4,7,4),Margin=new Thickness(0,0,0,8),
            ToolTip="Name this information collection"};
        shelfBody.Children.Add(new TextBlock{Text="Name",FontSize=11,Foreground=Ink});
        shelfBody.Children.Add(name);
        var rowsHost=new StackPanel();
        shelfBody.Children.Add(rowsHost);
        editorRows.Clear();
        void AddRow(Item? old=null)
        {
            if(addingRow)return;
            addingRow=true;
            try
            {
                var item=new RowEditor{VaultId=old?.VaultId};
                var wrap=new Grid{Margin=new Thickness(0,1,0,5)};
                wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(93)});
                wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
                item.Choice=new ComboBox{
                    ItemsSource=new[]{"UI","PW","Other"},SelectedIndex=0,
                    FontSize=11,Height=29,Margin=new Thickness(0,0,4,0)};
                wrap.Children.Add(item.Choice);
                var fields=new StackPanel();Grid.SetColumn(fields,1);wrap.Children.Add(fields);
                item.Custom=new TextBox{Height=28,Visibility=Visibility.Collapsed,
                    Padding=new Thickness(5,3,5,3),ToolTip="Custom label",
                    Margin=new Thickness(0,0,0,4)};
                item.Value=new TextBox{Height=29,Padding=new Thickness(5,3,5,3),
                    ToolTip="Value"};
                item.Password=new PasswordBox{Height=29,Padding=new Thickness(5,3,5,3),
                    Visibility=Visibility.Collapsed,ToolTip="Stored encrypted in Security Vault"};
                fields.Children.Add(item.Custom);fields.Children.Add(item.Value);
                fields.Children.Add(item.Password);
                rowsHost.Children.Add(wrap);
                editorRows.Add(item);
                if(old!=null)
                {
                    if(old.VaultId!=null || old.Label=="PW"){
                        item.Choice.SelectedItem="PW";
                    }
                    else if(old.Label=="UI")item.Choice.SelectedItem="UI";
                    else {item.Choice.SelectedItem="Other";item.Custom.Text=old.Label;}
                    item.Value.Text=old.VaultId==null?old.Value:"";
                    if(old.VaultId!=null)item.Password.ToolTip="Existing password preserved unless replaced";
                }
                void MaybeAppend()
                {
                    if(addingRow||editorRows.Count==0||editorRows[^1]!=item)return;
                    bool typed=!string.IsNullOrWhiteSpace(item.Custom.Text)||
                        !string.IsNullOrWhiteSpace(item.Value.Text)||
                        item.Password.Password.Length>0;
                    if(typed)AddRow();
                }
                item.Custom.TextChanged+=(_,_)=>MaybeAppend();
                item.Value.TextChanged+=(_,_)=>MaybeAppend();
                item.Password.PasswordChanged+=(_,_)=>MaybeAppend();
                item.Choice.SelectionChanged+=(_,_)=>{
                    var selected=item.Choice.SelectedItem as string;
                    item.Custom.Visibility=selected=="Other"?Visibility.Visible:Visibility.Collapsed;
                    item.Value.Visibility=selected=="PW"?Visibility.Collapsed:Visibility.Visible;
                    item.Password.Visibility=selected=="PW"?Visibility.Visible:Visibility.Collapsed;
                    if(selected=="PW")OfferVaultUnlock();
                };
            }
            finally{addingRow=false;}
        }
        foreach(var row in collection.Rows)AddRow(row);
        AddRow(); // always exactly one trailing empty row
        var commands=new WrapPanel{Margin=new Thickness(0,5,0,2)};
        commands.Children.Add(SmallButton("Save",()=>SaveEdited(name.Text,collection)));
        commands.Children.Add(SmallButton("Cancel",()=>{
            editorActive=false;
            if(editingId!=null)OpenCollection(editingId.Value);
            else CloseShelf();
        }));
        shelfBody.Children.Add(commands);
    }
    private void SaveEdited(string title,Collection source)
    {
        try
        {
            title=title.Trim();
            if(title.Length==0){warning.Text="Enter a collection name.";return;}
            var rows=new List<Item>();
            var secrets=new List<(Guid id,string password)>();
            foreach(var entry in editorRows)
            {
                string type=entry.Choice.SelectedItem as string??"UI";
                string label=type=="Other"?entry.Custom.Text.Trim():type;
                bool secret=type=="PW";
                string value=secret?"":entry.Value.Text;
                bool hasPassword=secret&&entry.Password.Password.Length>0;
                if(label.Length==0&&string.IsNullOrWhiteSpace(value)&&!hasPassword)continue;
                if(label.Length==0){warning.Text="Enter a label for the custom row.";return;}
                if(secret)
                {
                    if(entry.VaultId==null&&!hasPassword)continue;
                    if(hasPassword && !vault.IsUnlocked)
                    {
                        warning.Text="Unlock the Security Vault to save the PW row.";
                        OfferVaultUnlock();return;
                    }
                    var id=entry.VaultId??Guid.NewGuid();
                    rows.Add(new Item{Label="PW",Value="",VaultId=id});
                    if(hasPassword)secrets.Add((id,entry.Password.Password));
                }
                else
                {
                    if(value.Length==0)continue;
                    rows.Add(new Item{Label=label,Value=value});
                }
            }
            if(rows.Count==0){warning.Text="Enter at least one information value.";return;}
            // Commit PW only to the encrypted vault before writing references.
            foreach(var entry in secrets)
                vault.Upsert(new DeskSpaceVaultV10.Secret{
                    Id=entry.id,Label="DeskSpace information / "+title,
                    Password=entry.password});
            if(secrets.Count>0)TouchVault();
            var updated=new Collection{
                Id=editingId??source.Id,
                Name=title,Rows=rows,UpdatedUtc=DateTime.UtcNow};
            var data=Read();
            int index=data.FindIndex(x=>x.Id==updated.Id);
            if(index>=0)data[index]=updated;else data.Add(updated);
            Write(data);
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
            editorActive=false;shownCollectionId=null;
            RenderArc();changed();
            OpenCollection(updated.Id);
            warning.Text="";
        }
        catch(Exception ex){warning.Text=ex.Message;}
    }
    private void MaskRevealed()
    {
        foreach(var value in revealedValues)value.Text="••••••••";
        revealedValues.Clear();
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        arc.IsOpen=false;shelf.IsOpen=false;
        editorActive=false;
        MaskRevealed();
        arcCanvas.MouseWheel-=ScrollArc;
        newHoverTimer.Stop();lockTimer.Stop();
        vault.Dispose();
    }
}
