$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# R380: Copy DeskSpaceDesignV283 + ModernV380 suite (all 4 modernization phases +
# Pinned App Compatible Files & Smart Launcher) into DeskSpace_Windows_R1 and
# wire surgical hooks without mutating MainWindow.xaml or DeskSpaceDropShelfV15.

$lf = [string][char]10

function Begin-Patch([string]$path) {
    if (!(Test-Path -LiteralPath $path)) {
        throw ('R380 missing source: ' + $path)
    }
    $script:code = [regex]::Replace([IO.File]::ReadAllText($path), '\r+\n?', $lf)
}

function Swap-R380([string]$old, [string]$replacement, [string]$context) {
    $oldNorm = [regex]::Replace($old, '\r+\n?', $lf)
    $repNorm = [regex]::Replace($replacement, '\r+\n?', $lf)
    $count = ([regex]::Matches($script:code, [regex]::Escape($oldNorm))).Count
    if ($count -ne 1) {
        throw ('R380 ' + $context + ': expected 1 anchor match, got ' + $count)
    }
    $script:code = $script:code.Replace($oldNorm, $repNorm)
}

function Finish-Patch([string]$path) {
    [IO.File]::WriteAllText($path, $script:code, [Text.UTF8Encoding]::new($false))
}

$dstRoot = 'DeskSpace_Windows_R1/DeskSpace'
if (!(Test-Path -LiteralPath $dstRoot)) {
    throw "R380 missing reconstructed app folder: $dstRoot"
}

# 1. Copy DeskSpaceDesignV283.cs (required by all ModernV380 UI modules; depends only on V148)
$designSrc = 'src/DeskSpace/DeskSpaceDesignV283.cs'
$designDst = "$dstRoot/DeskSpaceDesignV283.cs"
if (!(Test-Path -LiteralPath $designSrc)) {
    throw 'R380 missing src/DeskSpace/DeskSpaceDesignV283.cs'
}
Copy-Item -LiteralPath $designSrc -Destination $designDst -Force
if ((Get-FileHash $designSrc -Algorithm SHA256).Hash -ne (Get-FileHash $designDst -Algorithm SHA256).Hash) {
    throw 'R380 DeskSpaceDesignV283.cs copy SHA256 mismatch'
}

# 2. Copy all ModernV380/*.cs files and verify SHA-256 parity
$modernSrcDir = 'src/DeskSpace/ModernV380'
$modernDstDir = "$dstRoot/ModernV380"
if (!(Test-Path -LiteralPath $modernSrcDir)) {
    throw "R380 missing $modernSrcDir"
}
if (Test-Path -LiteralPath $modernDstDir) {
    Remove-Item -LiteralPath $modernDstDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $modernDstDir | Out-Null

$modernFiles = @(Get-ChildItem -LiteralPath $modernSrcDir -Filter '*.cs' -File | Sort-Object Name)
if ($modernFiles.Count -lt 17) {
    throw "R380 expected at least 17 ModernV380 source files, found $($modernFiles.Count)"
}
foreach ($file in $modernFiles) {
    $dstFile = Join-Path $modernDstDir $file.Name
    Copy-Item -LiteralPath $file.FullName -Destination $dstFile -Force
    if ((Get-FileHash $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $dstFile -Algorithm SHA256).Hash) {
        throw "R380 copy SHA256 mismatch for $($file.Name)"
    }
}

# 3. Surgical hook: MainWindow.xaml.cs
$mainPath = "$dstRoot/MainWindow.xaml.cs"
Begin-Patch $mainPath
Swap-R380 @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
'@ @'
        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);
        Loaded += (_,_) => DeskSpaceModernHubV380.AttachMainWindow(this, () => data ?? new Workspace(), SafeSave, RefreshDashboard);
'@ 'MainWindow Loaded modern hub attach'
Swap-R380 @'
        trayMenu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        tray.ContextMenuStrip = trayMenu;
'@ @'
        trayMenu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        DeskSpaceModernHubV380.AddMainTraySubmenu(trayMenu, this, () => data ?? new Workspace());
        tray.ContextMenuStrip = trayMenu;
'@ 'MainWindow tray submenu'
Swap-R380 @'
        DeskSpaceSettingsStudioV148.Show(this,settings,()=>{
            settings.Save();settings.ApplyWindowsStartup();
        },()=>new WidgetOptionsWindow(settings,
'@ @'
        DeskSpaceSettingsStudioV148.Show(this,settings,()=>{
            settings.Save();settings.ApplyWindowsStartup();DeskSpaceFluentBackdropV380.ReapplyLiveTheme();
        },()=>new WidgetOptionsWindow(settings,
'@ 'MainWindow WidgetSettings live theme refresh'
Swap-R380 @'
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        WidgetHostLauncher.OpenSearch();
    }
'@ @'
    private void PinnedApps_Click(object sender,RoutedEventArgs e)
    {
        DeskSpaceModernHubV380.ShowPinnedAppsSelector(this);
    }
'@ 'MainWindow PinnedApps selector routing'
Finish-Patch $mainPath

# 4. Surgical hook: WidgetHost.cs
$hostPath = "$dstRoot/WidgetHost.cs"
Begin-Patch $hostPath
Swap-R380 @'
            if (ringExpanded && informationOverlay?.IsArcOpen!=true && !ringGesture && !draggingGesture &&
'@ @'
            if (ringExpanded && informationOverlay?.IsArcOpen!=true && appsWheel?.IsArcOpen!=true && excelWheel?.IsArcOpen!=true && !ringGesture && !draggingGesture &&
'@ 'WidgetHost MouseLeave sub-arc protection'
Swap-R380 @'
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsArcOpen==true)return;
'@ @'
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsArcOpen==true || appsWheel?.IsArcOpen==true || excelWheel?.IsArcOpen==true)return;
'@ 'WidgetHost OnGestureMove sub-arc protection'
Swap-R380 @'
        // Leaving Information and moving into any other main sector closes
        // its old arc and passive shelf. Never discard an active editor.
        if(selected!=7 && informationOverlay?.IsOpen==true &&
            informationOverlay.IsEditing==false)informationOverlay.Hide();
        UpdateHighlightedSector(p);
        if(selected==7)
        {
            mainWheelSector=selected;mainWheelQuick=-1;
            return; // Information opens its own child wheel immediately
        }
'@ @'
        // Leaving Information and moving into any other main sector closes
        // its old arc and passive shelf. Never discard an active editor.
        if(selected!=6 && informationOverlay?.IsOpen==true &&
            informationOverlay.IsEditing==false)informationOverlay.Hide();
        UpdateHighlightedSector(p);
        if(selected==6 || selected==10 || selected==11)
        {
            mainWheelSector=selected;mainWheelQuick=-1;
            return; // Information, Applications and Excel open their own child arcs immediately
        }
'@ 'WidgetHost SampleMainWheel Information sector 6 flicker fix'
Swap-R380 @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
'@ @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
        DeskSpaceModernHubV380.AttachWidgetHost(this, menu);
'@ 'WidgetHost tray modern submenu and global hotkeys'
Swap-R380 @'
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
    }
'@ @'
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
        DeskSpaceFocusRingV380.AttachToDonutCanvas(canvas);
    }
'@ 'WidgetHost donut Focus Ring canvas attachment'
Swap-R380 @'
        if(name=="Excel Functions")
        {
            OpenExcelArc();
            return; // The circle must NEVER open the large Excel editor.
        }
        if(name=="Information")
        {
            OpenInformationArc();
            return;
        }
'@ @'
        if(name=="Excel Functions")
        {
            OpenExcelArc();
            return; // The circle must NEVER open the large Excel editor.
        }
        if(name=="Information")
        {
            OpenInformationArc();
            informationOverlay?.OpenAppFileInfoShelf();
            if(ringExpanded)ToggleRing();
            informationOverlay?.HideArc();
            return;
        }
'@ 'WidgetHost ClickSegment Information direct App Info shelf'
Finish-Patch $hostPath

# 4b. Surgical hook: DeskSpaceInformationV36.cs (tight concentric clipped bounds & monitor-aware shelf)
$infoPath = "$dstRoot/DeskSpaceInformationV36.cs"
Begin-Patch $infoPath
Swap-R380 @'
    private List<Collection> all=new();
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
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    all=snapshot;warmStamp=stamp;warmed=true;warming=false;
                    if(arc.IsOpen){RenderArc();PositionArc();changed();}
                }));
            }
'@ @'
    private List<Collection> all=new();
    private List<(string Label, string Path)> arcApps=new();
    private int ActiveArcTotalCount()=>all.Count>0?all.Count:arcApps.Count;
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
                var appsSnapshot=DeskSpaceInformationTabV380.GetAvailableAppsForInformation();
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    all=snapshot;arcApps=appsSnapshot;warmStamp=stamp;warmed=true;warming=false;
                    if(arc.IsOpen){RenderArc();PositionArc();changed();}
                }));
            }
'@ 'DeskSpaceInformationV36 arcApps WarmStart'
Swap-R380 '    public int SavedCount => all.Count;' '    public int SavedCount => ActiveArcTotalCount();' 'DeskSpaceInformationV36 SavedCount'
Swap-R380 '        arcCanvas=new Canvas{Width=488,Height=222,Focusable=true};' '        arcCanvas=new Canvas{Width=1,Height=1,Focusable=true};' 'DeskSpaceInformationV36 tight initial canvas'
Swap-R380 '            ()=>all.Count>PageSize,' '            ()=>ActiveArcTotalCount()>PageSize,' 'DeskSpaceInformationV36 middleScroll predicate'
Swap-R380 @'
    public void Show()
    {
        if(disposed)return;
        // Returning to Information starts with the wheel, not a previously
        // selected shelf. Never discard an active unsaved editor.
        if(shelf.IsOpen && !editorActive)CloseShelf();
        try
        {
            if(!warmed || CurrentStamp()!=warmStamp)
                WarmStart(); // immediately paint cache, refresh if stale
            startIndex=Math.Min(startIndex,Math.Max(0,all.Count-4));
            RenderArc();
'@ @'
    public void Show()
    {
        if(disposed)return;
        // Returning to Information starts with the wheel, not a previously
        // selected shelf. Never discard an active unsaved editor.
        if(shelf.IsOpen && !editorActive)CloseShelf();
        try
        {
            if(all.Count==0 && arcApps.Count==0)
                arcApps=DeskSpaceInformationTabV380.GetAvailableAppsForInformation();
            if(!warmed || CurrentStamp()!=warmStamp)
                WarmStart(); // immediately paint cache, refresh if stale
            startIndex=Math.Min(startIndex,Math.Max(0,ActiveArcTotalCount()-4));
            RenderArc();
'@ 'DeskSpaceInformationV36 Show app wedges fallback'
Swap-R380 @'
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
    private const int PageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private readonly DeskSpaceSubDonutStandardV62.Wheel standardWheel=new();
    private readonly List<(int index, Guid? id, Geometry hit)> arcHitZones=new();
    private bool arcPressed;
    private bool capturedDragSelection;
    private bool editorKeyboardMode;
    private TextBox? editorNameField;

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
        var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);
        double halfSweep=layout.Half;
        double start=layout.From(0);
        double step=layout.Step;
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
                Width=62,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                FontSize=11,
                FontWeight=add?FontWeights.SemiBold:FontWeights.Normal,
                Foreground=add?Brushes.White:Ink,
                ToolTip=name,
                IsHitTestVisible=false};
            Canvas.SetLeft(label,x-31);
            Canvas.SetTop(label,y-9);
            arcCanvas.Children.Add(label);
        }
        // + New is an action and is never the hidden-items divider.
        // Keep hidden counts on the last SAVED wedge's radial edge.
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count,startIndex,visible.Length,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
        DeskSpaceSubDonutStandardV62.DrawHiddenCounters(
            arcCanvas,countView,arcCanvas.Height/2,Slide);
        arcCanvas.MouseWheel-=ScrollArc;
        arcCanvas.MouseWheel+=ScrollArc;
    }
'@ @'
    private Rect arcBounds=Rect.Empty;
    private const double ArcMargin=6;
    private const int PageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private readonly DeskSpaceSubDonutStandardV62.Wheel standardWheel=new();
    private readonly List<(int index, Guid? id, Geometry hit)> arcHitZones=new();
    private bool arcPressed;
    private bool capturedDragSelection;
    private bool editorKeyboardMode;
    private TextBox? editorNameField;

    // Child sectors are placed ONLY across a partial arc centered over the
    // Information parent at the top of the existing outer ring, tightly clipped
    // to exact sector bounds so WPF/monitor fencing keeps them concentric.
    private void RenderArc()
    {
        newHoverTimer.Stop();
        arcCanvas.Children.Clear();
        childWedges.Clear();childLabels.Clear();arcHitZones.Clear();
        hoveredChild=-1;
        bool usingApps=all.Count==0 && arcApps.Count>0;
        var visible=all.Skip(startIndex).Take(PageSize).ToArray();
        var visibleApps=usingApps?arcApps.Skip(startIndex).Take(PageSize).ToArray():Array.Empty<(string Label,string Path)>();
        int itemCount=usingApps?visibleApps.Length:visible.Length;
        int totalItems=usingApps?arcApps.Count:all.Count;
        int count=itemCount+1;
        var layout=DeskSpaceSubDonutStandardV62.Plan(count,0,-90);
        double halfSweep=layout.Half;
        double start=layout.From(0);
        double step=layout.Step;
        var rawShapes=new Geometry[count];
        for(int i=0;i<count;i++)
            rawShapes[i]=DeskSpaceSubArcGeometryV48.Sector(0,start+step*i,start+step*(i+1));
        arcBounds=DeskSpaceSubArcGeometryV48.Bounds(rawShapes);
        arcCanvas.Width=arcBounds.Width+ArcMargin*2;
        arcCanvas.Height=arcBounds.Height+ArcMargin*2;
        double rMid=(DeskSpaceSubArcGeometryV48.Inner(0)+DeskSpaceSubArcGeometryV48.Outer(0))/2;
        for(int i=0;i<count;i++)
        {
            bool add=i==itemCount;
            Guid? id=(add||usingApps)?null:visible[i].Id;
            string name=add?(usingApps?"+ Apps":"+ New"):(usingApps?visibleApps[i].Label:visible[i].Name);
            string tip=add?(usingApps?"Select an application or create manual info":"Create a named information collection"):
                (usingApps?"Inspect "+visibleApps[i].Label+" inside-file details":name);
            double a=start+step*i,b=start+step*(i+1);
            Geometry hit=DeskSpaceSubArcGeometryV48.Clip(rawShapes[i],arcBounds,ArcMargin);
            var sector=new System.Windows.Shapes.Path{
                Data=hit,
                Stroke=Brushes.White,StrokeThickness=2,
                Fill=add?Accent:ChildIdle,
                Tag=i,Cursor=Cursors.Hand,
                ToolTip=tip};
            // Every visible sector is a true hit target; a single shared
            // Canvas pointer dispatcher handles all child selection.
            arcCanvas.Children.Add(sector);
            childWedges.Add(sector);
            arcHitZones.Add((i,id,hit));
            var p=DeskSpaceSubArcGeometryV48.Polar(rMid,(a+b)/2);
            double x=DeskSpaceSubArcGeometryV48.CX+p.X-arcBounds.Left+ArcMargin;
            double y=DeskSpaceSubArcGeometryV48.CY+p.Y-arcBounds.Top+ArcMargin;
            var label=new TextBlock{
                Text=name,
                Width=62,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                FontSize=11,
                FontWeight=add?FontWeights.SemiBold:FontWeights.Normal,
                Foreground=add?Brushes.White:Ink,
                ToolTip=tip,
                IsHitTestVisible=false};
            Canvas.SetLeft(label,x-31);
            Canvas.SetTop(label,y-9);
            arcCanvas.Children.Add(label);
        }
        // + New is an action and is never the hidden-items divider.
        // Keep hidden counts on the last SAVED wedge's radial edge.
        var countView=new DeskSpaceSubDonutStandardV62.View(
            totalItems,startIndex,itemCount,startIndex,
            Math.Max(0,totalItems-startIndex-itemCount),
            -90,halfSweep,step);
        DeskSpaceSubDonutStandardV62.DrawHiddenCounters(
            arcCanvas,countView,arcCanvas.Height/2,Slide);
        arcCanvas.MouseWheel-=ScrollArc;
        arcCanvas.MouseWheel+=ScrollArc;
        PositionArc();
    }
'@ 'DeskSpaceInformationV36 concentric tight RenderArc'
Swap-R380 @'
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
        if(editorActive)return;
        if(sector<arcHitZones.Count-1 && id.HasValue)
        {
            // Immediately preview the selected wedge, but do not collapse the
            // donut: the shelf is temporary until a deliberate selection.
            transientPreview=true;
            OpenCollection(id.Value);
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
        if(disposed || !arc.IsOpen)return;
        // A click, key activation or completed drag commits the child,
        // shrinking BOTH parent rings while keeping its shelf/editor.
        if(sector==arcHitZones.Count-1)
        {
            newHoverTimer.Stop();
            CloseTransientPreview();
            if(editorActive)return;
            NewCollection();
            CommitChildSelection();
            return;
        }
        if(id.HasValue && !editorActive)
        {
            transientPreview=false; // an explicit selection persists
            OpenCollection(id.Value);
            CommitChildSelection();
        }
    }
'@ @'
    private void HoverChild(int sector,Guid? id)
    {
        if(disposed || !arc.IsOpen)return;
        bool enteredNewSector=hoveredChild!=sector;
        if(enteredNewSector)newHoverTimer.Stop();
        hoveredChild=sector;
        keyboardIndex=sector==arcHitZones.Count-1?ActiveArcTotalCount():startIndex+sector;
        for(int i=0;i<childWedges.Count;i++)
        {
            var w=childWedges[i];
            bool selected=w.Tag is int tag && tag==sector;
            bool isNew=i==childWedges.Count-1;
            w.Fill=selected?(isNew?
                new SolidColorBrush(Color.FromRgb(73,58,153)):ChildHover):
                (isNew?Accent:ChildIdle);
        }
        if(editorActive)return;
        if(all.Count==0 && arcApps.Count>0 && sector>=0 && sector<arcHitZones.Count-1)
        {
            int appIdx=startIndex+sector;
            if(appIdx>=0 && appIdx<arcApps.Count)
            {
                transientPreview=true;
                OpenAppFileInfoShelf(arcApps[appIdx].Path,keepTransient:true);
                return;
            }
        }
        if(sector<arcHitZones.Count-1 && id.HasValue)
        {
            // Immediately preview the selected wedge, but do not collapse the
            // donut: the shelf is temporary until a deliberate selection.
            transientPreview=true;
            OpenCollection(id.Value);
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
        if(disposed || !arc.IsOpen)return;
        // A click, key activation or completed drag commits the child,
        // shrinking BOTH parent rings while keeping its shelf/editor.
        if(sector==arcHitZones.Count-1)
        {
            newHoverTimer.Stop();
            CloseTransientPreview();
            if(editorActive)return;
            OpenAppFileInfoShelf();
            CommitChildSelection();
            return;
        }
        if(all.Count==0 && arcApps.Count>0 && sector>=0 && sector<arcHitZones.Count-1 && !editorActive)
        {
            int appIdx=startIndex+sector;
            if(appIdx>=0 && appIdx<arcApps.Count)
            {
                transientPreview=false;
                OpenAppFileInfoShelf(arcApps[appIdx].Path,keepTransient:false);
                CommitChildSelection();
                return;
            }
        }
        if(id.HasValue && !editorActive)
        {
            transientPreview=false; // an explicit selection persists
            OpenCollection(id.Value);
            CommitChildSelection();
        }
    }
'@ 'DeskSpaceInformationV36 HoverChild and ActivateChild app support'
Swap-R380 @'
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
        int amount=standardWheel.Step(e.Delta);
        if(amount!=0)Slide(amount);
        e.Handled=true;
    }
    private bool CanPage(int direction) =>
        direction<0?startIndex>0:startIndex+PageSize<all.Count;
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
        startIndex=Math.Clamp(startIndex+delta,0,Math.Max(0,all.Count-PageSize));
        RenderArc();
        DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,-90);
        if(!CanPage(delta))StopEdgeScroll();
    }
'@ @'
        if(e.Key==Key.Left || e.Key==Key.Right ||
           e.Key==Key.Up || e.Key==Key.Down)
        {
            int total=ActiveArcTotalCount();
            if(total==0){HoverChild(0,null);e.Handled=true;return;}
            int delta=e.Key==Key.Left || e.Key==Key.Up?-1:1;
            keyboardIndex=Math.Clamp(keyboardIndex+delta,0,total);
            if(keyboardIndex>=total)
            {
                HoverChild(arcHitZones.Count-1,null);
            }
            else
            {
                startIndex=Math.Clamp(keyboardIndex-2,0,Math.Max(0,total-4));
                RenderArc();
                Guid? itemId=all.Count>0?all[keyboardIndex].Id:null;
                HoverChild(keyboardIndex-startIndex,itemId);
            }
            e.Handled=true;return;
        }
        if(e.Key==Key.Enter || e.Key==Key.Space)
        {
            int total=ActiveArcTotalCount();
            if(keyboardIndex==total)ActivateChild(arcHitZones.Count-1,null);
            else if(keyboardIndex>=0 && keyboardIndex<total)
            {
                Guid? itemId=all.Count>0?all[keyboardIndex].Id:null;
                ActivateChild(keyboardIndex-startIndex,itemId);
            }
            e.Handled=true;
        }
    }
    private void ScrollArc(object sender,MouseWheelEventArgs e)
    {
        int amount=standardWheel.Step(e.Delta);
        if(amount!=0)Slide(amount);
        e.Handled=true;
    }
    private bool CanPage(int direction) =>
        direction<0?startIndex>0:startIndex+PageSize<ActiveArcTotalCount();
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
        startIndex=Math.Clamp(startIndex+delta,0,Math.Max(0,ActiveArcTotalCount()-PageSize));
        RenderArc();
        DeskSpaceSubDonutStandardV62.Animate(arcCanvas,delta,-90);
        if(!CanPage(delta))StopEdgeScroll();
    }
'@ 'DeskSpaceInformationV36 keyboard and paging app support'
Swap-R380 @'
        // Placeholder only: future opt-in website/app association for UI/PW.
        // Never read URLs, browser windows or passwords automatically yet.
        shelfBody.Children.Add(new TextBlock{
            Text="Future: assign a website/app to a UI or PW entry; "+
                 "when that page is open, suggest the matching entry. "+
                 "Automatic detection and autofill are not enabled.",
            FontSize=10,Foreground=Brushes.Gray,
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(3,8,3,4),
            ToolTip="Placeholder only — existing security vault remains unchanged"
        });
'@ @'
        var appInfoEntryBtn=SmallButton("Select App Files (Header → Value)…",()=>{
            editorActive=false;
            LeaveEditorKeyboardMode();
            OpenAppFileInfoShelf();
        });
        appInfoEntryBtn.ToolTip="Check an application's supported files for ≥ 2 matching inside-file Header → Value fields";
        appInfoEntryBtn.Margin=new Thickness(0,4,0,4);
        shelfBody.Children.Add(appInfoEntryBtn);
'@ 'DeskSpaceInformationV36 OpenEditor App Info button'
Swap-R380 @'
        int direction=point.Y>=0 && point.Y<=222 ?
            point.X<=82 && CanPage(-1)?-1:
            point.X>=406 && CanPage(1)?1:0:0;
'@ @'
        double cw=arcCanvas.Width,ch=arcCanvas.Height;
        double edgeBand=Math.Min(48,cw*0.14);
        int direction=point.Y>=0 && point.Y<=ch ?
            point.X<=edgeBand && CanPage(-1)?-1:
            point.X>=cw-edgeBand && CanPage(1)?1:0:0;
'@ 'DeskSpaceInformationV36 dynamic edge corridors'
Swap-R380 @'
    private void PositionArc()
    {
        if(disposed)return;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        arcCanvas.LayoutTransform=new ScaleTransform(scale,scale);
        Point point=DeskSpaceDonutGeometryV43.PopupOffset(owner,
            ArcCenterX,ArcCenterY,-5,-6);
        arc.HorizontalOffset=point.X;
        arc.VerticalOffset=point.Y;
    }
'@ @'
    private void PositionArc()
    {
        if(disposed || arcBounds.IsEmpty)return;
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        arcCanvas.LayoutTransform=new ScaleTransform(scale,scale);
        Point point=DeskSpaceSubArcGeometryV48.PopupOffset(owner,arcBounds,ArcMargin);
        arc.HorizontalOffset=point.X+(++positionTick%2)*0.001;
        arc.VerticalOffset=point.Y;
    }
'@ 'DeskSpaceInformationV36 concentric PositionArc'
Swap-R380 @'
        // Constrain the shelf to the working area without repositioning the
        // circle or moving the information panel into a remote screen region.
        var screen=SystemParameters.WorkArea;
        double left=owner.Left+x;
        if(left<screen.Left+8)x+=screen.Left+8-left;
        if(owner.Left+x+panelWidth>screen.Right-8)
            x-=(owner.Left+x+panelWidth)-(screen.Right-8);
        double y=height+5; // five-DIP gap under the currently visible circle
'@ @'
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
'@ 'DeskSpaceInformationV36 monitor-aware shelf positioning'
Swap-R380 @'
        var add=SmallButton("+",()=>{if(!editorActive)NewCollection();});
        add.ToolTip="New information collection";
        add.FontSize=17;
        add.MinWidth=29;
        add.Margin=new Thickness(1,2,1,2);
        DockPanel.SetDock(add,Dock.Right);
        header.Children.Add(add);
'@ @'
        var add=SmallButton("+",()=>{if(!editorActive)NewCollection();});
        add.ToolTip="New information collection";
        add.FontSize=17;
        add.MinWidth=29;
        add.Margin=new Thickness(1,2,1,2);
        DockPanel.SetDock(add,Dock.Right);
        header.Children.Add(add);
        var appsBtn=SmallButton("Apps",()=>OpenAppFileInfoShelf());
        appsBtn.ToolTip="Select an application to inspect inside-file Header → Value information";
        appsBtn.FontSize=11;
        appsBtn.MinWidth=40;
        appsBtn.Margin=new Thickness(1,3,1,3);
        DockPanel.SetDock(appsBtn,Dock.Right);
        header.Children.Add(appsBtn);
'@ 'DeskSpaceInformationV36 Apps shelf header button'
Swap-R380 @'
    private void MaskRevealed()
'@ @'
    private string activeShelfAppPath="";
    private string activeShelfFilePath="";
    private int shelfAppScanGen;
    internal void OpenAppFileInfoShelf(string? appPath=null,bool keepTransient=false)
    {
        if(disposed)return;
        editorActive=false;
        shownCollectionId=null;
        LeaveEditorKeyboardMode();
        if(!string.IsNullOrWhiteSpace(appPath))
        {
            if(!string.Equals(activeShelfAppPath,appPath,StringComparison.OrdinalIgnoreCase))
                activeShelfFilePath="";
            activeShelfAppPath=appPath;
        }

        var apps=DeskSpaceInformationTabV380.GetAvailableAppsForInformation();
        if(!string.IsNullOrWhiteSpace(activeShelfAppPath) &&
           apps.All(a=>!string.Equals(a.Path,activeShelfAppPath,StringComparison.OrdinalIgnoreCase)))
        {
            var customElig=DeskSpaceInformationTabV380.EvaluateAppEligibility(activeShelfAppPath);
            apps.Insert(0,(customElig.AppDisplayName,activeShelfAppPath));
        }
        if(string.IsNullOrWhiteSpace(activeShelfAppPath) && apps.Count>0)
        {
            var pref=apps.FirstOrDefault(a=>a.Label.Contains("Winman",StringComparison.OrdinalIgnoreCase));
            activeShelfAppPath=!string.IsNullOrEmpty(pref.Path)?pref.Path:apps[0].Path;
        }

        transientPreview=keepTransient;
        Shelf("App Info (Header → Value)");
        shelfBorder.Width=420;
        shelfBorder.MaxHeight=460;
        shelfScroll.MaxHeight=385;
        UpdateShelfPosition();

        var topRow=new WrapPanel{Margin=new Thickness(0,2,0,4)};
        foreach(var a in apps)
        {
            string capPath=a.Path;
            bool isSel=string.Equals(capPath,activeShelfAppPath,StringComparison.OrdinalIgnoreCase);
            var btn=SmallButton(a.Label,()=>OpenAppFileInfoShelf(capPath));
            if(isSel){btn.Background=Accent;btn.Foreground=Brushes.White;}
            topRow.Children.Add(btn);
        }
        var browseBtn=SmallButton("+ Browse…",()=>{
            var dlg=new Microsoft.Win32.OpenFileDialog{
                Title="Select an application to inspect inside-file Header → Value details",
                Filter="Applications & Shortcuts|*.exe;*.lnk|All Files|*.*"};
            if(dlg.ShowDialog(owner)==true)
                OpenAppFileInfoShelf(dlg.FileName);
        });
        browseBtn.ToolTip="Pick any installed .exe or .lnk shortcut";
        topRow.Children.Add(browseBtn);
        var manualBtn=SmallButton("+ Manual UI/PW",()=>NewCollection());
        manualBtn.ToolTip="Create a manual UI / PW Information collection";
        topRow.Children.Add(manualBtn);
        var fullBtn=SmallButton("Full ↗",()=>DeskSpaceInformationTabV380.SelectOrOpen(owner));
        fullBtn.ToolTip="Open full Application File Information view";
        topRow.Children.Add(fullBtn);
        shelfBody.Children.Add(topRow);

        if(string.IsNullOrWhiteSpace(activeShelfAppPath))
        {
            shelfBody.Children.Add(new TextBlock{
                Text="Pin an application or click + Browse… to select an application.",
                FontSize=11.5,Foreground=Ink,TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(2,4,2,4)});
            return;
        }

        var elig=DeskSpaceInformationTabV380.EvaluateAppEligibility(activeShelfAppPath);
        if(!elig.IsSupported)
        {
            shelfBody.Children.Add(new TextBlock{
                Text="✕ "+elig.Reason,
                FontSize=11.5,FontWeight=FontWeights.SemiBold,
                Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
                TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(2,4,2,6)});
            return;
        }

        var loadingTxt=new TextBlock{
            Text="Scanning "+elig.AppDisplayName+" files for ≥ 2 matching Header → Value fields…",
            FontSize=11.5,Foreground=Ink,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(2,4,2,4)};
        shelfBody.Children.Add(loadingTxt);

        string targetApp=activeShelfAppPath;
        int myGen=++shelfAppScanGen;
        _=System.Threading.Tasks.Task.Run(()=>{
            var res=DeskSpaceInformationTabV380.DiscoverAppInformation(targetApp);
            owner.Dispatcher.BeginInvoke(new Action(()=>{
                if(disposed || !shelf.IsOpen || myGen!=shelfAppScanGen)return;
                RenderAppFileInfoShelfResults(targetApp,res);
            }));
        });
    }
    private void RenderAppFileInfoShelfResults(string appPath,AppInfoDiscoveryResultV380 res)
    {
        while(shelfBody.Children.Count>1)
            shelfBody.Children.RemoveAt(1);

        if(!res.Eligibility.IsSupported || !res.MeetsMinimumTwoFilesRule)
        {
            shelfBody.Children.Add(new TextBlock{
                Text=res.StatusMessage,
                FontSize=11.5,
                Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
                TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(2,4,2,6)});
            return;
        }

        if(!string.IsNullOrEmpty(activeShelfFilePath) &&
           res.MatchedFiles.All(f=>!string.Equals(f.FullPath,activeShelfFilePath,StringComparison.OrdinalIgnoreCase)))
        {
            activeShelfFilePath="";
        }

        // 1. File Selector Strip
        var fileBar=new WrapPanel{Margin=new Thickness(0,2,0,4)};
        fileBar.Children.Add(new TextBlock{
            Text="Select File:",FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(2,0,4,0)});
        var fileBtnEntries=new List<(string Path,Button Btn)>();
        var filesHost=new StackPanel();
        var visibleSet=new HashSet<string>(res.VisibleHeaders,StringComparer.OrdinalIgnoreCase);
        var matchedHeaderNames=new HashSet<string>(res.MatchedHeaders.Select(h=>h.Header),StringComparer.OrdinalIgnoreCase);

        void UpdateFileBtnHighlights()
        {
            foreach(var (fPath,btn) in fileBtnEntries)
            {
                bool isSel=string.Equals(fPath,activeShelfFilePath,StringComparison.OrdinalIgnoreCase);
                btn.Background=isSel?Accent:Pale;
                btn.Foreground=isSel?Brushes.White:Ink;
            }
        }

        void RebuildFilesList()
        {
            filesHost.Children.Clear();
            var orderedVis=res.MatchedHeaders.Select(h=>h.Header).Where(visibleSet.Contains).ToList();
            if(orderedVis.Count==0)
            {
                filesHost.Children.Add(new TextBlock{
                    Text="Check at least one header above to display inside-file information.",
                    FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(2,2,2,4)});
                return;
            }
            var targetFiles=string.IsNullOrEmpty(activeShelfFilePath)
                ? res.MatchedFiles
                : res.MatchedFiles.Where(f=>string.Equals(f.FullPath,activeShelfFilePath,StringComparison.OrdinalIgnoreCase)).ToList();

            foreach(var file in targetFiles)
            {
                var pairs=new List<KeyValuePair<string,string>>(DeskSpaceInformationTabV380.GetVisibleFieldsForFile(file,orderedVis));
                var seenKeys=new HashSet<string>(pairs.Select(kv=>kv.Key),StringComparer.OrdinalIgnoreCase);
                foreach(var kv in file.OrderedFields)
                {
                    if(!matchedHeaderNames.Contains(kv.Key) && seenKeys.Add(kv.Key))
                        pairs.Add(kv);
                }
                if(pairs.Count==0)continue;

                var cardStack=new StackPanel{Margin=new Thickness(5,4,5,4)};
                var headDock=new DockPanel{LastChildFill=true,Margin=new Thickness(0,0,0,3)};
                var capturedFile=file;
                var capturedPairs=pairs;
                var cardBtns=new StackPanel{Orientation=Orientation.Horizontal};

                var copyAllBtn=SmallButton("Copy",()=>{
                    Clipboard.SetText(string.Join(Environment.NewLine,capturedPairs.Select(kv=>$"{kv.Key}: {kv.Value}")));
                    warning.Text=$"Copied {capturedPairs.Count} fields from '{capturedFile.DisplayName}'.";
                });
                copyAllBtn.FontSize=10;copyAllBtn.MinHeight=20;copyAllBtn.Padding=new Thickness(5,1,5,1);
                copyAllBtn.ToolTip="Copy all visible inside-file fields";

                var saveBtn=SmallButton("+ Save",()=>{
                    try
                    {
                        var data=Read();
                        var col=new Collection{
                            Id=Guid.NewGuid(),
                            Name=capturedFile.DisplayName,
                            UpdatedUtc=DateTime.UtcNow,
                            Rows=capturedPairs.Select(kv=>new Item{Label=kv.Key,Value=kv.Value}).ToList()
                        };
                        int idx=data.FindIndex(x=>string.Equals(x.Name,col.Name,StringComparison.OrdinalIgnoreCase));
                        if(idx>=0){col.Id=data[idx].Id;data[idx]=col;}
                        else data.Add(col);
                        Write(data);
                        all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
                        RenderArc();changed();
                        warning.Text="Saved '"+col.Name+"' to Donut Information.";
                    }
                    catch(Exception ex){warning.Text=ex.Message;}
                });
                saveBtn.FontSize=10;saveBtn.MinHeight=20;saveBtn.Padding=new Thickness(5,1,5,1);

                var openBtn=SmallButton("↗",()=>DeskSpacePinnedAppFilesV380.TryOpenQuickArcItem(appPath,capturedFile.FullPath,owner));
                openBtn.FontSize=10;openBtn.MinHeight=20;openBtn.Padding=new Thickness(5,1,5,1);
                openBtn.ToolTip="Open "+capturedFile.FullPath;

                cardBtns.Children.Add(copyAllBtn);
                cardBtns.Children.Add(saveBtn);
                cardBtns.Children.Add(openBtn);
                DockPanel.SetDock(cardBtns,Dock.Right);
                headDock.Children.Add(cardBtns);
                headDock.Children.Add(new TextBlock{
                    Text=$"{file.DisplayName} ({pairs.Count})",FontSize=12,FontWeight=FontWeights.Bold,
                    Foreground=Ink,VerticalAlignment=VerticalAlignment.Center,
                    TextTrimming=TextTrimming.CharacterEllipsis});
                cardStack.Children.Add(headDock);

                foreach(var kv in pairs)
                {
                    var line=new Grid{Margin=new Thickness(0,1.5,0,1.5)};
                    line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(132)});
                    line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
                    line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    line.Children.Add(new TextBlock{
                        Text=kv.Key,FontSize=11,FontWeight=FontWeights.SemiBold,
                        Foreground=Ink,VerticalAlignment=VerticalAlignment.Top,
                        TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,1,4,0)});
                    var valTb=new TextBlock{
                        Text=kv.Value,FontSize=11,Foreground=Ink,
                        VerticalAlignment=VerticalAlignment.Top,
                        TextWrapping=TextWrapping.Wrap,
                        Margin=new Thickness(2,1,4,0),ToolTip=kv.Value};
                    Grid.SetColumn(valTb,1);line.Children.Add(valTb);
                    string copyVal=kv.Value;
                    var cpBtn=SmallButton("⧉",()=>Clipboard.SetText(copyVal));
                    cpBtn.FontSize=10;cpBtn.MinHeight=20;cpBtn.Padding=new Thickness(5,1,5,1);
                    cpBtn.VerticalAlignment=VerticalAlignment.Top;
                    cpBtn.ToolTip="Copy "+kv.Key;
                    Grid.SetColumn(cpBtn,2);line.Children.Add(cpBtn);
                    cardStack.Children.Add(line);
                }
                filesHost.Children.Add(new Border{
                    BorderBrush=new SolidColorBrush(Color.FromRgb(215,221,240)),
                    BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),
                    Margin=new Thickness(0,0,0,6),Child=cardStack});
            }
        }

        var allFilesBtn=SmallButton($"All ({res.MatchedFiles.Count})",()=>{
            activeShelfFilePath="";
            UpdateFileBtnHighlights();
            RebuildFilesList();
        });
        allFilesBtn.FontSize=10.5;allFilesBtn.MinHeight=22;allFilesBtn.Padding=new Thickness(6,2,6,2);
        fileBtnEntries.Add(("",allFilesBtn));
        fileBar.Children.Add(allFilesBtn);

        foreach(var mf in res.MatchedFiles)
        {
            string capFile=mf.FullPath;
            var fBtn=SmallButton(mf.DisplayName,()=>{
                activeShelfFilePath=capFile;
                UpdateFileBtnHighlights();
                RebuildFilesList();
            });
            fBtn.FontSize=10.5;fBtn.MinHeight=22;fBtn.Padding=new Thickness(6,2,6,2);
            fBtn.ToolTip=mf.FullPath;
            fileBtnEntries.Add((capFile,fBtn));
            fileBar.Children.Add(fBtn);
        }
        UpdateFileBtnHighlights();
        shelfBody.Children.Add(fileBar);

        // 2. Visible Headers + Quick Selection Controls
        bool suppressHeaderEvents=false;
        var headerCheckBoxes=new List<(string Header,CheckBox Box)>();
        void ApplyBatchHeaders(IEnumerable<string> nextSet)
        {
            suppressHeaderEvents=true;
            try
            {
                visibleSet.Clear();
                foreach(string h in nextSet)visibleSet.Add(h);
                foreach(var (hName,cb) in headerCheckBoxes)
                    cb.IsChecked=visibleSet.Contains(hName);
                DeskSpaceInformationTabV380.SaveVisibleHeadersPreference(appPath,visibleSet);
                RebuildFilesList();
            }
            finally{suppressHeaderEvents=false;}
        }

        var hdrBar=new DockPanel{LastChildFill=true,Margin=new Thickness(0,2,0,3)};
        var hdrQuick=new StackPanel{Orientation=Orientation.Horizontal};
        var hAllBtn=SmallButton("All",()=>ApplyBatchHeaders(res.MatchedHeaders.Select(h=>h.Header)));
        hAllBtn.FontSize=10;hAllBtn.MinHeight=20;hAllBtn.Padding=new Thickness(5,1,5,1);
        var hSumBtn=SmallButton("Summary",()=>ApplyBatchHeaders(DeskSpaceInformationTabV380.GetSummaryPriorityHeaders(res.MatchedHeaders)));
        hSumBtn.FontSize=10;hSumBtn.MinHeight=20;hSumBtn.Padding=new Thickness(5,1,5,1);
        var hClrBtn=SmallButton("Clear",()=>ApplyBatchHeaders(Array.Empty<string>()));
        hClrBtn.FontSize=10;hClrBtn.MinHeight=20;hClrBtn.Padding=new Thickness(5,1,5,1);
        hdrQuick.Children.Add(hAllBtn);
        hdrQuick.Children.Add(hSumBtn);
        hdrQuick.Children.Add(hClrBtn);
        DockPanel.SetDock(hdrQuick,Dock.Right);
        hdrBar.Children.Add(hdrQuick);
        hdrBar.Children.Add(new TextBlock{
            Text=$"Visible Headers ({res.MatchedHeaders.Count} matched):",
            FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Ink,
            VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,0,0)});
        shelfBody.Children.Add(hdrBar);

        var checksWrap=new WrapPanel{Margin=new Thickness(0,0,0,6)};
        foreach(var hdr in res.MatchedHeaders)
        {
            string hName=hdr.Header;
            var cb=new CheckBox{
                Content=$"{hName} ({hdr.MatchingFileCount})",
                IsChecked=visibleSet.Contains(hName),
                FontSize=11,Foreground=Ink,
                Margin=new Thickness(2,1,6,2),
                ToolTip=$"Sample: {hdr.SampleValue}"};
            cb.Checked+=(_,_)=>{
                if(suppressHeaderEvents)return;
                visibleSet.Add(hName);
                DeskSpaceInformationTabV380.SaveVisibleHeadersPreference(appPath,visibleSet);
                RebuildFilesList();
            };
            cb.Unchecked+=(_,_)=>{
                if(suppressHeaderEvents)return;
                visibleSet.Remove(hName);
                DeskSpaceInformationTabV380.SaveVisibleHeadersPreference(appPath,visibleSet);
                RebuildFilesList();
            };
            headerCheckBoxes.Add((hName,cb));
            checksWrap.Children.Add(cb);
        }

        shelfBody.Children.Add(checksWrap);
        shelfBody.Children.Add(filesHost);
        RebuildFilesList();
    }
    private void MaskRevealed()
'@ 'DeskSpaceInformationV36 App Info Header->Value shelf'
Finish-Patch $infoPath

# 5. Surgical hook: DeskSpaceApplicationsV55.cs (CI R232 baseline does NOT include r262)
$appsPath = "$dstRoot/DeskSpaceApplicationsV55.cs"
Begin-Patch $appsPath
Swap-R380 @'
    private const double BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=310;
    private Rect bounds=Rect.Empty;
    private int childPage,hovered=-1,placementTick;
    private readonly DeskSpaceSubDonutStandardV62.Wheel scrollWheel=new();
    private DeskSpaceSubDonutStandardV62.View currentChildView;
    private string group="";
    private bool disposed;
'@ @'
    private const double BaseInner=202,BaseOuter=255,SubInner=257,SubOuter=323,FileInner=325,FileOuter=465;
    private const int FilePageSize=DeskSpaceSubDonutStandardV62.Visible-1;
    private Rect bounds=Rect.Empty;
    private int childPage,filePage,hovered=-1,placementTick;
    private readonly DeskSpaceSubDonutStandardV62.Wheel scrollWheel=new();
    private readonly DeskSpaceMiddleScrollV63.Controller middleScroll;
    private DeskSpaceSubDonutStandardV62.View currentChildView;
    private string group="";
    private string activeAppPath="";
    private double activeAppCenter;
    private IReadOnlyList<PinnedAppFileItemV380> activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
    private bool activeAppLoading;
    private int activeAppVersion;
    private readonly DispatcherTimer loadingGuardTimer;
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
'@ 'DeskSpaceApplicationsV55 geometry, inscribed bounds and state fields'
Swap-R380 @'
        canvas=new Canvas{Width=1,Height=1,Background=null};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
        canvas.MouseWheel+=Wheel;
        arc=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas};
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
'@ @'
        canvas=new Canvas{Width=1,Height=1,Background=null,Focusable=true};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
        canvas.PreviewMouseRightButtonDown+=OpenFilesRightClickV380;
        canvas.MouseWheel+=Wheel;
        arc=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas};
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(
            canvas,
            ()=>group=="pinned"&&(HasFileOverflowV380()||HasChildOverflowV380()),
            ()=>!disposed&&arc.IsOpen&&group=="pinned",
            ScrollActiveLayerV380);
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
        loadingGuardTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(3000)};
        loadingGuardTimer.Tick+=(_,_)=>{
            loadingGuardTimer.Stop();
            if(disposed || !arc.IsOpen || !activeAppLoading)return;
            activeAppLoading=false;
            Render();
        };
'@ 'DeskSpaceApplicationsV55 right-click full files browser and middle scroll'
Swap-R380 @'
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
'@ @'
    private List<(string Label,string Kind,string Path)> Children()
    {
        // The installed-app list belongs in Search. The Applications wheel
        // shows PINNED shortcuts and provides + Pin / Search actions only.
        return pinned().Where(IsLaunchable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p=>{
                string title=DeskSpacePinnedAppFilesV380.ResolvePinnedAppDisplayName(p);
                if(string.IsNullOrWhiteSpace(title))
                    title=System.IO.Path.GetFileNameWithoutExtension(p);
                return (Label:title,Kind:"launch",Path:p);
            })
            .OrderBy(x=>x.Label,StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
'@ 'DeskSpaceApplicationsV55 friendly pinned app display names'
Swap-R380 @'
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
'@ @'
        currentChildView=default;
        bool d2LeftHalf=false;
        int visibleFileCount=0;
        var fileLookup=new Dictionary<string,PinnedAppFileItemV380>(StringComparer.OrdinalIgnoreCase);
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
                int maxFileStart=Math.Max(0,activeAppFiles.Count-FilePageSize);
                filePage=Math.Clamp(filePage,0,maxFileStart);
                var visibleFiles=activeAppFiles.Skip(filePage).Take(FilePageSize).ToList();
                visibleFileCount=visibleFiles.Count;

                var d2Items=new List<(string Label,string Kind,string Path)>();
                foreach(var f in visibleFiles)
                {
                    if(!string.IsNullOrEmpty(f.FullPath))
                        fileLookup[f.FullPath]=f;
                    d2Items.Add((FormatArcLabelV380(f.DisplayName),"launchfile",f.FullPath));
                }
                if(d2Items.Count==0 && activeAppLoading)
                    d2Items.Add(("Loading…","loading",""));
                else if(d2Items.Count==0)
                    d2Items.Add(("No files found","empty",""));
                d2Items.Add(("All files…","allfiles",activeAppPath));
                double d2Half=Math.Clamp(d2Items.Count*5.25,11.0,28.0);
                double d2Preferred=activeAppCenter*0.45+centre*0.55;
                double d2Center=FitDepth2Angle(d2Preferred,d2Half,AppOuter(2));
                double d2Start=d2Center-d2Half;
                double d2Step=(d2Half*2)/d2Items.Count;
                double normAngle=((d2Center%360)+360)%360;
                d2LeftHalf=normAngle>90 && normAngle<270;
                for(int i=0;i<d2Items.Count;i++)
                {
                    var d2=d2LeftHalf?d2Items[d2Items.Count-1-i]:d2Items[i];
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
        var depth1Wedges=new List<System.Windows.Shapes.Path>();
        var depth2FileWedges=new List<System.Windows.Shapes.Path>();
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
                    ? (fileLookup.TryGetValue(item.Path,out var fi)
                        ? CreateCompactTooltipV380(fi.DisplayName,
                            fi.TallyCompanyCode!=null
                                ? $"{fi.KindBadge} · {fi.FullPath}"
                                : $"{System.IO.Path.GetFileName(fi.FullPath)} · {fi.ParentFolder}")
                        : CreateCompactTooltipV380(System.IO.Path.GetFileName(item.Path) is string fn && fn.Length>0?fn:item.Label,
                            System.IO.Path.GetDirectoryName(item.Path)))
                    : item.Kind=="allfiles"
                        ? CreateCompactTooltipV380(
                            activeAppFiles.Count>0
                                ? $"All compatible files ({activeAppFiles.Count})"
                                : "All compatible files",
                            DeskSpacePinnedAppFilesV380.ResolvePinnedAppDisplayName(item.Path))
                        : CreateCompactTooltipV380(item.Label);
            var wedge=new System.Windows.Shapes.Path{
                Data=geometry,Stroke=Brushes.White,StrokeThickness=1.5,
                Fill=isAction?accent:(isSelectedApp?selected:normal),
                Cursor=Cursors.Hand,ToolTip=tip,Tag=i};
            ToolTipService.SetInitialShowDelay(wedge,item.Kind=="launchfile"?650:850);
            ToolTipService.SetBetweenShowDelay(wedge,200);
            Panel.SetZIndex(wedge,1);
            fills.Add(wedge);
            if(item.Depth==1 && item.Kind=="launch")
                depth1Wedges.Add(wedge);
            else if(item.Depth==2 && item.Kind=="launchfile")
                depth2FileWedges.Add(wedge);
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
        {
            if(depth1Wedges.Count>0 &&
               (currentChildView.HiddenLeft>0 || currentChildView.HiddenRight>0))
            {
                DeskSpaceSubDonutStandardV62.DrawHiddenCountersForWedges(
                    canvas,depth1Wedges.ToArray(),
                    currentChildView.HiddenLeft,currentChildView.HiddenRight,
                    ()=>MoveChild(-1),()=>MoveChild(1));
            }
            if(depth2FileWedges.Count>0 && activeAppFiles.Count>FilePageSize)
            {
                int hiddenBefore=filePage;
                int hiddenAfter=Math.Max(0,activeAppFiles.Count-filePage-visibleFileCount);
                if(d2LeftHalf)
                {
                    DeskSpaceSubDonutStandardV62.DrawHiddenCountersForWedges(
                        canvas,depth2FileWedges.ToArray(),
                        hiddenAfter,hiddenBefore,
                        ()=>MoveFilesV380(1),()=>MoveFilesV380(-1));
                }
                else
                {
                    DeskSpaceSubDonutStandardV62.DrawHiddenCountersForWedges(
                        canvas,depth2FileWedges.ToArray(),
                        hiddenBefore,hiddenAfter,
                        ()=>MoveFilesV380(-1),()=>MoveFilesV380(1));
                }
            }
        }
        Position();
    }
'@ 'DeskSpaceApplicationsV55 inscribed two-pass Z-order depth-2 rendering and hidden counters'
Swap-R380 @'
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
'@ @'
    private static bool AreFileListsEquivalentV380(
        IReadOnlyList<PinnedAppFileItemV380> a,IReadOnlyList<PinnedAppFileItemV380> b)
    {
        if(a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++)
        {
            if(!string.Equals(a[i].FullPath,b[i].FullPath,StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(a[i].DisplayName,b[i].DisplayName,StringComparison.Ordinal))
                return false;
        }
        return true;
    }
    private void ClearActiveAppFilesV380()
    {
        loadingGuardTimer.Stop();
        activeAppVersion++;
        activeAppPath="";
        activeAppLoading=false;
        activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
        filePage=0;
    }
    private void PrewarmVisibleAppsV380()
    {
        if(disposed || group!="pinned")return;
        DeskSpacePinnedAppFilesV380.PrewarmQuickArcApps(
            items.Where(x=>x.Depth==1 && x.Kind=="launch" && !string.IsNullOrEmpty(x.Path))
                 .Select(x=>x.Path));
    }
    private void SelectAppFilesV380(string path,double centerAngle)
    {
        if(!IsLaunchable(path))return;
        bool sameApp=string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase);
        if(sameApp)
        {
            bool angleChanged=Math.Abs(activeAppCenter-centerAngle)>=0.5;
            activeAppCenter=centerAngle;
            if(angleChanged)Render();
            return;
        }
        loadingGuardTimer.Stop();
        filePage=0;
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
        if(DeskSpacePinnedAppFilesV380.TryGetPartialQuickArcFiles(path,out var partial))
        {
            activeAppFiles=partial;
            activeAppLoading=false;
        }
        else
        {
            activeAppFiles=Array.Empty<PinnedAppFileItemV380>();
            activeAppLoading=true;
            loadingGuardTimer.Start();
        }
        Render();
        _=DeskSpacePinnedAppFilesV380.GetQuickArcFilesAsync(
            path,250,
            onProgress:partialBatch=>{
                if(disposed)return;
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed || !arc.IsOpen || version!=activeAppVersion ||
                       !string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase))
                        return;
                    activeAppFiles=partialBatch;
                    if(partialBatch.Count>0)
                    {
                        activeAppLoading=false;
                        loadingGuardTimer.Stop();
                    }
                    Render();
                }));
            }).ContinueWith(t=>{
                if(disposed)return;
                var result=t.Status==System.Threading.Tasks.TaskStatus.RanToCompletion
                    ? t.Result
                    : Array.Empty<PinnedAppFileItemV380>();
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed || !arc.IsOpen || version!=activeAppVersion ||
                       !string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase))
                        return;
                    loadingGuardTimer.Stop();
                    bool shouldRender=activeAppLoading || !AreFileListsEquivalentV380(activeAppFiles,result);
                    activeAppFiles=result;
                    activeAppLoading=false;
                    if(shouldRender)Render();
                }));
            });
    }
    private bool HasChildOverflowV380()=>
        currentChildView.HiddenLeft>0 || currentChildView.HiddenRight>0;
    private bool HasFileOverflowV380()=>
        !string.IsNullOrEmpty(activeAppPath) && activeAppFiles.Count>FilePageSize;
    internal void MoveFilesV380(int delta)
    {
        if(group!="pinned" || !HasFileOverflowV380() || delta==0)return;
        int maxStart=Math.Max(0,activeAppFiles.Count-FilePageSize);
        int next=Math.Clamp(filePage+delta,0,maxStart);
        if(next==filePage)return;
        filePage=next;
        Render();
        DeskSpaceSubDonutStandardV62.Animate(canvas,delta,activeAppCenter);
    }
    private void ScrollActiveLayerV380(int delta)
    {
        if(group!="pinned" || delta==0)return;
        int slot=-1;
        if(canvas.IsLoaded)
        {
            try{slot=Slot(DeskSpaceWheelHoverV35.MouseIn(canvas));}
            catch(InvalidOperationException){slot=hovered;}
        }
        else slot=hovered;
        if(slot<0)slot=hovered;
        if(HasFileOverflowV380())
        {
            if(slot>=0 && slot<items.Count && items[slot].Depth==1 && HasChildOverflowV380())
                MoveChild(delta);
            else
                MoveFilesV380(delta);
            return;
        }
        if(HasChildOverflowV380())
            MoveChild(delta);
    }
    private void MoveChild(int delta)
    {
        if(group!="pinned")return;
        int next=DeskSpaceSubDonutStandardV62.Advance(currentChildView,delta);
        if(next==childPage)return;
        childPage=next;
        ClearActiveAppFilesV380();
        Render();
        PrewarmVisibleAppsV380();
        DeskSpaceSubDonutStandardV62.Animate(canvas,delta,
            ApplicationsParentCenter);
    }
'@ 'DeskSpaceApplicationsV55 async quick arc files loader and depth-2 scroll'
Swap-R380 @'
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
'@ @'
    public void Show()
    {
        if(disposed)return;
        group="";childPage=0;ClearActiveAppFilesV380();
        Render();arc.IsOpen=owner.IsVisible;
    }
    public void HideArc(){middleScroll.Stop();arc.IsOpen=false;group="";childPage=0;ClearActiveAppFilesV380();}
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
            group=kind;childPage=0;ClearActiveAppFilesV380();Render();PrewarmVisibleAppsV380();
            return;
        }
        if(kind=="launch")
        {
            double mid=(items[i].A+items[i].B)/2.0;
            SelectAppFilesV380(hitZones[i].Path,mid);
            return;
        }
        if(kind is "launchfile" or "allfiles" or "loading" or "empty")
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
    private void OpenFilesRightClickV380(object sender,MouseButtonEventArgs e)
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
        if(e.OriginalSource is DependencyObject dep)
        {
            while(dep!=null && !ReferenceEquals(dep,canvas))
            {
                if(dep is Button)return;
                dep=dep is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(dep)
                    : LogicalTreeHelper.GetParent(dep);
            }
        }
        if(arc.IsOpen&&Activate(e.GetPosition(canvas)))e.Handled=true;
    }
'@ 'DeskSpaceApplicationsV55 hover expand, right-click handler and button guard'
Swap-R380 @'
            case "pinned":
                group=item.Kind;childPage=0;Render();return true;
            case "launch":
                if(IsLaunchable(item.Path))
                {
                    try{DeskSpacePinnedRecentV122.OpenLatestOrApp(item.Path);}
                    catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                }
                return true;
'@ @'
            case "pinned":
                group=item.Kind;childPage=0;Render();PrewarmVisibleAppsV380();return true;
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
'@ 'DeskSpaceApplicationsV55 direct open + depth-2 launchfile + allfiles'
Swap-R380 @'
    private void Wheel(object sender,MouseWheelEventArgs e)
    {
        if(group!="pinned")return;
        int delta=scrollWheel.Step(e.Delta);
        if(delta!=0)MoveChild(delta);
        e.Handled=true;
    }
'@ @'
    private void Wheel(object sender,MouseWheelEventArgs e)
    {
        if(group!="pinned")return;
        int delta=scrollWheel.Step(e.Delta);
        if(delta!=0)ScrollActiveLayerV380(delta);
        e.Handled=true;
    }
'@ 'DeskSpaceApplicationsV55 wheel depth routing'
Swap-R380 @'
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;HideArc();
        owner.LocationChanged-=Moved;owner.SizeChanged-=Resized;
    }
'@ @'
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;HideArc();middleScroll.Dispose();
        owner.LocationChanged-=Moved;owner.SizeChanged-=Resized;
    }
'@ 'DeskSpaceApplicationsV55 dispose middleScroll'
Finish-Patch $appsPath

# 5b. Surgical hook: DeskSpaceSubDonutStandardV62.cs
$subDonutPath = "$dstRoot/DeskSpaceSubDonutStandardV62.cs"
Begin-Patch $subDonutPath
Swap-R380 @'
    internal static void DrawHiddenCounters(Canvas canvas,View view,
        double verticalCentre,Action<int> move)
    {
        if(view.HiddenLeft==0&&view.HiddenRight==0)return;
        var wedges=canvas.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>p.Data!=null&&!p.Data.Bounds.IsEmpty)
            .ToArray();
        int count=Math.Min(view.Count,wedges.Length);
        if(count==0)return;
        // Break the actual existing divider stroke rather than hiding it
        // beneath a rectangle. Keep the original wedge fill and hit region.
        if(count==1 && view.HiddenLeft>0 && view.HiddenRight>0)
            SplitDividerStroke(canvas,wedges[0],true,true);
        else
        {
            if(view.HiddenLeft>0)
                SplitDividerStroke(canvas,wedges[0],true,false);
            if(view.HiddenRight>0)
                SplitDividerStroke(canvas,wedges[count-1],false,true);
        }
        if(view.HiddenLeft>0)
            AddDividerCount(canvas,wedges[0],view.HiddenLeft,true,
                ()=>move(-1));
        if(view.HiddenRight>0)
            AddDividerCount(canvas,wedges[count-1],view.HiddenRight,false,
                ()=>move(1));
    }
'@ @'
    internal static void DrawHiddenCountersForWedges(Canvas canvas,
        System.Windows.Shapes.Path[] wedges,int hiddenLeading,int hiddenTrailing,
        Action onLeading,Action onTrailing)
    {
        if((hiddenLeading<=0&&hiddenTrailing<=0)||wedges.Length==0)return;
        int count=wedges.Length;
        // Break the actual existing divider stroke rather than hiding it
        // beneath a rectangle. Keep the original wedge fill and hit region.
        if(count==1 && hiddenLeading>0 && hiddenTrailing>0)
            SplitDividerStroke(canvas,wedges[0],true,true);
        else
        {
            if(hiddenLeading>0)
                SplitDividerStroke(canvas,wedges[0],true,false);
            if(hiddenTrailing>0)
                SplitDividerStroke(canvas,wedges[count-1],false,true);
        }
        if(hiddenLeading>0)
            AddDividerCount(canvas,wedges[0],hiddenLeading,true,onLeading);
        if(hiddenTrailing>0)
            AddDividerCount(canvas,wedges[count-1],hiddenTrailing,false,onTrailing);
    }

    internal static void DrawHiddenCounters(Canvas canvas,View view,
        double verticalCentre,Action<int> move)
    {
        if(view.HiddenLeft==0&&view.HiddenRight==0)return;
        var wedges=canvas.Children.OfType<System.Windows.Shapes.Path>()
            .Where(p=>p.Data!=null&&!p.Data.Bounds.IsEmpty)
            .ToArray();
        int count=Math.Min(view.Count,wedges.Length);
        if(count==0)return;
        DrawHiddenCountersForWedges(canvas,wedges.Take(count).ToArray(),
            view.HiddenLeft,view.HiddenRight,()=>move(-1),()=>move(1));
    }
'@ 'DeskSpaceSubDonutStandardV62 DrawHiddenCountersForWedges'
Finish-Patch $subDonutPath

# 6. Surgical hook: DeskSpaceFeatureHubV199.cs
$hubPath = "$dstRoot/DeskSpaceFeatureHubV199.cs"
Begin-Patch $hubPath
Swap-R380 @'
        var explanation=new TextBlock
'@ @'
        Section("Modern Suite");
        DeskSpaceModernHubV380.AddFeatureHubButtons(Add);
        var explanation=new TextBlock
'@ 'DeskSpaceFeatureHubV199 Modern Suite section'
Finish-Patch $hubPath

# 7. Surgical hook: DeskSpaceClipboardV10.cs
$clipPath = "$dstRoot/DeskSpaceClipboardV10.cs"
Begin-Patch $clipPath
Swap-R380 @'
            lastContent=secret;
            Clipboard.SetText(secret);
'@ @'
            lastContent=secret;
            DeskSpaceRichClipboardV380.NotifySensitiveCopy(secret);
            Clipboard.SetText(secret);
'@ 'DeskSpaceClipboardV10 sensitive copy exclusion'
Finish-Patch $clipPath

# 8. Surgical hook: DeskSpaceActionPaletteV197.cs
$palettePath = "$dstRoot/DeskSpaceActionPaletteV197.cs"
Begin-Patch $palettePath
Swap-R380 @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
    };
'@ @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files"),
        new("v380_commandbar","Command Bar · Search, Math, GST & Units","Open Spotlight launcher with calculator, converters & quick actions","Tools"),
        new("v380_information","Information Collections · Saved Info & Vault PW","View, search, copy & edit saved Information collections synced with donut","Workspace"),
        new("v380_pinnedfiles","Pinned Apps · Compatible Files","Browse all compatible files & Tally companies for pinned apps (Recent first)","Files"),
        new("v380_clipboard","Rich Clipboard History & Transforms","Event-driven text, image, color & file clipboard history","Text"),
        new("v380_snip","Screen Snip, Table OCR & Pin on Screen","Capture region to OCR text, Excel TSV or floating pin","Tools"),
        new("v380_eyedropper","Screen Eyedropper Color Picker","Sample #HEX, RGB & HSL color anywhere on screen","Tools"),
        new("v380_focus","Focus Ring Pomodoro Studio","Start deep-work timer rendered on the DeskSpace donut","Workspace"),
        new("v380_vaultpro","Vault Pro · Hello, TOTP 2FA & Backup","Windows Hello unlock, RFC 6238 TOTP & AES-256-GCM backup","Privacy"),
        new("v380_desktouch","DeskTouch Ecosystem Hub & OTP Grabber","Drop files to phone/PC, extract OTPs & scan camera tables","System"),
        new("v380_snippets","Dynamic Snippet Expander","Expand snippets with {{date}}, {{clipboard}} & {{prompt:...}}","Text"),
        new("v380_excelpro","Excel & Accounting Pro Studio","Pre-paste diff, rollback history, GSTIN Luhn-36 & PAN validator","Excel"),
        new("v380_filetoolkit","File Toolkit · QR, EXIF, SHA-256, PDF & ZIP","Offline QR codes, EXIF removal, hash verify, PDF PNG & ZIP","Files"),
        new("v380_windowpower","Window Power · Snap Zones & Auto-Layouts","Monitor topology layouts, Snap Zones & Always-on-Top pin","Windows"),
        new("v380_snapzones","Snap Active Window to Zone","Pick visual layout zone for foreground window","Windows"),
        new("v380_topmost","Toggle Always-on-Top Window Pin","Pin or unpin the active foreground window","Windows"),
        new("v380_routines","Automation Routines & Smart Profiles","Multi-step routines with dry-run plan and per-app actions","Automation"),
        new("v380_localai","Local AI Copilot (Opt-In)","Ollama or custom endpoint assistant (disabled by default)","Tools")
    };
'@ 'DeskSpaceActionPaletteV197 V380 command entries'
Swap-R380 @'
                    catch(Exception ex) when(ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception or System.Security.SecurityException)
                    {
                        MessageBox.Show(owner,"Unable to open Downloads: "+ex.GetType().Name,
                            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }
                break;
        }
'@ @'
                    catch(Exception ex) when(ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception or System.Security.SecurityException)
                    {
                        MessageBox.Show(owner,"Unable to open Downloads: "+ex.GetType().Name,
                            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
                    }
                }
                break;
            default:
                DeskSpaceModernHubV380.TryExecutePaletteCommand(owner, command.Id);
                break;
        }
'@ 'DeskSpaceActionPaletteV197 V380 command dispatch'
Finish-Patch $palettePath

# 8b. Surgical hook: DeskSpaceSearchV11.cs (fix R337 Unloaded/Dispose CancellationTokenSource ObjectDisposedException crash)
$searchPath = "$dstRoot/DeskSpaceSearchV11.cs"
Begin-Patch $searchPath
Swap-R380 @'
        frame.Unloaded+=(_,_)=>{
            slowNoticeV337.Stop();
            indexRefresh?.Stop();
            pending?.Cancel();
        };
        return frame;
    }
    public void Dispose()
    {
        disposed=true;appRefresh?.Stop();
        debounce?.Stop();indexRefresh?.Stop();pending?.Cancel();pending?.Dispose();
    }
'@ @'
        frame.Unloaded+=(_,_)=>{
            slowNoticeV337.Stop();
            appRefresh?.Stop();
            indexRefresh?.Stop();
            SafeCancelPendingV380(dispose:true);
        };
        return frame;
    }
    private void SafeCancelPendingV380(bool dispose)
    {
        var cts=dispose?Interlocked.Exchange(ref pending,null):Volatile.Read(ref pending);
        if(cts==null)return;
        try{cts.Cancel();}catch(ObjectDisposedException){}
        if(dispose)
        {
            try{cts.Dispose();}catch(ObjectDisposedException){}
        }
    }
    public void Dispose()
    {
        disposed=true;appRefresh?.Stop();
        debounce?.Stop();indexRefresh?.Stop();
        SafeCancelPendingV380(dispose:true);
    }
'@ 'DeskSpaceSearchV11 Unloaded and Dispose CTS safety'
Swap-R380 'pending?.Cancel();pending?.Dispose();' 'SafeCancelPendingV380(dispose:true);' 'DeskSpaceSearchV11 debounce Tick CTS safety'
$cancelCount = ([regex]::Matches($script:code, [regex]::Escape('pending?.Cancel();'))).Count
if ($cancelCount -ne 3) {
    throw "R380 DeskSpaceSearchV11: expected 3 remaining pending?.Cancel() sites, got $cancelCount"
}
$script:code = $script:code.Replace('pending?.Cancel();', 'SafeCancelPendingV380(dispose:false);')
Finish-Patch $searchPath

# 9. Post-patch invariants check
$deckCheck = [IO.File]::ReadAllText("$dstRoot/DeskSpaceDropShelfV15.cs")
if (([regex]::Matches($deckCheck, 'DeckHeaderButton[(]')).Count -ne 4) {
    throw 'R380 invariant broken: DeskSpaceDropShelfV15 DeckHeaderButton( count != 4'
}
$xamlCheck = [IO.File]::ReadAllText("$dstRoot/MainWindow.xaml")
foreach ($req in @('TextElement.Foreground="#243F5C"', 'Foreground="#1B3A57"', 'Value="#BFDFF4"', 'Ready — Workspace loaded')) {
    if (!$xamlCheck.Contains($req)) {
        throw "R380 invariant broken: MainWindow.xaml missing $req"
    }
}
if (!(Test-Path -LiteralPath "$dstRoot/DeskSpaceDesignV283.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpacePinnedAppFilesV380.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpaceInformationTabV380.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpaceModernHubV380.cs")) {
    throw 'R380 missing copied ModernV380 or DeskSpaceDesignV283 files in target tree'
}

Write-Host "R380 PASS: Copied DeskSpaceDesignV283 + $($modernFiles.Count) ModernV380 modules and wired all hooks safely."
