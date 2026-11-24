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
using System.Windows.Media.Animation;

namespace DeskSpace;

// A contextual PARTIAL arc and one compact shelf; never a new application
// window. Password rows reference the existing authenticated AES-GCM vault.
// Original V22/V25 information remains unmodified and is imported read-only.
internal sealed class DeskSpaceInformationV36 : IDisposable
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
    private readonly Action onCommitted;
    private readonly DeskSpaceVaultV10 vault=new();
    private readonly DispatcherTimer lockTimer;
    private readonly DispatcherTimer newHoverTimer;
    private readonly DispatcherTimer edgeTimer;
    private readonly DispatcherTimer pointerTimer;
    private readonly DeskSpaceMiddleScrollV63.Controller middleScroll;
    private int edgeDirection;
    private readonly Popup arc;
    private readonly Popup shelf;
    private readonly Canvas arcCanvas;
    private readonly Border shelfBorder;
    private readonly StackPanel shelfBody;
    private readonly ScrollViewer shelfScroll;
    private readonly TextBlock shelfTitle;
    private readonly Button headerAddBtn;
    private readonly Button headerAppsBtn;
    private int positionTick;
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
    private List<(string Label, string Path)> arcApps=new();
    private string activeArcAppPath="";
    private double activeArcAppAngle=-90;
    private int filePage;
    private readonly HashSet<string> loadingArcApps=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,int> arcAppScanGens=new(StringComparer.OrdinalIgnoreCase);
    private long warmedAppsRev=-1;
    private DateTime warmedConfigStampUtc=DateTime.MinValue;
    private bool contextMenuOpen;
    private int ActiveArcTotalCount()=>arcApps.Count>0?arcApps.Count:all.Count;
    private bool warmed,warming;
    private DateTime warmStamp=DateTime.MinValue;
    private static DateTime CurrentStamp()
    {
        var target=File.Exists(CollectionsPath)?CollectionsPath:LegacyPath;
        try{return File.Exists(target)?File.GetLastWriteTimeUtc(target):
            DateTime.MinValue;}
        catch(IOException){return DateTime.MinValue;}
        catch(UnauthorizedAccessException){return DateTime.MinValue;}
    }
    public void WarmStart()
    {
        if(disposed||warming)return;
        warming=true;
        _=System.Threading.Tasks.Task.Run(()=>{
            try
            {
                var stamp=CurrentStamp();
                var snapshot=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
                var appsSnapshot=DeskSpaceInformationTabV380.GetSelectedAppsForInformation();
                long appsRev=DeskSpaceInformationTabV380.InformationAppsRevision;
                DateTime cfgStamp=DeskSpaceInformationTabV380.GetInformationConfigStampUtc();
                DeskSpaceInformationTabV380.PrewarmInformationApps(appsSnapshot.Select(a=>a.Path));
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    all=snapshot;arcApps=appsSnapshot;warmStamp=stamp;warmedAppsRev=appsRev;warmedConfigStampUtc=cfgStamp;warmed=true;warming=false;
                    if(arcApps.Count==1)
                        EnsureAppInfoLoadedForArc(arcApps[0].Path);
                    if(arc.IsOpen){RenderArc();PositionArc();changed();}
                }));
            }
            catch(Exception ex)
            {
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    warming=false;
                    if(arc.IsOpen)warning.Text=
                        "Saved information cannot be read: "+ex.Message;
                }));
            }
        });
    }
    private readonly List<RowEditor> editorRows=new();
    private readonly List<TextBlock> revealedValues=new();
    private bool addingRow;
    private bool transientPreview;
    private void CloseTransientPreview()
    {
        if(!transientPreview || editorActive)return;
        transientPreview=false;
        if(shelf.IsOpen)CloseShelf();
    }
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
    public int SavedCount => ActiveArcTotalCount();
    internal bool IsContextMenuOpen => contextMenuOpen;
    internal bool IsPointerNearArc()
    {
        if(contextMenuOpen)return true;
        if(disposed || !arc.IsOpen || !arcCanvas.IsLoaded)return false;
        try
        {
            var pt=DeskSpaceWheelHoverV35.MouseIn(arcCanvas);
            return pt.X>=-26 && pt.X<=arcCanvas.Width+26 && pt.Y>=-26 && pt.Y<=arcCanvas.Height+26;
        }
        catch(InvalidOperationException){return false;}
    }
    internal string CenterSummaryText
    {
        get
        {
            if(arcApps.Count==1)
            {
                var app=arcApps[0];
                if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(app.Path,out var singleRes) &&
                   singleRes.MeetsMinimumTwoFilesRule)
                    return $"{app.Label}\n{singleRes.MatchedFiles.Count} files";
                return $"{app.Label}\nLoading…";
            }
            if(arcApps.Count>=2)
                return $"Information\n{arcApps.Count} apps";
            return $"Information\n{all.Count} saved";
        }
    }
    public bool ConfirmNavigation()
    {
        if(!editorActive)return true;
        return MessageBox.Show(owner,
            "Close the Information editor without saving these changes?",
            "Unsaved information",MessageBoxButton.YesNo,
            MessageBoxImage.Warning)==MessageBoxResult.Yes;
    }
    public DeskSpaceInformationV36(Window window,Action onChanged,Action? onSelected=null)
    {
        owner=window;changed=onChanged;
        onCommitted=onSelected??(()=>{});
        arcCanvas=new Canvas{Width=1,Height=1,Focusable=true};
        arcCanvas.PreviewKeyDown+=OnSubmenuKeyDown;
        // The popup has its own HWND. Dispatch gestures from the CANVAS,
        // not from labels or individual wedge mouse events: WPF popup
        // capture and overlapping controls otherwise lose selections.
        arcCanvas.MouseMove+=OnArcMouseMove;
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(arcCanvas,
            ()=>HasAnyArcOverflow(),
            ()=>arc.IsOpen&&!editorActive,
            dir=>HandleScrollStep(dir,SlotAtPointerOrHover()));
        // The popup's transparent/non-hit-test pixels cannot own WPF hover.
        // Independently poll real screen-pointer position during an open arc.
        // Both parent-ring capture and child-popup hit-testing therefore use
        // one logical radial navigation path.
        pointerTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
        pointerTimer.Tick+=(_,_)=>PollPointer();
        arcCanvas.PreviewMouseLeftButtonDown+=OnArcPointerDown;
        arcCanvas.PreviewMouseLeftButtonUp+=OnArcPointerUp;
        arcCanvas.PreviewMouseRightButtonDown+=OnArcRightButtonDown;
        newHoverTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(DeskSpaceWheelHoverV35.ChildCommitMilliseconds)};
        edgeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(DeskSpaceWheelHoverV35.EdgeFirstMilliseconds)};
        edgeTimer.Tick+=(_,_)=>{
            if(!arc.IsOpen || disposed || editorActive || edgeDirection==0){
                StopEdgeScroll();return;
            }
            if(!CanPage(edgeDirection)){StopEdgeScroll();return;}
            Slide(edgeDirection);
            edgeTimer.Interval=TimeSpan.FromMilliseconds(DeskSpaceWheelHoverV35.EdgeRepeatMilliseconds);
        };
        newHoverTimer.Tick+=(_,_)=>{
            newHoverTimer.Stop();
            if(disposed || !arc.IsOpen || editorActive ||
               capturedDragSelection || edgeDirection!=0 ||
               Mouse.LeftButton==MouseButtonState.Pressed ||
               Mouse.RightButton==MouseButtonState.Pressed)return;
            // An unselected hover must never become a committed information
            // preview. Only mouse click, explicit drag-release or Enter can
            // call ActivateChild. Do not leave a shelf below the circle.
            EndArcHover();
        };
        arc=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=-39,VerticalOffset=-102,
            AllowsTransparency=true,StaysOpen=true,Child=arcCanvas};
        shelfBody=new StackPanel{Margin=new Thickness(9,3,9,6)};
        shelfTitle=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,VerticalAlignment=VerticalAlignment.Center};
        var header=new DockPanel{
            LastChildFill=true,Background=Pale,Height=31,
            Margin=new Thickness(0,0,0,2)};
        var x=SmallButton("×",CloseShelf);
        x.FontSize=16;x.MinWidth=30;
        x.Margin=new Thickness(4,3,6,3);
        DockPanel.SetDock(x,Dock.Right);
        header.Children.Add(x);
        var add=SmallButton("+",()=>{if(!editorActive)NewCollection();});
        add.ToolTip="New information collection";
        add.FontSize=17;
        add.MinWidth=29;
        add.Margin=new Thickness(1,2,1,2);
        DockPanel.SetDock(add,Dock.Right);
        headerAddBtn=add;
        header.Children.Add(add);
        var appsBtn=SmallButton("Apps",()=>OpenAppFileInfoShelf());
        appsBtn.ToolTip="Select an application to inspect inside-file Header → Value information";
        appsBtn.FontSize=11;
        appsBtn.MinWidth=40;
        appsBtn.Margin=new Thickness(1,3,1,3);
        DockPanel.SetDock(appsBtn,Dock.Right);
        headerAppsBtn=appsBtn;
        header.Children.Add(appsBtn);
        var h=new Border{Padding=new Thickness(9,6,0,0),Child=shelfTitle};
        header.Children.Add(h);
        shelfScroll=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            MaxHeight=165,Content=shelfBody};
        warning=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
            FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,2,8,0)};
        var frame=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(header,Dock.Top);frame.Children.Add(header);
        DockPanel.SetDock(warning,Dock.Bottom);frame.Children.Add(warning);
        frame.Children.Add(shelfScroll);
        shelfBorder=new Border{Width=304,MaxHeight=234,
            Focusable=true,
            Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(201,208,230)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),
            Child=frame,Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=14,Opacity=.17,ShadowDepth=3}};
        shelfBorder.PreviewKeyDown+=OnSubmenuKeyDown;
        shelf=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=0,VerticalOffset=owner.Height+5,
            AllowsTransparency=true,StaysOpen=true,Child=shelfBorder};
        // WPF Popup occupies a separate HWND. Updating the offsets on each
        // owner move/resize (including animated morph frames) keeps the shelf
        // visually tethered to the actual compact/expanded donut.
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
        DeskSpaceMonitorFenceV153.TrackPopup(owner,shelf);
        owner.LocationChanged+=OwnerLocationChanged;
        owner.SizeChanged+=OwnerSizeChanged;
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
        // Returning to Information starts with the wheel, not a previously
        // selected shelf. Never discard an active unsaved editor.
        if(shelf.IsOpen && !editorActive)CloseShelf();
        try
        {
            DateTime cfgStamp=DeskSpaceInformationTabV380.GetInformationConfigStampUtc();
            if(!warmed || CurrentStamp()!=warmStamp ||
               warmedAppsRev!=DeskSpaceInformationTabV380.InformationAppsRevision ||
               warmedConfigStampUtc!=cfgStamp)
            {
                arcApps=DeskSpaceInformationTabV380.GetSelectedAppsForInformation();
                warmedAppsRev=DeskSpaceInformationTabV380.InformationAppsRevision;
                warmedConfigStampUtc=cfgStamp;
                DeskSpaceInformationTabV380.PrewarmInformationApps(arcApps.Select(a=>a.Path));
                WarmStart();
            }
            activeArcAppPath="";
            filePage=0;
            startIndex=Math.Clamp(startIndex,0,Math.Max(0,Depth0TotalCount()-Depth0PageSize()));
            if(arcApps.Count==1)
                EnsureAppInfoLoadedForArc(arcApps[0].Path);
            RenderArc();
            PositionArc();
            arc.IsOpen=true;
            pointerTimer.Start();
            changed();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace information",
            MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    public void HideArc()
    {
        contextMenuOpen=false;
        newHoverTimer.Stop();
        middleScroll.Stop();
        pointerTimer.Stop();
        CloseTransientPreview();
        StopEdgeScroll();
        capturedDragSelection=false;
        activeArcAppPath="";
        filePage=0;
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
        transientPreview=false;
        editorActive=false;shownCollectionId=null;
        editorNameField=null;
        shelf.IsOpen=false;
        LeaveEditorKeyboardMode();
        MaskRevealed();vault.Lock();master.Clear();
        changed(); // restore the correct centre preview when shelf closes
    }
    private void EnterEditorKeyboardMode(TextBox firstField)
    {
        // Normal circle is intentionally no-activate. When a user opens the
        // Information editor, switch to keyboard-capable input temporarily.
        // Otherwise WPF textboxes can appear editable without accepting keys.
        if(owner.IsMouseCaptured)owner.ReleaseMouseCapture();
        DeskSpaceWindowBehavior.AllowInput(owner,true);
        editorKeyboardMode=true;
        editorNameField=firstField;
        owner.Activate();
        FocusManager.SetIsFocusScope(shelfBorder,true);
        owner.Dispatcher.BeginInvoke(new Action(()=>{
            if(!editorActive || !shelf.IsOpen || disposed)return;
            firstField.Focus();
            Keyboard.Focus(firstField);
            FocusManager.SetFocusedElement(shelfBorder,firstField);
        }),DispatcherPriority.Input);
    }
    private void LeaveEditorKeyboardMode()
    {
        if(!editorKeyboardMode)return;
        editorKeyboardMode=false;
        DeskSpaceWindowBehavior.AllowInput(owner,false);
    }
    private Rect arcBounds=Rect.Empty;
    private const double ArcMargin=6;
    private const double InfoOuterInner=257,InfoOuterOuter=322;
    private const int PageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private const int SingleAppFilePageSize=DeskSpaceSubDonutStandardV62.Visible-2;
    private const int FilePageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private int Depth0PageSize()=>arcApps.Count==1?SingleAppFilePageSize:PageSize;
    private readonly DeskSpaceSubDonutStandardV62.Wheel standardWheel=new();
    private readonly List<(int index,int depth,string kind,Guid? id,string appPath,string filePath,double midAngle,Geometry hit)> arcHitZones=new();
    private bool arcPressed;
    private bool capturedDragSelection;
    private bool editorKeyboardMode;
    private TextBox? editorNameField;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll",ExactSpelling=true)]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);

    private static double InfoInner(int depth)=>depth switch
    {
        0=>DeskSpaceSubArcGeometryV48.Inner(0),
        1=>InfoOuterInner,
        _=>DeskSpaceSubArcGeometryV48.Inner(depth)
    };
    private static double InfoOuter(int depth)=>depth switch
    {
        0=>DeskSpaceSubArcGeometryV48.Outer(0),
        1=>InfoOuterOuter,
        _=>DeskSpaceSubArcGeometryV48.Outer(depth)
    };
    private static Geometry InfoSector(int depth,double from,double to)
    {
        if(depth==0)return DeskSpaceSubArcGeometryV48.Sector(0,from,to);
        double r0=InfoInner(depth),r1=InfoOuter(depth);
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
    private double FitOuterAngle(double preferred,double half,double outerRadius)
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
        double[] shifts={0,-8,8,-16,16,-24,24,-32,32,-40,40,-48,48};
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
    private static string FormatArcLabel(string text)
    {
        if(string.IsNullOrEmpty(text))return text;
        return text.Replace("_","_\u200B").Replace("-","-\u200B");
    }
    private static ToolTip CreateCompactTooltip(string title,string? subtitle=null)
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
    private static (double Left,double Width) InscribedHorizontalSpan(
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

    private void EnsureAppInfoLoadedForArc(string appPath)
    {
        if(disposed || string.IsNullOrWhiteSpace(appPath))return;
        if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(appPath,out _))return;
        if(!loadingArcApps.Add(appPath))return;
        int gen=arcAppScanGens.TryGetValue(appPath,out int cur)?cur+1:1;
        arcAppScanGens[appPath]=gen;
        _=DeskSpaceInformationTabV380.GetAppInformationAsync(appPath).ContinueWith(t=>{
            loadingArcApps.Remove(appPath);
            if(disposed)return;
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(disposed || !arc.IsOpen)return;
                if(arcAppScanGens.TryGetValue(appPath,out int latest) && latest!=gen)return;
                if((arcApps.Count==1 && string.Equals(arcApps[0].Path,appPath,StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(activeArcAppPath,appPath,StringComparison.OrdinalIgnoreCase))
                {
                    RenderArc();
                }
            }));
        });
    }

    private int Depth0TotalCount()
    {
        if(arcApps.Count==1)
        {
            if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(arcApps[0].Path,out var singleRes) &&
               singleRes.MeetsMinimumTwoFilesRule)
                return singleRes.MatchedFiles.Count;
            return 0;
        }
        if(arcApps.Count>=2)return arcApps.Count;
        return all.Count;
    }

    private int ActiveDepth1TotalCount()
    {
        if(arcApps.Count<2 || string.IsNullOrEmpty(activeArcAppPath))return 0;
        if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(activeArcAppPath,out var res) &&
           res.MeetsMinimumTwoFilesRule)
            return res.MatchedFiles.Count;
        return 0;
    }

    private bool HasAnyArcOverflow()=>
        Depth0TotalCount()>Depth0PageSize() || ActiveDepth1TotalCount()>FilePageSize;

    // Child sectors are placed ONLY across a partial arc centered over the
    // Information parent at the top of the existing outer ring, tightly clipped
    // to exact sector bounds so WPF/monitor fencing keeps them concentric.
    private void RenderArc()
    {
        newHoverTimer.Stop();
        arcCanvas.Children.Clear();
        childWedges.Clear();childLabels.Clear();arcHitZones.Clear();
        hoveredChild=-1;

        var drawItems=new List<(string Label,string Kind,Guid? Id,string AppPath,string FilePath,double A,double B,int Depth,string Tip,bool IsAction)>();

        bool isSingleAppMode=arcApps.Count==1;
        bool isMultiAppMode=arcApps.Count>=2;
        int d0Total=0;
        int d0VisibleItems=0;
        int d1Total=0;
        int d1VisibleItems=0;

        if(isSingleAppMode)
        {
            var app=arcApps[0];
            bool hasCached=DeskSpaceInformationTabV380.TryGetCachedAppInformation(app.Path,out var singleRes);
            bool isLoading=!hasCached || loadingArcApps.Contains(app.Path);
            if(!hasCached)EnsureAppInfoLoadedForArc(app.Path);

            if(hasCached && singleRes.MeetsMinimumTwoFilesRule && singleRes.MatchedFiles.Count>0)
            {
                var files=singleRes.MatchedFiles;
                d0Total=files.Count;
                startIndex=Math.Clamp(startIndex,0,Math.Max(0,d0Total-SingleAppFilePageSize));
                var slice=files.Skip(startIndex).Take(SingleAppFilePageSize).ToArray();
                d0VisibleItems=slice.Length;
                int count=slice.Length+2;
                var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);
                for(int i=0;i<slice.Length;i++)
                {
                    var mf=slice[i];
                    drawItems.Add((FormatArcLabel(mf.DisplayName),"infofile",null,app.Path,mf.FullPath,
                        layout.From(i),layout.To(i),0,
                        $"{mf.DisplayName}\n{mf.KindBadge} · Click to show selected info · Right-click to manage app",
                        false));
                }
                drawItems.Add(("⚙ Fields","choosefields",null,app.Path,"",
                    layout.From(slice.Length),layout.To(slice.Length),0,
                    $"{app.Label} · Choose visible Header → Value fields",
                    true));
                drawItems.Add(("⚙ Apps","manageapps",null,app.Path,"",
                    layout.From(slice.Length+1),layout.To(slice.Length+1),0,
                    $"{app.Label} · Add or remove Information applications",
                    true));
            }
            else
            {
                var layout=DeskSpaceSubDonutStandardV62.Plan(2,0,-90);
                string statusLabel=isLoading?"Loading…":(!singleRes.Eligibility.IsSupported?"Unsupported":"No ≥2 files");
                string statusTip=isLoading?$"Scanning {app.Label} files…":singleRes.StatusMessage;
                drawItems.Add((statusLabel,isLoading?"infoloading":"appstatus",null,app.Path,"",
                    layout.From(0),layout.To(0),0,statusTip,false));
                drawItems.Add(("⚙ Apps","manageapps",null,app.Path,"",
                    layout.From(1),layout.To(1),0,
                    $"{app.Label} · Add or remove Information applications",true));
            }
        }
        else if(isMultiAppMode)
        {
            d0Total=arcApps.Count;
            startIndex=Math.Clamp(startIndex,0,Math.Max(0,d0Total-PageSize));
            var visibleApps=arcApps.Skip(startIndex).Take(PageSize).ToArray();
            d0VisibleItems=visibleApps.Length;
            int count=visibleApps.Length+1;
            var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);

            for(int i=0;i<visibleApps.Length;i++)
            {
                var app=visibleApps[i];
                drawItems.Add((app.Label,"infoapp",null,app.Path,"",
                    layout.From(i),layout.To(i),0,
                    $"{app.Label}\nHover or click to view files · Right-click to remove",
                    false));
            }
            drawItems.Add(("⚙ Apps","manageapps",null,"","",
                layout.From(visibleApps.Length),layout.To(visibleApps.Length),0,
                "Add or remove Information applications",true));

            if(!string.IsNullOrEmpty(activeArcAppPath) &&
               visibleApps.Any(a=>string.Equals(a.Path,activeArcAppPath,StringComparison.OrdinalIgnoreCase)))
            {
                bool hasCached=DeskSpaceInformationTabV380.TryGetCachedAppInformation(activeArcAppPath,out var activeRes);
                bool isLoading=!hasCached || loadingArcApps.Contains(activeArcAppPath);
                if(!hasCached)EnsureAppInfoLoadedForArc(activeArcAppPath);

                var d1Entries=new List<(string Label,string Kind,string AppPath,string FilePath,string Tip,bool IsAction)>();
                if(hasCached && activeRes.MeetsMinimumTwoFilesRule && activeRes.MatchedFiles.Count>0)
                {
                    d1Total=activeRes.MatchedFiles.Count;
                    filePage=Math.Clamp(filePage,0,Math.Max(0,d1Total-FilePageSize));
                    var slice=activeRes.MatchedFiles.Skip(filePage).Take(FilePageSize).ToArray();
                    d1VisibleItems=slice.Length;
                    foreach(var mf in slice)
                    {
                        d1Entries.Add((FormatArcLabel(mf.DisplayName),"infofile",activeArcAppPath,mf.FullPath,
                            $"{mf.DisplayName}\n{mf.KindBadge} · Click to show selected info below",false));
                    }
                }
                else
                {
                    string stLabel=isLoading?"Loading…":"No ≥2 files";
                    string stTip=isLoading?"Scanning compatible files…":(hasCached?activeRes.StatusMessage:"Scanning compatible files…");
                    d1Entries.Add((stLabel,isLoading?"infoloading":"appstatus",activeArcAppPath,"",stTip,false));
                }
                d1Entries.Add(("⚙ Fields","choosefields",activeArcAppPath,"",
                    "Choose visible Header → Value fields for this application",true));

                double d1Half=Math.Clamp(d1Entries.Count*8.5,16.0,44.0);
                double d1Center=FitOuterAngle(activeArcAppAngle,d1Half,InfoOuter(1));
                double d1Start=d1Center-d1Half;
                double d1Step=(d1Half*2)/d1Entries.Count;
                for(int i=0;i<d1Entries.Count;i++)
                {
                    var e=d1Entries[i];
                    drawItems.Add((e.Label,e.Kind,null,e.AppPath,e.FilePath,
                        d1Start+d1Step*i,d1Start+d1Step*(i+1),1,e.Tip,e.IsAction));
                }
            }
        }
        else
        {
            // Classic Collection mode (preserves exact R62/R380 layout and bounds when arcApps is empty)
            d0Total=all.Count;
            startIndex=Math.Clamp(startIndex,0,Math.Max(0,d0Total-PageSize));
            var visible=all.Skip(startIndex).Take(PageSize).ToArray();
            d0VisibleItems=visible.Length;
            int count=visible.Length+1;
            var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);
            for(int i=0;i<count;i++)
            {
                bool add=i==visible.Length;
                Guid? id=add?null:visible[i].Id;
                string name=add?(all.Count==0?"+ Apps":"+ New"):visible[i].Name;
                string kind=add?(all.Count==0?"manageapps":"newcollection"):"collection";
                string tip=add?(all.Count==0?"Add structured applications or create manual info":"Create a named information collection"):name;
                drawItems.Add((name,kind,id,"","",layout.From(i),layout.To(i),0,tip,add));
            }
        }

        var rawShapes=drawItems.Select(x=>InfoSector(x.Depth,x.A,x.B)).ToArray();
        arcBounds=DeskSpaceSubArcGeometryV48.Bounds(rawShapes);
        arcCanvas.Width=arcBounds.Width+ArcMargin*2;
        arcCanvas.Height=arcBounds.Height+ArcMargin*2;

        var clippedGeoms=new Geometry[drawItems.Count];
        var d0ScrollWedges=new List<System.Windows.Shapes.Path>();
        var d1ScrollWedges=new List<System.Windows.Shapes.Path>();

        // Pass 1: Wedge paths at Z=1
        for(int i=0;i<drawItems.Count;i++)
        {
            var item=drawItems[i];
            Geometry hit=DeskSpaceSubArcGeometryV48.Clip(rawShapes[i],arcBounds,ArcMargin);
            clippedGeoms[i]=hit;
            bool isSelectedParent=item.Kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                string.Equals(item.AppPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase);
            Brush fill=item.IsAction?Accent:(isSelectedParent?ChildHover:ChildIdle);
            var sector=new System.Windows.Shapes.Path{
                Data=hit,
                Stroke=Brushes.White,StrokeThickness=item.Depth==0?2:1.5,
                Fill=fill,
                Tag=i,Cursor=Cursors.Hand,
                ToolTip=item.Depth==0 && item.Kind=="collection"?(object)item.Tip:CreateCompactTooltip(item.Label,item.Tip)};
            ToolTipService.SetInitialShowDelay(sector,item.Depth>=1?600:800);
            Panel.SetZIndex(sector,1);
            arcCanvas.Children.Add(sector);
            childWedges.Add(sector);
            double midAngle=(item.A+item.B)/2.0;
            arcHitZones.Add((i,item.Depth,item.Kind,item.Id,item.AppPath,item.FilePath,midAngle,hit));

            if(item.Depth==0 && i<d0VisibleItems)
                d0ScrollWedges.Add(sector);
            else if(item.Depth==1 && d1VisibleItems>0 && d1ScrollWedges.Count<d1VisibleItems)
                d1ScrollWedges.Add(sector);
        }

        // Pass 2: Icons & Labels at Z=10
        for(int i=0;i<drawItems.Count;i++)
        {
            var item=drawItems[i];
            if(item.Depth==0 && item.Kind is "collection" or "newcollection")
            {
                double rMidClassic=(InfoInner(0)+InfoOuter(0))/2;
                var p=DeskSpaceSubArcGeometryV48.Polar(rMidClassic,(item.A+item.B)/2);
                double x=DeskSpaceSubArcGeometryV48.CX+p.X-arcBounds.Left+ArcMargin;
                double y=DeskSpaceSubArcGeometryV48.CY+p.Y-arcBounds.Top+ArcMargin;
                var label=new TextBlock{
                    Text=item.Label,
                    Width=62,
                    TextAlignment=TextAlignment.Center,
                    TextTrimming=TextTrimming.CharacterEllipsis,
                    FontSize=11,
                    FontWeight=item.IsAction?FontWeights.SemiBold:FontWeights.Normal,
                    Foreground=item.IsAction?Brushes.White:Ink,
                    ToolTip=item.Tip,
                    IsHitTestVisible=false};
                Panel.SetZIndex(label,10);
                Canvas.SetLeft(label,x-31);
                Canvas.SetTop(label,y-9);
                arcCanvas.Children.Add(label);
                continue;
            }

            double rMid=(InfoInner(item.Depth)+InfoOuter(item.Depth))/2;
            double midA=(item.A+item.B)/2;
            var pt=DeskSpaceSubArcGeometryV48.Polar(rMid,midA);
            double midX=DeskSpaceSubArcGeometryV48.CX+pt.X-arcBounds.Left+ArcMargin;
            double midY=DeskSpaceSubArcGeometryV48.CY+pt.Y-arcBounds.Top+ArcMargin;
            ImageSource? iconSrc=item.Kind=="infoapp"?DeskSpaceApplicationIconsV65.Get(item.AppPath):null;
            bool hasIcon=iconSrc!=null;
            double maxLabelH=item.Depth>=1?30:(hasIcon?22:28);
            double probeHalfH=item.Depth>=1?13:(hasIcon?16:12);
            double pad=item.Depth>=1?7:4;
            double maxW=item.Depth>=1?112:66;
            var (boxLeft,boxWidth)=InscribedHorizontalSpan(
                clippedGeoms[i],midX,midY,probeHalfH,pad,item.Depth>=1?44:34,maxW,arcCanvas.Width);
            double unitCenterX=boxLeft+boxWidth*.5;

            var lbl=new TextBlock{
                Text=item.Label,
                Width=boxWidth,
                MaxHeight=maxLabelH,
                FontSize=item.Depth==0?(hasIcon?9.5:10):10,
                FontWeight=item.IsAction?FontWeights.SemiBold:FontWeights.Normal,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                Foreground=item.IsAction?Brushes.White:Ink,
                IsHitTestVisible=false};
            lbl.Measure(new Size(boxWidth,maxLabelH));
            double actualH=Math.Min(maxLabelH,Math.Max(12,lbl.DesiredSize.Height));
            if(hasIcon)
            {
                const double iconSize=17,gap=2;
                double totalH=iconSize+gap+actualH;
                double topY=Math.Clamp(midY-totalH*.5,2,Math.Max(2,arcCanvas.Height-totalH-2));
                var appIcon=new Image{Source=iconSrc!,Width=iconSize,Height=iconSize,
                    Stretch=Stretch.Uniform,IsHitTestVisible=false};
                Panel.SetZIndex(appIcon,10);
                Canvas.SetLeft(appIcon,Math.Clamp(unitCenterX-iconSize*.5,2,Math.Max(2,arcCanvas.Width-iconSize-2)));
                Canvas.SetTop(appIcon,topY);
                arcCanvas.Children.Add(appIcon);
                Canvas.SetLeft(lbl,boxLeft);
                Canvas.SetTop(lbl,topY+iconSize+gap);
            }
            else
            {
                Canvas.SetLeft(lbl,boxLeft);
                Canvas.SetTop(lbl,Math.Clamp(midY-actualH*.5,2,Math.Max(2,arcCanvas.Height-actualH-2)));
            }
            Panel.SetZIndex(lbl,10);
            arcCanvas.Children.Add(lbl);
        }

        // Pass 3: Hidden counters for Depth-0 and Depth-1
        if(d0ScrollWedges.Count>0 && d0Total>Depth0PageSize())
        {
            int hiddenBefore=startIndex;
            int hiddenAfter=Math.Max(0,d0Total-startIndex-d0ScrollWedges.Count);
            DeskSpaceSubDonutStandardV62.DrawHiddenCountersForWedges(
                arcCanvas,d0ScrollWedges.ToArray(),hiddenBefore,hiddenAfter,
                ()=>Slide(-1),()=>Slide(1));
        }
        if(d1ScrollWedges.Count>0 && d1Total>FilePageSize)
        {
            int hiddenBefore=filePage;
            int hiddenAfter=Math.Max(0,d1Total-filePage-d1ScrollWedges.Count);
            DeskSpaceSubDonutStandardV62.DrawHiddenCountersForWedges(
                arcCanvas,d1ScrollWedges.ToArray(),hiddenBefore,hiddenAfter,
                ()=>SlideFiles(-1),()=>SlideFiles(1));
        }

        arcCanvas.MouseWheel-=ScrollArc;
        arcCanvas.MouseWheel+=ScrollArc;
        PositionArc();
    }
    private int SlotAtPoint(Point point)
    {
        for(int i=arcHitZones.Count-1;i>=0;i--)
            if(arcHitZones[i].hit.FillContains(point))return arcHitZones[i].index;
        return -1;
    }
    // Parent donut owns mouse capture during a press-and-drag. Its WPF
    // MouseMove/MouseUp never arrives in the child Popup's separate HWND.
    // Forward screen coordinates from WidgetHost and convert them into the
    // SAME local coordinate system used by direct click hit-testing.
    public bool TryCapturedDragAtScreen(Point screenPoint,bool released)
    {
        if(disposed || !arc.IsOpen || !arcCanvas.IsLoaded)return false;
        Point local;
        try{local=arcCanvas.PointFromScreen(screenPoint);}
        catch(InvalidOperationException){return false;}
        int slot=SlotAtPoint(local);
        if(slot<0)return false;
        capturedDragSelection=true;
        var target=arcHitZones[slot];
        if(slot!=hoveredChild)HoverChild(slot,target.id);
        if(released)
        {
            newHoverTimer.Stop();
            capturedDragSelection=false;
            ActivateChild(slot,target.id);
        }
        return true;
    }
    public void EndCapturedDrag()
    {
        capturedDragSelection=false;
        if(!editorActive && hoveredChild>=0 && hoveredChild<arcHitZones.Count &&
           arcHitZones[hoveredChild].depth==0 && hoveredChild==arcHitZones.Count-1)
            newHoverTimer.Stop();
    }

    private void PollPointer()
    {
        if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(owner))
        {
            newHoverTimer.Stop();
            return;
        }
        if(disposed || !arc.IsOpen || !arcCanvas.IsLoaded ||
           editorActive || contextMenuOpen)return;
        try
        {
            // All modules use the same DPI-aware wheel cursor source.
            var point=DeskSpaceWheelHoverV35.MouseIn(arcCanvas);
            ProcessWheelPointer(point);
        }
        catch(InvalidOperationException){} // popup is between HWND layouts
    }
    private void OnArcMouseMove(object sender,MouseEventArgs e)
    {
        if(!arc.IsOpen || disposed || contextMenuOpen)return;
        ProcessWheelPointer(e.GetPosition(arcCanvas));
    }
    private void ProcessWheelPointer(Point point)
    {
        if(disposed || !arc.IsOpen || editorActive || contextMenuOpen)return;
        double cw=arcCanvas.Width,ch=arcCanvas.Height;
        double edgeBand=Math.Min(48,cw*0.14);
        int direction=arcApps.Count==0 && point.Y>=0 && point.Y<=ch ?
            point.X<=edgeBand && CanPage(-1)?-1:
            point.X>=cw-edgeBand && CanPage(1)?1:0:0;
        int overItem=SlotAtPoint(point);
        if(overItem>=0)
        {
            StopEdgeScroll();
            if(overItem!=hoveredChild)
                HoverChild(overItem,arcHitZones[overItem].id);
            return;
        }
        if(direction!=0)
        {
            newHoverTimer.Stop();
            EndArcHover();
            StartEdgeScroll(direction);
            return;
        }
        StopEdgeScroll();
        int slot=SlotAtPoint(point);
        if(slot<0){if(hoveredChild>=0)EndArcHover();return;}
        if(slot==hoveredChild)return;
        HoverChild(slot,arcHitZones[slot].id);
    }
    private void EndArcHover()
    {
        newHoverTimer.Stop();
        CloseTransientPreview();
        if(hoveredChild>=0 && hoveredChild<childWedges.Count)
        {
            for(int i=0;i<childWedges.Count;i++)
            {
                var z=arcHitZones[i];
                bool isSelApp=z.kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                    string.Equals(z.appPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase);
                bool isAction=z.kind is "manageapps" or "newcollection" or "choosefields";
                childWedges[i].Fill=isAction?Accent:(isSelApp?ChildHover:ChildIdle);
            }
        }
        hoveredChild=-1;
    }
    private static bool IsClickInsideButtonControl(DependencyObject? dep,DependencyObject root)
    {
        while(dep!=null && !ReferenceEquals(dep,root))
        {
            if(dep is Button)return true;
            dep=dep is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }
        return false;
    }
    private void OnArcPointerDown(object sender,MouseButtonEventArgs e)
    {
        if(!arc.IsOpen)return;
        if(IsClickInsideButtonControl(e.OriginalSource as DependencyObject,arcCanvas))return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0)return;
        arcPressed=true;
        ActivateChild(slot,arcHitZones[slot].id);
        e.Handled=true;
    }
    private void OnArcPointerUp(object sender,MouseButtonEventArgs e)
    {
        if(arcPressed){arcPressed=false;e.Handled=true;return;}
        if(!arc.IsOpen)return;
        if(IsClickInsideButtonControl(e.OriginalSource as DependencyObject,arcCanvas))return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0)return;
        ActivateChild(slot,arcHitZones[slot].id);
        e.Handled=true;
    }
    private void OnArcRightButtonDown(object sender,MouseButtonEventArgs e)
    {
        if(disposed || !arc.IsOpen)return;
        int slot=SlotAtPoint(e.GetPosition(arcCanvas));
        if(slot<0 || slot>=arcHitZones.Count)return;
        var zone=arcHitZones[slot];
        string appPath=!string.IsNullOrEmpty(zone.appPath)
            ? zone.appPath
            : (arcApps.Count==1?arcApps[0].Path:"");
        if(string.IsNullOrEmpty(appPath))return;

        var elig=DeskSpaceInformationTabV380.EvaluateAppEligibility(appPath);
        string appLabel=elig.AppDisplayName;
        var cm=new ContextMenu();

        if(zone.kind=="infofile" && !string.IsNullOrEmpty(zone.filePath))
        {
            string capApp=appPath,capFile=zone.filePath;
            var viewItem=new MenuItem{Header="Show Selected Fields Below Circle"};
            viewItem.Click+=(_,_)=>{
                OpenSelectedAppFileBelowCircle(capApp,capFile);
                CommitChildSelection();
            };
            cm.Items.Add(viewItem);
        }

        var fieldsItem=new MenuItem{Header=$"⚙ Choose Visible Fields for {appLabel}…"};
        fieldsItem.Click+=(_,_)=>{
            OpenChooseFieldsShelf(appPath,zone.kind=="infofile" && !string.IsNullOrEmpty(zone.filePath)?zone.filePath:null);
            CommitChildSelection();
        };
        cm.Items.Add(fieldsItem);

        var manageItem=new MenuItem{Header="+ Add / Manage Information Apps…"};
        manageItem.Click+=(_,_)=>{
            OpenManageInfoAppsShelf();
            CommitChildSelection();
        };
        cm.Items.Add(manageItem);

        cm.Items.Add(new Separator());
        var removeItem=new MenuItem{Header=$"✕ Remove {appLabel} from Information"};
        removeItem.Click+=(_,_)=>{
            DeskSpaceInformationTabV380.RemoveAppFromInformation(appPath);
            RefreshArcAppsFromStore(appPath);
        };
        cm.Items.Add(removeItem);

        cm.PlacementTarget=arcCanvas;
        contextMenuOpen=true;
        cm.Closed+=(_,_)=>contextMenuOpen=false;
        cm.IsOpen=true;
        e.Handled=true;
    }

    private void RefreshArcAppsFromStore(string? removedAppPath=null)
    {
        arcApps=DeskSpaceInformationTabV380.GetSelectedAppsForInformation();
        warmedAppsRev=DeskSpaceInformationTabV380.InformationAppsRevision;
        warmedConfigStampUtc=DeskSpaceInformationTabV380.GetInformationConfigStampUtc();
        if(!string.IsNullOrEmpty(removedAppPath) &&
           string.Equals(activeArcAppPath,removedAppPath,StringComparison.OrdinalIgnoreCase))
        {
            activeArcAppPath="";
            filePage=0;
        }
        startIndex=0;
        if(arcApps.Count==1)
            EnsureAppInfoLoadedForArc(arcApps[0].Path);
        if(arc.IsOpen)
            RenderArc();
        changed();
    }

    private static readonly Brush ChildIdle =
        new SolidColorBrush(Color.FromRgb(220,229,251));
    private static readonly Brush ChildHover =
        new SolidColorBrush(Color.FromRgb(159,185,237));

    private void SelectArcApp(string appPath,double centerAngle)
    {
        if(string.IsNullOrWhiteSpace(appPath))return;
        bool sameApp=string.Equals(activeArcAppPath,appPath,StringComparison.OrdinalIgnoreCase);
        if(sameApp)
        {
            if(Math.Abs(activeArcAppAngle-centerAngle)>=0.5)
            {
                activeArcAppAngle=centerAngle;
                RenderArc();
            }
            return;
        }
        activeArcAppPath=appPath;
        activeArcAppAngle=centerAngle;
        filePage=0;
        EnsureAppInfoLoadedForArc(appPath);
        RenderArc();
    }

    private void HoverChild(int sector,Guid? id)
    {
        if(disposed || !arc.IsOpen || sector<0 || sector>=arcHitZones.Count)return;
        bool enteredNewSector=hoveredChild!=sector;
        if(enteredNewSector)newHoverTimer.Stop();
        hoveredChild=sector;
        var zone=arcHitZones[sector];
        keyboardIndex=arcApps.Count==0
            ?(sector==arcHitZones.Count-1?all.Count:startIndex+sector)
            :sector;
        for(int i=0;i<childWedges.Count;i++)
        {
            var w=childWedges[i];
            var z=arcHitZones[i];
            bool selected=i==sector || (z.kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                string.Equals(z.appPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase));
            bool isAction=z.kind is "manageapps" or "newcollection" or "choosefields";
            w.Fill=selected?(isAction?
                new SolidColorBrush(Color.FromRgb(73,58,153)):ChildHover):
                (isAction?Accent:ChildIdle);
        }
        if(editorActive)return;
        if(zone.kind=="infoapp")
        {
            CloseTransientPreview();
            SelectArcApp(zone.appPath,zone.midAngle);
            return;
        }
        if(zone.kind is "infofile" or "infoloading" or "appstatus" or "choosefields" or "manageapps")
            return;
        if(zone.kind=="collection" && (id??zone.id).HasValue)
        {
            transientPreview=true;
            OpenCollection((id??zone.id)!.Value);
        }
        else
            CloseTransientPreview();
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
        if(disposed || !arc.IsOpen || sector<0 || sector>=arcHitZones.Count)return;
        var zone=arcHitZones[sector];
        switch(zone.kind)
        {
            case "infoapp":
                CloseTransientPreview();
                SelectArcApp(zone.appPath,zone.midAngle);
                return;
            case "infofile":
                if(editorActive)return;
                newHoverTimer.Stop();
                transientPreview=false;
                OpenSelectedAppFileBelowCircle(zone.appPath,zone.filePath);
                CommitChildSelection();
                return;
            case "choosefields":
                if(editorActive)return;
                newHoverTimer.Stop();
                transientPreview=false;
                OpenChooseFieldsShelf(zone.appPath);
                CommitChildSelection();
                return;
            case "appstatus":
                if(editorActive)return;
                newHoverTimer.Stop();
                transientPreview=false;
                OpenManageInfoAppsShelf(zone.appPath);
                CommitChildSelection();
                return;
            case "manageapps":
                if(editorActive)return;
                newHoverTimer.Stop();
                CloseTransientPreview();
                OpenManageInfoAppsShelf(zone.appPath);
                CommitChildSelection();
                return;
            case "newcollection":
                newHoverTimer.Stop();
                CloseTransientPreview();
                if(editorActive)return;
                NewCollection();
                CommitChildSelection();
                return;
            case "collection":
                if(!editorActive && (id??zone.id).HasValue)
                {
                    transientPreview=false;
                    OpenCollection((id??zone.id)!.Value);
                    CommitChildSelection();
                }
                return;
        }
    }
    private void CommitChildSelection()
    {
        StopEdgeScroll();
        // After popup input has released, collapse the parent window.
        // Do not Hide(): only the arc closes; shelf stays attached below
        // the now-compact centre circle.
        owner.Dispatcher.BeginInvoke(new Action(()=>{
            if(disposed)return;
            onCommitted();
            if(editorActive && shelf.IsOpen)
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(!disposed && editorActive)
                        Keyboard.Focus(editorNameField is not null?
                            (IInputElement)editorNameField:(IInputElement)shelfBorder);
                }),DispatcherPriority.ContextIdle);
        }),DispatcherPriority.Background);
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
            int total=Depth0TotalCount();
            int d0ZoneCount=arcHitZones.Count(z=>z.depth==0);
            if(total==0){if(d0ZoneCount>0)HoverChild(d0ZoneCount-1,null);e.Handled=true;return;}
            int delta=e.Key==Key.Left || e.Key==Key.Up?-1:1;
            keyboardIndex=Math.Clamp(keyboardIndex+delta,0,total);
            if(keyboardIndex>=total)
            {
                int actionIdx=arcHitZones.FindLastIndex(z=>z.depth==0);
                if(actionIdx>=0)HoverChild(actionIdx,null);
            }
            else
            {
                startIndex=Math.Clamp(keyboardIndex-2,0,Math.Max(0,total-Depth0PageSize()));
                RenderArc();
                int slot=keyboardIndex-startIndex;
                if(slot>=0 && slot<arcHitZones.Count)
                    HoverChild(slot,arcHitZones[slot].id);
            }
            e.Handled=true;return;
        }
        if(e.Key==Key.Enter || e.Key==Key.Space)
        {
            int total=Depth0TotalCount();
            if(keyboardIndex==total)
            {
                int actionIdx=arcHitZones.FindLastIndex(z=>z.depth==0);
                if(actionIdx>=0)ActivateChild(actionIdx,null);
            }
            else if(keyboardIndex>=0 && keyboardIndex<total)
            {
                int slot=keyboardIndex-startIndex;
                if(slot>=0 && slot<arcHitZones.Count)
                    ActivateChild(slot,arcHitZones[slot].id);
            }
            e.Handled=true;
        }
    }
    private int SlotAtPointerOrHover()
    {
        if(arcCanvas.IsLoaded)
        {
            try
            {
                int s=SlotAtPoint(DeskSpaceWheelHoverV35.MouseIn(arcCanvas));
                if(s>=0)return s;
            }
            catch(InvalidOperationException){}
        }
        return hoveredChild;
    }
    private void ScrollArc(object sender,MouseWheelEventArgs e)
    {
        int amount=standardWheel.Step(e.Delta);
        if(amount!=0)HandleScrollStep(amount,SlotAtPointerOrHover());
        e.Handled=true;
    }
    private void HandleScrollStep(int delta,int slot)
    {
        if(delta==0)return;
        bool d0Overflow=CanPage(delta);
        bool d1Overflow=CanPageFiles(delta);
        if(arcApps.Count>=2 && !string.IsNullOrEmpty(activeArcAppPath))
        {
            if(slot>=0 && slot<arcHitZones.Count && arcHitZones[slot].depth==1 && d1Overflow)
            {
                SlideFiles(delta);
                return;
            }
            if(slot>=0 && slot<arcHitZones.Count && arcHitZones[slot].depth==0 && d0Overflow)
            {
                Slide(delta);
                return;
            }
            if(d1Overflow){SlideFiles(delta);return;}
        }
        if(d0Overflow)Slide(delta);
    }
    private bool CanPage(int direction) =>
        direction<0?startIndex>0:startIndex+Depth0PageSize()<Depth0TotalCount();
    private bool CanPageFiles(int direction) =>
        direction<0?filePage>0:filePage+FilePageSize<ActiveDepth1TotalCount();
    private void StartEdgeScroll(int direction)
    {
        if(!CanPage(direction)){StopEdgeScroll();return;}
        if(edgeDirection==direction && edgeTimer.IsEnabled)return;
        edgeDirection=direction;
        edgeTimer.Interval=TimeSpan.FromMilliseconds(175);
        newHoverTimer.Stop();
        edgeTimer.Start();
    }
    private void StopEdgeScroll()
    {
        edgeDirection=0;
        edgeTimer.Stop();
    }
    private void Slide(int delta)
    {
        CloseTransientPreview();
        if(!CanPage(delta)){StopEdgeScroll();return;}
        if(arcApps.Count>=2){activeArcAppPath="";filePage=0;}
        startIndex=Math.Clamp(startIndex+delta,0,Math.Max(0,Depth0TotalCount()-Depth0PageSize()));
        RenderArc();
        DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,-90);
        if(!CanPage(delta))StopEdgeScroll();
    }
    private void SlideFiles(int delta)
    {
        if(arcApps.Count<2 || string.IsNullOrEmpty(activeArcAppPath) || !CanPageFiles(delta))return;
        filePage=Math.Clamp(filePage+delta,0,Math.Max(0,ActiveDepth1TotalCount()-FilePageSize));
        RenderArc();
        DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,activeArcAppAngle);
    }
    private void Shelf(string title,double? customWidth=null,double? customMaxHeight=null,double? customScrollMax=null,bool showLegacyHeaderButtons=true)
    {
        MaskRevealed();
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        warning.Text="";
        headerAddBtn.Visibility=showLegacyHeaderButtons?Visibility.Visible:Visibility.Collapsed;
        headerAppsBtn.Visibility=showLegacyHeaderButtons?Visibility.Visible:Visibility.Collapsed;
        if(customWidth.HasValue)
        {
            shelfBorder.Width=customWidth.Value;
            if(customMaxHeight.HasValue)shelfBorder.MaxHeight=customMaxHeight.Value;
            if(customScrollMax.HasValue)shelfScroll.MaxHeight=customScrollMax.Value;
            shelfBody.Margin=new Thickness(8,3,8,6);
        }
        else
        {
            SetCompactShelfLayout(editorActive);
        }
        UpdateShelfPosition();
        shelf.IsOpen=true;
        // Refresh after visual measurement and after current ring animation.
        owner.Dispatcher.BeginInvoke(new Action(UpdateShelfPosition),
            DispatcherPriority.Loaded);
    }

    private void PositionArc()
    {
        if(disposed || arcBounds.IsEmpty)return;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        arcCanvas.LayoutTransform=new ScaleTransform(scale,scale);
        Point point=DeskSpaceSubArcGeometryV48.PopupOffset(owner,arcBounds,ArcMargin);
        arc.HorizontalOffset=point.X+(++positionTick%2)*0.001;
        arc.VerticalOffset=point.Y;
    }
    private void OwnerLocationChanged(object? sender,EventArgs e)
    {
        if(arc.IsOpen)PositionArc();
        if(shelf.IsOpen)UpdateShelfPosition();
    }
    private void OwnerSizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(owner.ActualWidth<300 || owner.ActualHeight<300)
            HideArc(); // Prevent detached Information fragments on collapse.
        else if(arc.IsOpen)PositionArc();
        if(shelf.IsOpen)UpdateShelfPosition();
    }
    private void SetCompactShelfLayout(bool editing)
    {
        // One-row collections should not reserve a 398x356 blank rectangle.
        shelfBorder.Width=editing?304:272;
        shelfBorder.MaxHeight=editing?310:186;
        shelfScroll.MaxHeight=editing?235:124;
        shelfBody.Margin=new Thickness(7,2,7,editing?6:2);
    }
    private void UpdateShelfPosition()
    {
        if(disposed)return;
        double diameter=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double height=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        if(double.IsNaN(diameter)||diameter<=0)diameter=owner.Width;
        if(double.IsNaN(height)||height<=0)height=owner.Height;
        double panelWidth=shelfBorder.Width;
        double x=(diameter-panelWidth)/2;
        // Constrain the shelf to the assigned monitor working area (DIPs).
        Rect rawWork=DeskSpaceSubArcGeometryV48.MonitorWorkArea(owner);
        var dpi=VisualTreeHelper.GetDpi(owner);
        double dx=dpi.DpiScaleX>0?dpi.DpiScaleX:1.0;
        double dy=dpi.DpiScaleY>0?dpi.DpiScaleY:1.0;
        var screen=new Rect(rawWork.Left/dx,rawWork.Top/dy,rawWork.Width/dx,rawWork.Height/dy);
        double left=owner.Left+x;
        if(left<screen.Left+8)x+=screen.Left+8-left;
        if(owner.Left+x+panelWidth>screen.Right-8)
            x-=(owner.Left+x+panelWidth)-(screen.Right-8);
        double y=height+5; // five-DIP gap under the currently visible circle
        double estShelfHeight=shelfBorder.ActualHeight>1?shelfBorder.ActualHeight:Math.Min(shelfBorder.MaxHeight,186);
        if(owner.Top+y+estShelfHeight>screen.Bottom-8)
            y=Math.Max(screen.Top+8-owner.Top,-estShelfHeight-5);
        // Force WPF Popup.UpdatePosition even during animation frames whose
        // geometric offsets may otherwise round to the same whole pixel.
        var epsilon=(++positionTick%2)*0.001;
        shelf.HorizontalOffset=x+epsilon;
        shelf.VerticalOffset=y+epsilon;
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
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(94)});
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
        var appInfoEntryBtn=SmallButton("Select App Files (Header → Value)…",()=>{
            editorActive=false;
            LeaveEditorKeyboardMode();
            OpenAppFileInfoShelf();
        });
        appInfoEntryBtn.ToolTip="Check an application's supported files for ≥ 2 matching inside-file Header → Value fields";
        appInfoEntryBtn.Margin=new Thickness(0,4,0,4);
        shelfBody.Children.Add(appInfoEntryBtn);
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
            LeaveEditorKeyboardMode();
            if(editingId!=null)OpenCollection(editingId.Value);
            else CloseShelf();
        }));
        shelfBody.Children.Add(commands);
        EnterEditorKeyboardMode(name);
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
            LeaveEditorKeyboardMode();
            RenderArc();changed();
            OpenCollection(updated.Id);
            warning.Text="";
        }
        catch(Exception ex){warning.Text=ex.Message;}
    }
    private int shelfAppScanGen;

    internal void OpenAppFileInfoShelf(string? appPath=null,bool keepTransient=false) =>
        OpenManageInfoAppsShelf(appPath,keepTransient);

    internal void OpenSelectedAppFileBelowCircle(string appPath,string filePath)
    {
        if(disposed || string.IsNullOrWhiteSpace(appPath) || string.IsNullOrWhiteSpace(filePath))return;
        editorActive=false;
        shownCollectionId=null;
        transientPreview=false;
        LeaveEditorKeyboardMode();

        string fileTitle=System.IO.Path.GetFileNameWithoutExtension(filePath);
        if(string.IsNullOrWhiteSpace(fileTitle))fileTitle=filePath;

        Shelf(fileTitle,364,340,268,showLegacyHeaderButtons:false);

        if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(appPath,out var cachedRes))
        {
            RenderSelectedFileRowsInShelf(appPath,filePath,cachedRes);
            return;
        }

        shelfBody.Children.Add(new TextBlock{
            Text="Loading inside-file Header → Value fields…",
            FontSize=11.5,Foreground=Ink,Margin=new Thickness(2,6,2,6)});

        int gen=++shelfAppScanGen;
        _=DeskSpaceInformationTabV380.GetAppInformationAsync(appPath).ContinueWith(t=>{
            var res=t.Status==System.Threading.Tasks.TaskStatus.RanToCompletion
                ? t.Result
                : DeskSpaceInformationTabV380.DiscoverAppInformation(appPath);
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(disposed || !shelf.IsOpen || gen!=shelfAppScanGen)return;
                RenderSelectedFileRowsInShelf(appPath,filePath,res);
            }));
        });
    }

    private void RenderSelectedFileRowsInShelf(string appPath,string filePath,AppInfoDiscoveryResultV380 res)
    {
        shelfBody.Children.Clear();
        if(!res.Eligibility.IsSupported || !res.MeetsMinimumTwoFilesRule)
        {
            shelfBody.Children.Add(new TextBlock{
                Text=res.StatusMessage,
                FontSize=11.5,Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,6,2,6)});
            return;
        }

        var file=res.MatchedFiles.FirstOrDefault(f=>string.Equals(f.FullPath,filePath,StringComparison.OrdinalIgnoreCase));
        if(file==null)
        {
            shelfBody.Children.Add(new TextBlock{
                Text="Selected file details could not be loaded.",
                FontSize=11.5,Foreground=Ink,Margin=new Thickness(2,6,2,6)});
            return;
        }

        shelfTitle.Text=file.DisplayName;
        var visibleSet=new HashSet<string>(res.VisibleHeaders,StringComparer.OrdinalIgnoreCase);
        var orderedVis=res.MatchedHeaders.Select(h=>h.Header).Where(visibleSet.Contains).ToList();
        var pairs=DeskSpaceInformationTabV380.GetVisibleFieldsForFile(file,orderedVis);

        var topTools=new DockPanel{LastChildFill=true,Margin=new Thickness(0,2,0,6)};
        var rightBtns=new StackPanel{Orientation=Orientation.Horizontal};

        var fieldsBtn=SmallButton("⚙ Fields",()=>OpenChooseFieldsShelf(appPath,file.FullPath));
        fieldsBtn.FontSize=10.5;fieldsBtn.MinHeight=22;fieldsBtn.Padding=new Thickness(7,2,7,2);
        fieldsBtn.ToolTip="Choose which Headers appear for "+res.Eligibility.AppDisplayName;

        var copyAllBtn=SmallButton("Copy All",()=>{
            if(pairs.Count==0)return;
            Clipboard.SetText(string.Join(Environment.NewLine,pairs.Select(kv=>$"{kv.Key}: {kv.Value}")));
            warning.Text=$"Copied {pairs.Count} fields from '{file.DisplayName}'.";
        });
        copyAllBtn.FontSize=10.5;copyAllBtn.MinHeight=22;copyAllBtn.Padding=new Thickness(7,2,7,2);
        copyAllBtn.ToolTip="Copy all visible Header : Value rows";

        var openBtn=SmallButton("↗ Open",()=>DeskSpacePinnedAppFilesV380.TryOpenQuickArcItem(appPath,file.FullPath,owner));
        openBtn.FontSize=10.5;openBtn.MinHeight=22;openBtn.Padding=new Thickness(7,2,7,2);
        openBtn.ToolTip=file.FullPath;

        rightBtns.Children.Add(fieldsBtn);
        rightBtns.Children.Add(copyAllBtn);
        rightBtns.Children.Add(openBtn);
        DockPanel.SetDock(rightBtns,Dock.Right);
        topTools.Children.Add(rightBtns);
        topTools.Children.Add(new TextBlock{
            Text=$"{res.Eligibility.AppDisplayName} · {pairs.Count} fields",
            FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(95,103,132)),
            VerticalAlignment=VerticalAlignment.Center,
            TextTrimming=TextTrimming.CharacterEllipsis});
        shelfBody.Children.Add(topTools);

        if(pairs.Count==0)
        {
            shelfBody.Children.Add(new TextBlock{
                Text="No visible headers selected. Click ⚙ Fields above to choose headers.",
                FontSize=11.5,Foreground=Ink,TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(2,6,2,6)});
            return;
        }

        for(int i=0;i<pairs.Count;i++)
        {
            var kv=pairs[i];
            var line=new Grid{Margin=new Thickness(4,3,4,3)};
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(128)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            line.Children.Add(new TextBlock{
                Text=kv.Key,FontSize=11,FontWeight=FontWeights.SemiBold,
                Foreground=Ink,VerticalAlignment=VerticalAlignment.Center,
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,6,0)});
            var valTb=new TextBlock{
                Text=kv.Value,FontSize=11.5,Foreground=Ink,
                VerticalAlignment=VerticalAlignment.Center,
                TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(0,0,6,0),ToolTip=kv.Value};
            Grid.SetColumn(valTb,1);line.Children.Add(valTb);
            string copyVal=kv.Value;
            string copyKey=kv.Key;
            var cpBtn=SmallButton("⧉",()=>{
                Clipboard.SetText(copyVal);
                warning.Text="Copied "+copyKey+".";
            });
            cpBtn.FontSize=10;cpBtn.MinHeight=21;cpBtn.Padding=new Thickness(6,1,6,1);
            cpBtn.ToolTip="Copy "+copyKey;
            Grid.SetColumn(cpBtn,2);line.Children.Add(cpBtn);

            shelfBody.Children.Add(new Border{
                Background=i%2==0?Pale:Brushes.White,
                CornerRadius=new CornerRadius(5),
                Margin=new Thickness(0,0,0,2),
                Child=line});
        }
        owner.Dispatcher.BeginInvoke(new Action(UpdateShelfPosition),DispatcherPriority.Loaded);
    }

    internal void OpenChooseFieldsShelf(string appPath,string? returnFilePath=null)
    {
        if(disposed || string.IsNullOrWhiteSpace(appPath))return;
        editorActive=false;
        shownCollectionId=null;
        transientPreview=false;
        LeaveEditorKeyboardMode();

        var elig=DeskSpaceInformationTabV380.EvaluateAppEligibility(appPath);
        Shelf("Fields · "+elig.AppDisplayName,368,360,288,showLegacyHeaderButtons:false);

        if(DeskSpaceInformationTabV380.TryGetCachedAppInformation(appPath,out var cachedRes))
        {
            RenderChooseFieldsBody(appPath,returnFilePath,cachedRes);
            return;
        }

        shelfBody.Children.Add(new TextBlock{
            Text="Loading matched headers…",
            FontSize=11.5,Foreground=Ink,Margin=new Thickness(2,6,2,6)});

        int gen=++shelfAppScanGen;
        _=DeskSpaceInformationTabV380.GetAppInformationAsync(appPath).ContinueWith(t=>{
            var res=t.Status==System.Threading.Tasks.TaskStatus.RanToCompletion
                ? t.Result
                : DeskSpaceInformationTabV380.DiscoverAppInformation(appPath);
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(disposed || !shelf.IsOpen || gen!=shelfAppScanGen)return;
                RenderChooseFieldsBody(appPath,returnFilePath,res);
            }));
        });
    }

    private void RenderChooseFieldsBody(string appPath,string? returnFilePath,AppInfoDiscoveryResultV380 res)
    {
        shelfBody.Children.Clear();
        if(!res.Eligibility.IsSupported || !res.MeetsMinimumTwoFilesRule)
        {
            shelfBody.Children.Add(new TextBlock{
                Text=res.StatusMessage,FontSize=11.5,
                Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,4,2,6)});
            return;
        }

        var visibleSet=new HashSet<string>(res.VisibleHeaders,StringComparer.OrdinalIgnoreCase);
        bool suppressHeaderEvents=false;
        var headerCheckBoxes=new List<(string Header,CheckBox Box)>();

        void SavePrefAndCount(TextBlock statusTb)
        {
            DeskSpaceInformationTabV380.SaveVisibleHeadersPreference(appPath,visibleSet);
            statusTb.Text=$"{visibleSet.Count} of {res.MatchedHeaders.Count} headers visible";
        }

        var statusLbl=new TextBlock{
            Text=$"{visibleSet.Count} of {res.MatchedHeaders.Count} headers visible",
            FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Ink,
            VerticalAlignment=VerticalAlignment.Center};

        void ApplyBatchHeaders(IEnumerable<string> nextSet)
        {
            suppressHeaderEvents=true;
            try
            {
                visibleSet.Clear();
                foreach(string h in nextSet)visibleSet.Add(h);
                foreach(var (hName,cb) in headerCheckBoxes)
                    cb.IsChecked=visibleSet.Contains(hName);
                SavePrefAndCount(statusLbl);
            }
            finally{suppressHeaderEvents=false;}
        }

        var hdrBar=new DockPanel{LastChildFill=true,Margin=new Thickness(0,2,0,4)};
        var hdrQuick=new StackPanel{Orientation=Orientation.Horizontal};
        if(!string.IsNullOrEmpty(returnFilePath))
        {
            var backBtn=SmallButton("← Back",()=>OpenSelectedAppFileBelowCircle(appPath,returnFilePath));
            backBtn.Background=Accent;backBtn.Foreground=Brushes.White;
            backBtn.FontSize=10;backBtn.MinHeight=21;backBtn.Padding=new Thickness(6,1,6,1);
            hdrQuick.Children.Add(backBtn);
        }
        var hAllBtn=SmallButton("All",()=>ApplyBatchHeaders(res.MatchedHeaders.Select(h=>h.Header)));
        hAllBtn.FontSize=10;hAllBtn.MinHeight=21;hAllBtn.Padding=new Thickness(5,1,5,1);
        var hSumBtn=SmallButton("Summary",()=>ApplyBatchHeaders(DeskSpaceInformationTabV380.GetSummaryPriorityHeaders(res.MatchedHeaders)));
        hSumBtn.FontSize=10;hSumBtn.MinHeight=21;hSumBtn.Padding=new Thickness(5,1,5,1);
        var hClrBtn=SmallButton("Clear",()=>ApplyBatchHeaders(Array.Empty<string>()));
        hClrBtn.FontSize=10;hClrBtn.MinHeight=21;hClrBtn.Padding=new Thickness(5,1,5,1);
        hdrQuick.Children.Add(hAllBtn);
        hdrQuick.Children.Add(hSumBtn);
        hdrQuick.Children.Add(hClrBtn);
        DockPanel.SetDock(hdrQuick,Dock.Right);
        hdrBar.Children.Add(hdrQuick);
        hdrBar.Children.Add(statusLbl);
        shelfBody.Children.Add(hdrBar);

        foreach(var hdr in res.MatchedHeaders)
        {
            string hName=hdr.Header;
            var cb=new CheckBox{
                Content=$"{hName} ({hdr.MatchingFileCount} files)",
                IsChecked=visibleSet.Contains(hName),
                FontSize=11.5,Foreground=Ink,
                Margin=new Thickness(2,2,2,3),
                ToolTip=$"Sample: {hdr.SampleValue}"};
            cb.Checked+=(_,_)=>{
                if(suppressHeaderEvents)return;
                visibleSet.Add(hName);
                SavePrefAndCount(statusLbl);
            };
            cb.Unchecked+=(_,_)=>{
                if(suppressHeaderEvents)return;
                visibleSet.Remove(hName);
                SavePrefAndCount(statusLbl);
            };
            headerCheckBoxes.Add((hName,cb));
            shelfBody.Children.Add(cb);
        }
        owner.Dispatcher.BeginInvoke(new Action(UpdateShelfPosition),DispatcherPriority.Loaded);
    }

    internal void OpenManageInfoAppsShelf(string? focusAppPath=null,bool keepTransient=false)
    {
        if(disposed)return;
        editorActive=false;
        shownCollectionId=null;
        transientPreview=keepTransient;
        LeaveEditorKeyboardMode();

        Shelf("Information Apps",364,360,288,showLegacyHeaderButtons:false);

        arcApps=DeskSpaceInformationTabV380.GetSelectedAppsForInformation();
        warmedAppsRev=DeskSpaceInformationTabV380.InformationAppsRevision;
        warmedConfigStampUtc=DeskSpaceInformationTabV380.GetInformationConfigStampUtc();

        var modeNote=arcApps.Count==1
            ? "1 app selected — Information wheel directly shows its files."
            : arcApps.Count>=2
                ? $"{arcApps.Count} apps selected — Hover an app wedge to open its files ring."
                : "No apps selected yet. Add an eligible structured app below.";
        shelfBody.Children.Add(new TextBlock{
            Text=modeNote,FontSize=11,
            Foreground=new SolidColorBrush(Color.FromRgb(95,103,132)),
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,2,2,6)});

        foreach(var a in arcApps)
        {
            string capPath=a.Path;
            string capLabel=a.Label;
            bool isFocused=!string.IsNullOrEmpty(focusAppPath) &&
                string.Equals(capPath,focusAppPath,StringComparison.OrdinalIgnoreCase);

            var row=new DockPanel{LastChildFill=true,Margin=new Thickness(4,3,4,3)};
            var rowBtns=new StackPanel{Orientation=Orientation.Horizontal};

            var fieldsBtn=SmallButton("Fields",()=>OpenChooseFieldsShelf(capPath));
            fieldsBtn.FontSize=10;fieldsBtn.MinHeight=21;fieldsBtn.Padding=new Thickness(6,1,6,1);
            fieldsBtn.ToolTip="Choose visible Header → Value fields for "+capLabel;

            var rmBtn=SmallButton("✕ Remove",()=>{
                DeskSpaceInformationTabV380.RemoveAppFromInformation(capPath);
                RefreshArcAppsFromStore(capPath);
                OpenManageInfoAppsShelf();
            });
            rmBtn.FontSize=10;rmBtn.MinHeight=21;rmBtn.Padding=new Thickness(6,1,6,1);
            rmBtn.ToolTip="Remove "+capLabel+" from Information";

            rowBtns.Children.Add(fieldsBtn);
            rowBtns.Children.Add(rmBtn);
            DockPanel.SetDock(rowBtns,Dock.Right);
            row.Children.Add(rowBtns);

            var namePanel=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            var ic=DeskSpaceApplicationIconsV65.Get(capPath);
            if(ic!=null)
            {
                namePanel.Children.Add(new Image{
                    Source=ic,Width=16,Height=16,Stretch=Stretch.Uniform,
                    Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center});
            }
            namePanel.Children.Add(new TextBlock{
                Text=capLabel,FontSize=12,
                FontWeight=isFocused?FontWeights.Bold:FontWeights.SemiBold,
                Foreground=Ink,VerticalAlignment=VerticalAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis});
            row.Children.Add(namePanel);

            shelfBody.Children.Add(new Border{
                Background=isFocused?new SolidColorBrush(Color.FromRgb(226,222,250)):Pale,
                CornerRadius=new CornerRadius(6),
                Margin=new Thickness(0,0,0,4),
                Child=row});
        }

        var candidates=DeskSpaceInformationTabV380.GetCandidateEligibleAppsToAdd();
        if(candidates.Count>0)
        {
            shelfBody.Children.Add(new TextBlock{
                Text="Quick-Add Supported Apps:",
                FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Ink,
                Margin=new Thickness(2,5,2,3)});
            var addWrap=new WrapPanel{Margin=new Thickness(0,0,0,6)};
            foreach(var cand in candidates)
            {
                string capCand=cand.Path;
                var addCandBtn=SmallButton("+ "+cand.Label,()=>{
                    if(DeskSpaceInformationTabV380.TryAddAppToInformation(capCand,out _))
                    {
                        RefreshArcAppsFromStore();
                        OpenManageInfoAppsShelf(capCand);
                    }
                });
                addCandBtn.FontSize=10.5;addCandBtn.MinHeight=22;addCandBtn.Padding=new Thickness(7,2,7,2);
                addWrap.Children.Add(addCandBtn);
            }
            shelfBody.Children.Add(addWrap);
        }

        var footerRow=new WrapPanel{Margin=new Thickness(0,4,0,2)};
        var browseBtn=SmallButton("+ Browse App…",()=>{
            var dlg=new Microsoft.Win32.OpenFileDialog{
                Title="Add an application to Information (checks Header → Value support)",
                Filter="Applications & Shortcuts|*.exe;*.lnk|All Files|*.*"};
            if(dlg.ShowDialog(owner)==true)
            {
                if(DeskSpaceInformationTabV380.TryAddAppToInformation(dlg.FileName,out var elig))
                {
                    RefreshArcAppsFromStore();
                    OpenManageInfoAppsShelf(dlg.FileName);
                }
                else
                {
                    warning.Text="✕ "+elig.Reason;
                }
            }
        });
        browseBtn.Background=Accent;browseBtn.Foreground=Brushes.White;
        browseBtn.FontSize=10.5;browseBtn.MinHeight=23;
        footerRow.Children.Add(browseBtn);

        var manualBtn=SmallButton("+ Manual UI/PW",()=>NewCollection());
        manualBtn.FontSize=10.5;manualBtn.MinHeight=23;
        footerRow.Children.Add(manualBtn);

        if(all.Count>0)
        {
            var savedBtn=SmallButton($"Saved ({all.Count})",()=>OpenSavedCollectionsPickerShelf());
            savedBtn.FontSize=10.5;savedBtn.MinHeight=23;
            footerRow.Children.Add(savedBtn);
        }

        var fullBtn=SmallButton("Full ↗",()=>DeskSpaceInformationTabV380.SelectOrOpen(owner));
        fullBtn.FontSize=10.5;fullBtn.MinHeight=23;
        footerRow.Children.Add(fullBtn);
        shelfBody.Children.Add(footerRow);
        owner.Dispatcher.BeginInvoke(new Action(UpdateShelfPosition),DispatcherPriority.Loaded);
    }

    private void OpenSavedCollectionsPickerShelf()
    {
        if(disposed)return;
        editorActive=false;
        shownCollectionId=null;
        transientPreview=false;
        LeaveEditorKeyboardMode();
        Shelf($"Saved Manual Info ({all.Count})");
        shelfBorder.Width=340;
        shelfBorder.MaxHeight=320;
        shelfScroll.MaxHeight=250;
        shelfBody.Margin=new Thickness(8,3,8,6);
        UpdateShelfPosition();

        foreach(var c in all)
        {
            Guid cid=c.Id;
            var btn=SmallButton($"{c.Name} ({c.Rows.Count})",()=>OpenCollection(cid));
            btn.HorizontalAlignment=HorizontalAlignment.Stretch;
            btn.HorizontalContentAlignment=HorizontalAlignment.Left;
            shelfBody.Children.Add(btn);
        }
        shelfBody.Children.Add(SmallButton("+ New Manual UI/PW",()=>NewCollection()));
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
        owner.LocationChanged-=OwnerLocationChanged;
        owner.SizeChanged-=OwnerSizeChanged;
        LeaveEditorKeyboardMode();
        MaskRevealed();
        arcCanvas.MouseWheel-=ScrollArc;
        newHoverTimer.Stop();edgeTimer.Stop();pointerTimer.Stop();lockTimer.Stop();
        middleScroll.Dispose();
        vault.Dispose();
    }
}
