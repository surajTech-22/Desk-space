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
    public void Save()
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp = Filename + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, Filename, true);
    }
'@ @'
    public void Save()
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp = Filename + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, Filename, true);
    }
    public void SavePreservingPinnedApps()
    {
        try
        {
            if (File.Exists(Filename))
            {
                var disk = JsonSerializer.Deserialize<WidgetSettingsV3>(File.ReadAllText(Filename));
                if (disk?.PinnedApps != null)
                {
                    PinnedApps.Clear();
                    PinnedApps.AddRange(disk.PinnedApps);
                }
            }
        }
        catch { }
        Save();
    }
'@ 'WidgetHost SavePreservingPinnedApps'
Swap-R380 @'
    private readonly List<System.Windows.Shapes.Path> ringPaths = new();
'@ @'
    private readonly List<System.Windows.Shapes.Path> ringPaths = new();
    private readonly List<(System.Windows.Shapes.Path Wedge, List<UIElement> LinkedVisuals, TextBlock Label, bool IsOuter, double MidAngle)> ringVisuals = new();
'@ 'WidgetHost ringVisuals field'
Swap-R380 @'
    private DateTime rightClickOpenedUntil = DateTime.MinValue;
'@ @'
    private DateTime rightClickOpenedUntil = DateTime.MinValue;
    private DateTime outsideRingSinceUtc = DateTime.MinValue;
'@ 'WidgetHost outsideRingSinceUtc field'
Swap-R380 @'
            if (ringExpanded && informationOverlay?.IsArcOpen!=true && !ringGesture && !draggingGesture &&
'@ @'
            if (ringExpanded && informationOverlay?.IsArcOpen!=true && appsWheel?.IsArcOpen!=true && excelWheel?.IsArcOpen!=true && !ringGesture && !draggingGesture &&
'@ 'WidgetHost MouseLeave sub-arc protection'
Swap-R380 @'
        Deactivated+=(_,_)=>{
            if(!keyboardDonutModeV207)return;
            EndKeyboardDonutV207();
            if(ringExpanded && activeModule==null)CollapseRing();
        };
'@ @'
        Deactivated+=(_,_)=>{
            if(!keyboardDonutModeV207 ||
               informationOverlay?.IsContextMenuOpen==true ||
               appsWheel?.IsContextMenuOpen==true)return;
            EndKeyboardDonutV207();
            if(ringExpanded && activeModule==null)CollapseRing();
        };
'@ 'WidgetHost Deactivated context menu guard'
Swap-R380 @'
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.Save();prefs.ApplyWindowsStartup();
                }).Show())));
'@ @'
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.SavePreservingPinnedApps();prefs.ApplyWindowsStartup();
                }).Show())));
'@ 'WidgetHost options preserve PinnedApps'
Swap-R380 @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
'@ @'
        DeskSpaceDeskTouchBridgeV378.AddTraySubmenu(menu, Dispatcher);
        DeskSpaceModernHubV380.AttachWidgetHost(this, menu);
'@ 'WidgetHost tray modern submenu and global hotkeys'
Swap-R380 @'
        menu.Items.Add("Donut toolbar, themes & presets",null,(_,_)=>Dispatcher.Invoke(
            ()=>DeskSpaceSettingsStudioV148.Show(this,prefs,
                RefreshAppearanceV148,
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.SavePreservingPinnedApps();prefs.ApplyWindowsStartup();
                }).Show())));
'@ @'
        menu.Items.Add("Donut toolbar, themes & presets",null,(_,_)=>Dispatcher.Invoke(
            ()=>DeskSpaceSettingsStudioV148.Show(this,prefs,
                RefreshAppearanceV148,
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.SavePreservingPinnedApps();prefs.ApplyWindowsStartup();
                }).Show())));
        AddDonutStyleTrayMenuV380(menu);
'@ 'WidgetHost tray Donut Style and Transitions submenu'
Swap-R380 @'
            excelWheel??=new DeskSpaceExcelWheelV42(this,()=>{
                if(ringExpanded)CollapseRing();
            });
            excelWheel.WarmStart();
            informationOverlay??=new DeskSpaceInformationV36(this,()=>{
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
                else UpdateCenter();
            });
            informationOverlay.WarmStart();
'@ @'
            excelWheel??=new DeskSpaceExcelWheelV42(this,()=>{
                if(ringExpanded)CollapseRing();
            });
            excelWheel.WarmStart();
            EnsureInformationOverlay().WarmStart();
'@ 'WidgetHost Loaded EnsureInformationOverlay with onCommitted'
Swap-R380 @'
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsArcOpen==true)return;
'@ @'
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsArcOpen==true || appsWheel?.IsArcOpen==true || excelWheel?.IsArcOpen==true)return;
'@ 'WidgetHost OnGestureMove sub-arc protection'
Swap-R380 @'
        if (selection >= 0)
            Dispatcher.BeginInvoke(new Action(() => ClickSegment(Segments[selection])));
        CollapseRing();
'@ @'
        if (selection >= 0)
            Dispatcher.BeginInvoke(new Action(() => ClickSegment(Segments[selection])));
        if (selection is not (6 or 10 or 11))
            CollapseRing();
'@ 'WidgetHost OnGestureUp keep sub-donut ring open on sub-arc parent click'
Swap-R380 @'
            if(!string.IsNullOrWhiteSpace(stopwatchReturnCenterV371))
            {
                prefs.CenterMode=stopwatchReturnCenterV371!;
                prefs.Save();
            }
'@ @'
            if(!string.IsNullOrWhiteSpace(stopwatchReturnCenterV371))
            {
                prefs.CenterMode=stopwatchReturnCenterV371!;
                prefs.SavePreservingPinnedApps();
            }
'@ 'WidgetHost CompleteModuleReturn preserve PinnedApps'
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
    private void SavePosition()
    {
        if(DeskSpaceMonitorSessionsV154.SavePosition(this,ringExpanded))return;
        prefs.Left = Left; prefs.Top = Top; prefs.Save();
    }

    private void KeepOnVisibleDesktop()
    {
        if(DeskSpaceMonitorFenceV152.ClampIfBound(this))return;
        if(DeskSpaceMultiMonitorV151.IsSatellite(this))return;
        double minX = SystemParameters.VirtualScreenLeft;
        double minY = SystemParameters.VirtualScreenTop;
        double maxX = minX + SystemParameters.VirtualScreenWidth - Width;
        double maxY = minY + SystemParameters.VirtualScreenHeight - Height;
        Left = Math.Clamp(Left, minX, Math.Max(minX, maxX));
        Top = Math.Clamp(Top, minY, Math.Max(minY, maxY));
    }

    private DispatcherTimer? wheelHoverTimer;
    private int mainWheelSector=-1;
    private int mainWheelQuick=-1;
    private DateTime mainWheelEnteredUtc=DateTime.MinValue;

    private void StartWheelHover()
    {
        if(wheelHoverTimer==null)
        {
            wheelHoverTimer=new DispatcherTimer{
                Interval=TimeSpan.FromMilliseconds(
                    DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
            wheelHoverTimer.Tick+=(_,_)=>SampleMainWheel();
        }
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
        wheelHoverTimer.Start();
    }
    private void StopWheelHover()
    {
        wheelHoverTimer?.Stop();
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
    }
    private void SampleMainWheel()
    {
        if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(this))return;
        if(!ringExpanded || activeModule!=null || hiddenAtEdge ||
            draggingGesture || shortcutMoveInProgress)return;
        if(ringGesture || Mouse.LeftButton==MouseButtonState.Pressed ||
            Mouse.RightButton==MouseButtonState.Pressed)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            return; // drag release still uses existing captured input path
        }
        Point p;
        try{p=DeskSpaceWheelHoverV35.MouseIn(this);}
        catch(InvalidOperationException){return;}
        int selected=SectorAt(p);
        int add=QuickAddHitAt(p);
        if(selected<0)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            return;
        }
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
    private void SavePosition()
    {
        if(DeskSpaceMonitorSessionsV154.SavePosition(this,ringExpanded))return;
        prefs.Left = Left; prefs.Top = Top; prefs.SavePreservingPinnedApps();
    }

    private void KeepOnVisibleDesktop()
    {
        if(DeskSpaceMonitorFenceV152.ClampIfBound(this))return;
        if(DeskSpaceMultiMonitorV151.IsSatellite(this))return;
        double minX = SystemParameters.VirtualScreenLeft;
        double minY = SystemParameters.VirtualScreenTop;
        double maxX = minX + SystemParameters.VirtualScreenWidth - Width;
        double maxY = minY + SystemParameters.VirtualScreenHeight - Height;
        Left = Math.Clamp(Left, minX, Math.Max(minX, maxX));
        Top = Math.Clamp(Top, minY, Math.Max(minY, maxY));
    }

    private DispatcherTimer? wheelHoverTimer;
    private int mainWheelSector=-1;
    private int mainWheelQuick=-1;
    private DateTime mainWheelEnteredUtc=DateTime.MinValue;

    private void StartWheelHover()
    {
        if(wheelHoverTimer==null)
        {
            wheelHoverTimer=new DispatcherTimer{
                Interval=TimeSpan.FromMilliseconds(
                    DeskSpaceWheelHoverV35.PointerPollMilliseconds)};
            wheelHoverTimer.Tick+=(_,_)=>SampleMainWheel();
        }
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
        outsideRingSinceUtc=DateTime.MinValue;
        wheelHoverTimer.Start();
    }
    private void StopWheelHover()
    {
        wheelHoverTimer?.Stop();
        mainWheelSector=-1;mainWheelQuick=-1;
        mainWheelEnteredUtc=DateTime.MinValue;
        outsideRingSinceUtc=DateTime.MinValue;
    }
    private void SampleMainWheel()
    {
        if(!ringExpanded || activeModule!=null || hiddenAtEdge ||
            draggingGesture || shortcutMoveInProgress)return;
        if(ringGesture || Mouse.LeftButton==MouseButtonState.Pressed ||
            Mouse.RightButton==MouseButtonState.Pressed)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            outsideRingSinceUtc=DateTime.MinValue;
            return; // drag release still uses existing captured input path
        }
        Point p;
        try{p=DeskSpaceWheelHoverV35.MouseIn(this);}
        catch(InvalidOperationException){return;}
        int selected=DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(this)?SectorAt(p):-1;
        int add=selected>=0?QuickAddHitAt(p):-1;
        if(selected<0)
        {
            mainWheelSector=-1;mainWheelQuick=-1;
            bool overMainDonut=DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(this) &&
                (p-new Point(Width/2,Height/2)).Length<=Width*198.0/420.0+14;
            bool overSubArc=(informationOverlay?.IsPointerNearArc()==true) ||
                (appsWheel?.IsPointerNearArc()==true) ||
                (excelWheel?.IsPointerNearArc()==true);
            if(!overMainDonut && !overSubArc &&
               DateTime.UtcNow>rightClickOpenedUntil && !keyboardDonutModeV207)
            {
                if(outsideRingSinceUtc==DateTime.MinValue)
                    outsideRingSinceUtc=DateTime.UtcNow;
                else if((DateTime.UtcNow-outsideRingSinceUtc).TotalMilliseconds>=260)
                {
                    outsideRingSinceUtc=DateTime.MinValue;
                    CollapseRing();
                }
            }
            else
            {
                outsideRingSinceUtc=DateTime.MinValue;
            }
            return;
        }
        outsideRingSinceUtc=DateTime.MinValue;
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
'@ 'WidgetHost SavePosition and SampleMainWheel outside-ring collapse'
Swap-R380 @'
    private void ToggleRing()
    {
        if(!ringExpanded && activeModule==null) donutHome.Capture(this);
'@ @'
    private void ToggleRing()
    {
        outsideRingSinceUtc=DateTime.MinValue;
        if(!ringExpanded && activeModule==null) donutHome.Capture(this);
'@ 'WidgetHost ToggleRing reset outsideRingSinceUtc'
Swap-R380 @'
                else
                {
                    prefs.CenterMode=mode;prefs.Save();
                    if(activeModule==null&&!ringExpanded)BuildCompact();
                    UpdateCenter();
                }
'@ @'
                else
                {
                    prefs.CenterMode=mode;prefs.SavePreservingPinnedApps();
                    if(activeModule==null&&!ringExpanded)BuildCompact();
                    UpdateCenter();
                }
'@ 'WidgetHost ChooseCenter preserve PinnedApps'
Swap-R380 @'
    private void OpenApplicationsArc()
    {
        if(activeModule!=null)return;
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        appsWheel??=new DeskSpaceApplicationsV55(this,
            ()=>prefs.PinnedApps.ToArray(),
            p=>{
                if(!prefs.PinnedApps.Any(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase)))
                {prefs.PinnedApps.Add(p);prefs.Save();}
            },
            p=>{
                var old=prefs.PinnedApps.FirstOrDefault(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase));
                if(old!=null){prefs.PinnedApps.Remove(old);prefs.Save();}
            },
            ()=>OpenInWidget("Search"));
'@ @'
    internal void SyncPinnedAppsFromDiskV380()
    {
        try
        {
            var latest=WidgetSettingsV3.Load().PinnedApps;
            prefs.PinnedApps.Clear();
            prefs.PinnedApps.AddRange(latest);
            if(appsWheel?.IsArcOpen==true)
            {
                appsWheel.RefreshPinsV380();
                if(activeModule==null && ringExpanded)
                    content.Text="Applications"+Environment.NewLine+appsWheel.PinnedCount+" pinned";
            }
        }
        catch{}
    }

    private void OpenApplicationsArc()
    {
        if(activeModule!=null)return;
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        SyncPinnedAppsFromDiskV380();
        appsWheel??=new DeskSpaceApplicationsV55(this,
            ()=>prefs.PinnedApps.ToArray(),
            p=>{
                SyncPinnedAppsFromDiskV380();
                if(!prefs.PinnedApps.Any(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase)))
                {prefs.PinnedApps.Add(p);prefs.Save();}
            },
            p=>{
                SyncPinnedAppsFromDiskV380();
                var old=prefs.PinnedApps.FirstOrDefault(x=>string.Equals(x,p,
                    StringComparison.OrdinalIgnoreCase));
                if(old!=null){prefs.PinnedApps.Remove(old);prefs.Save();}
                if(appsWheel?.IsArcOpen==true && activeModule==null && ringExpanded)
                    content.Text="Applications"+Environment.NewLine+appsWheel.PinnedCount+" pinned";
            },
            ()=>OpenInWidget("Search"));
'@ 'WidgetHost OpenApplicationsArc sync pinned apps and live count'
Swap-R380 @'
            informationOverlay??=new DeskSpaceInformationV36(this,()=>{
                if(informationOverlay?.IsArcOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
                else UpdateCenter();
            },()=>{
                // A committed child (click, Enter, or drag release)
                // completes the expanded -> compact transition. Only
                // hide child arc; keep the selected Information shelf.
                if(ringExpanded)ToggleRing();
                informationOverlay?.HideArc();
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
            });
            informationOverlay.Show();
            if(informationOverlay.IsOpen)
                content.Text="Information"+Environment.NewLine+
                    informationOverlay.SavedCount+" saved";
'@ @'
            var overlay=EnsureInformationOverlay();
            overlay.Show();
            if(overlay.IsArcOpen)
                content.Text=overlay.CenterSummaryText;
'@ 'WidgetHost OpenInformationArc use EnsureInformationOverlay'
Swap-R380 @'
    private void OpenInformationArc()
'@ @'
    private DeskSpaceInformationV36 EnsureInformationOverlay()
    {
        informationOverlay??=new DeskSpaceInformationV36(this,()=>{
            if(informationOverlay?.IsArcOpen==true)
                content.Text=informationOverlay.CenterSummaryText;
            else UpdateCenter();
        },()=>{
            // A committed child (click, Enter, or drag release)
            // completes the expanded -> compact transition. Only
            // hide child arc; keep the selected Information shelf.
            if(ringExpanded)ToggleRing();
            informationOverlay?.HideArc();
            UpdateCenter();
        });
        return informationOverlay;
    }
    private void OpenInformationArc()
'@ 'WidgetHost EnsureInformationOverlay definition'
Swap-R380 @'
            prefs.CenterMode="Clock";
            prefs.Save();CollapseRing();BuildCompact();UpdateCenter();
'@ @'
            prefs.CenterMode="Clock";
            prefs.SavePreservingPinnedApps();CollapseRing();BuildCompact();UpdateCenter();
'@ 'WidgetHost Clock segment preserve PinnedApps'
Swap-R380 @'
    private void ApplySettings()
    {
        prefs.Save();prefs.ApplyWindowsStartup();UpdateCenter();
        if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
    }
'@ @'
    private void ApplySettings()
    {
        prefs.SavePreservingPinnedApps();prefs.ApplyWindowsStartup();UpdateCenter();
        if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
    }
'@ 'WidgetHost ApplySettings preserve PinnedApps'
Swap-R380 @'
        if(informationOverlay?.IsOpen==true)
        {
            content.Text="Information"+Environment.NewLine+
                informationOverlay.SavedCount+" saved";
            return;
        }
'@ @'
        if(informationOverlay?.IsArcOpen==true)
        {
            content.Text=informationOverlay.CenterSummaryText;
            return;
        }
'@ 'WidgetHost UpdateCenter active Information arc summary'
Swap-R380 @'
        if(selected==highlightedSector)return;
        highlightedSector=selected;
        for(int i=0;i<ringPaths.Count;i++)
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==selected,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
    }
'@ @'
        if(selected==highlightedSector)return;
        highlightedSector=selected;
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int i=0;i<ringVisuals.Count;i++)
        {
            var rv=ringVisuals[i];
            bool hov=i==selected;
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,rv.IsOuter?1:0,i,false,false,hov);
            rv.Wedge.Fill=vis.Fill;
            rv.Wedge.Stroke=vis.Stroke;
            rv.Wedge.StrokeThickness=vis.StrokeThickness;
            rv.Wedge.Effect=vis.Effect;
            rv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
            DeskSpaceDonutThemeV380.ApplyHoverLift(rv.LinkedVisuals,hov,rv.MidAngle,themeApp);
        }
    }
'@ 'WidgetHost UpdateHighlightedSector themed lift and glow'
Swap-R380 @'
        highlightedSector=keyboardDonutSectorV207;
        for(int i=0;i<ringPaths.Count;i++)
        {
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==highlightedSector,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
            ringPaths[i].StrokeThickness=i==highlightedSector?4:2;
        }
'@ @'
        highlightedSector=keyboardDonutSectorV207;
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int i=0;i<ringVisuals.Count;i++)
        {
            var rv=ringVisuals[i];
            bool hov=i==highlightedSector;
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,rv.IsOuter?1:0,i,false,false,hov);
            rv.Wedge.Fill=vis.Fill;
            rv.Wedge.Stroke=vis.Stroke;
            rv.Wedge.StrokeThickness=hov?Math.Max(3.2,vis.StrokeThickness):vis.StrokeThickness;
            rv.Wedge.Effect=vis.Effect;
            rv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
            DeskSpaceDonutThemeV380.ApplyHoverLift(rv.LinkedVisuals,hov,rv.MidAngle,themeApp);
        }
'@ 'WidgetHost keyboard donut highlight themed'
Swap-R380 @'
        if (ringExpanded)
        {
            BuildDonut();
            StartWheelHover();
        }
'@ @'
        if (ringExpanded)
        {
            BuildDonut();
            DeskSpaceDonutThemeV380.AnimateOpen(
                canvas,
                ringVisuals.Select(v=>(FrameworkElement)v.Wedge).ToArray(),
                210,210,-90,isSubArc:false);
            StartWheelHover();
        }
'@ 'WidgetHost ToggleRing open transition'
Swap-R380 @'
        canvas.Children.Clear();
        ringPaths.Clear();
        highlightedSector = -1;
        canvas.Width = canvas.Height = 292;
        var shadow = new Ellipse { Width = 282, Height = 282,
            Fill = new SolidColorBrush(Color.FromArgb(70, 25, 19, 58)) };
        Canvas.SetLeft(shadow, 5); Canvas.SetTop(shadow, 7);
        canvas.Children.Add(shadow);
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness = 7,
'@ @'
        canvas.Children.Clear();
        ringPaths.Clear();
        ringVisuals.Clear();
        highlightedSector = -1;
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var compactHubVis = DeskSpaceDonutThemeV380.ResolveHub(DeskSpaceAppearanceV148.Current, SystemParameters.HighContrast);
        canvas.Width = canvas.Height = 292;
        var shadow = new Ellipse { Width = 282, Height = 282,
            Fill = new SolidColorBrush(Color.FromArgb(85, 9, 16, 35)),
            IsHitTestVisible=false };
        Canvas.SetLeft(shadow, 5); Canvas.SetTop(shadow, 7);
        canvas.Children.Add(shadow);
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = compactHubVis.Stroke,
            StrokeThickness = 7,
            Effect = compactHubVis.Effect,
'@ 'WidgetHost BuildCompact hub style'
Swap-R380 @'
        canvas.Children.Clear();
        ringPaths.Clear();
        highlightedSector=-1;
        canvas.Width=canvas.Height=420;
        var shadow=new Ellipse{Width=408,Height=408,Fill=new SolidColorBrush(Color.FromArgb(36,25,20,60)),IsHitTestVisible=false};
        Canvas.SetLeft(shadow,6);Canvas.SetTop(shadow,7);canvas.Children.Add(shadow);
        for(int i=0;i<Segments.Length;i++)
        {
            bool outer=i>=6;int slot=outer?i-6:i;
            bool hasQuickAdd=HasModuleQuickAdd(Segments[i]);
            double step=360.0/(outer?6:6);double first=-90-step/2+slot*step,last=first+step-1.5;
            var wedge=new System.Windows.Shapes.Path{
                Data=Sector(210,210,outer?132:70,outer?195:124,first,last),
                Fill=DeskSpaceHighContrastV208.Wedge(
                    SystemParameters.HighContrast,i,false,
                    light,DeskSpaceAppearanceV148.SecondaryBrush,accent),
                Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
                StrokeThickness=2,IsHitTestVisible=false
            };
            canvas.Children.Add(wedge);ringPaths.Add(wedge);
            var angle=(-90+slot*(360.0/(outer?6:6)))*Math.PI/180;
            var label=new TextBlock{
                Text=SegmentIcons[i]+"\n"+
                    (Segments[i]=="Clock & Weather"?"Clock\nWeather":Segments[i]),
                FontSize=outer?12:11,
                TextAlignment=TextAlignment.Center,FontWeight=FontWeights.SemiBold,
                Foreground=DeskSpaceHighContrastV208.Label(
                    SystemParameters.HighContrast,DeskSpaceAppearanceV148.InkBrush),
                IsHitTestVisible=false
            };
            label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            double radius=outer?(hasQuickAdd?151:162):(hasQuickAdd?94:100);
            Canvas.SetLeft(label,210+radius*Math.Cos(angle)-label.DesiredSize.Width/2);
            Canvas.SetTop(label,210+radius*Math.Sin(angle)-label.DesiredSize.Height/2);
            canvas.Children.Add(label);
            if(hasQuickAdd)
            {
                // True in-donut hit region, no separate popup HWND or
                // overlapping modal panel. Click and drag release both
                // route through QuickAddHitAt on the parent gesture.
                double markRadius=outer?182:113;
                double px=210+markRadius*Math.Cos(angle);
                double py=210+markRadius*Math.Sin(angle);
                var mark=new Ellipse{
                    Width=20,Height=20,
                    Fill=DeskSpaceHighContrastV208.QuickAction(
                        SystemParameters.HighContrast,accent),
                    Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
                    StrokeThickness=1.4,
                    IsHitTestVisible=false};
                Canvas.SetLeft(mark,px-10);Canvas.SetTop(mark,py-10);
                canvas.Children.Add(mark);
                var plus=new TextBlock{
                    Text="+",FontSize=17,FontWeight=FontWeights.Bold,
                    Foreground=DeskSpaceHighContrastV208.QuickActionLabel(
                        SystemParameters.HighContrast),
                    TextAlignment=TextAlignment.Center,
                    Width=20,Height=20,IsHitTestVisible=false};
                Canvas.SetLeft(plus,px-10);Canvas.SetTop(plus,py-2-10);
                canvas.Children.Add(plus);
            }
        }
        var centre=new Ellipse{Width=130,Height=130,Fill=ClockFace(),
            Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness=3,IsHitTestVisible=false};
        Canvas.SetLeft(centre,145);Canvas.SetTop(centre,145);canvas.Children.Add(centre);
        clockFace=centre;
'@ @'
        canvas.Children.Clear();
        ringPaths.Clear();
        ringVisuals.Clear();
        highlightedSector=-1;
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        double gapDeg=DeskSpaceDonutThemeV380.GetMainGapDegrees(themeApp);
        double halfGapAdjust=(gapDeg-1.5)*0.5;
        canvas.Width=canvas.Height=420;
        var shadow=new Ellipse{Width=408,Height=408,
            Fill=new SolidColorBrush(Color.FromArgb(themeApp.MixWireframe?(byte)22:(byte)65,10,17,40)),
            IsHitTestVisible=false};
        Canvas.SetLeft(shadow,6);Canvas.SetTop(shadow,7);canvas.Children.Add(shadow);
        for(int i=0;i<Segments.Length;i++)
        {
            bool outer=i>=6;int slot=outer?i-6:i;
            bool hasQuickAdd=HasModuleQuickAdd(Segments[i]);
            double step=360.0/(outer?6:6);
            double first=-90-step/2+slot*step+halfGapAdjust;
            double last=first+step-gapDeg;
            double midAngleDeg=(first+last)/2.0;
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,outer?1:0,i,false,false,false);
            var wedge=new System.Windows.Shapes.Path{
                Data=Sector(210,210,outer?132:70,outer?195:124,first,last),
                Fill=vis.Fill,
                Stroke=vis.Stroke,
                StrokeThickness=vis.StrokeThickness,
                Effect=vis.Effect,
                IsHitTestVisible=false
            };
            canvas.Children.Add(wedge);ringPaths.Add(wedge);
            var angle=(-90+slot*(360.0/(outer?6:6)))*Math.PI/180;
            var label=new TextBlock{
                Text=SegmentIcons[i]+"\n"+
                    (Segments[i]=="Clock & Weather"?"Clock\nWeather":Segments[i]),
                FontSize=outer?12:11,
                TextAlignment=TextAlignment.Center,FontWeight=FontWeights.SemiBold,
                Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush,
                IsHitTestVisible=false
            };
            label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            double radius=outer?(hasQuickAdd?151:162):(hasQuickAdd?94:100);
            Canvas.SetLeft(label,210+radius*Math.Cos(angle)-label.DesiredSize.Width/2);
            Canvas.SetTop(label,210+radius*Math.Sin(angle)-label.DesiredSize.Height/2);
            canvas.Children.Add(label);
            var linked=new List<UIElement>{wedge,label};
            if(hasQuickAdd)
            {
                // True in-donut hit region, no separate popup HWND or
                // overlapping modal panel. Click and drag release both
                // route through QuickAddHitAt on the parent gesture.
                double markRadius=outer?182:113;
                double px=210+markRadius*Math.Cos(angle);
                double py=210+markRadius*Math.Sin(angle);
                var mark=new Ellipse{
                    Width=20,Height=20,
                    Fill=DeskSpaceHighContrastV208.QuickAction(
                        SystemParameters.HighContrast,accent),
                    Stroke=vis.Stroke,
                    StrokeThickness=1.4,
                    IsHitTestVisible=false};
                Canvas.SetLeft(mark,px-10);Canvas.SetTop(mark,py-10);
                canvas.Children.Add(mark);
                var plus=new TextBlock{
                    Text="+",FontSize=17,FontWeight=FontWeights.Bold,
                    Foreground=DeskSpaceHighContrastV208.QuickActionLabel(
                        SystemParameters.HighContrast),
                    TextAlignment=TextAlignment.Center,
                    Width=20,Height=20,IsHitTestVisible=false};
                Canvas.SetLeft(plus,px-10);Canvas.SetTop(plus,py-2-10);
                canvas.Children.Add(plus);
                linked.Add(mark);
                linked.Add(plus);
            }
            ringVisuals.Add((wedge,linked,label,outer,midAngleDeg));
        }
        var hubVis=DeskSpaceDonutThemeV380.ResolveHub(themeApp,hc);
        var centre=new Ellipse{Width=130,Height=130,Fill=ClockFace(),
            Stroke=hubVis.Stroke,
            StrokeThickness=hubVis.StrokeThickness,Effect=hubVis.Effect,IsHitTestVisible=false};
        Canvas.SetLeft(centre,145);Canvas.SetTop(centre,145);canvas.Children.Add(centre);
        clockFace=centre;
'@ 'WidgetHost BuildDonut themed ring rendering'
Swap-R380 @'
    private Brush ClockFace()
    {
        if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
        if (prefs.CenterMode != "Clock" || !prefs.ShowWeather) return accent;
        var value=DeskSpaceWeather.Latest;
        if (value == null || !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return accent;
        Color top, bottom;
        if (!value.IsDay) {top=Color.FromRgb(25,42,90);bottom=Color.FromRgb(73,61,137);}
        else if (value.Code >= 51) {top=Color.FromRgb(33,88,156);bottom=Color.FromRgb(46,61,99);}
        else if (value.Code <= 1) {top=Color.FromRgb(234,135,42);bottom=Color.FromRgb(124,77,189);}
        else {top=Color.FromRgb(49,136,209);bottom=Color.FromRgb(81,86,148);}
        return new LinearGradientBrush(top,bottom,90);
    }
'@ @'
    private Brush ClockFace()
    {
        if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
        var cur=DeskSpaceAppearanceV148.Current;
        if(cur.Style=="Modern" || cur.MixGlassFrost || cur.MixNeonEdge || cur.MixAuroraGradient || cur.MixWireframe)
            return DeskSpaceDonutThemeV380.ResolveHub(cur,false).Fill;
        if (prefs.CenterMode != "Clock" || !prefs.ShowWeather) return accent;
        var value=DeskSpaceWeather.Latest;
        if (value == null || !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return accent;
        Color top, bottom;
        if (!value.IsDay) {top=Color.FromRgb(25,42,90);bottom=Color.FromRgb(73,61,137);}
        else if (value.Code >= 51) {top=Color.FromRgb(33,88,156);bottom=Color.FromRgb(46,61,99);}
        else if (value.Code <= 1) {top=Color.FromRgb(234,135,42);bottom=Color.FromRgb(124,77,189);}
        else {top=Color.FromRgb(49,136,209);bottom=Color.FromRgb(81,86,148);}
        return new LinearGradientBrush(top,bottom,90);
    }
    private void AddDonutStyleTrayMenuV380(Forms.ContextMenuStrip menu)
    {
        var styleRoot=new Forms.ToolStripMenuItem("Donut Style & Transitions");
        styleRoot.DropDownOpening+=(_,_)=>{
            styleRoot.DropDownItems.Clear();
            DeskSpaceAppearanceV148.ReloadIfChanged();
            var cur=DeskSpaceAppearanceV148.Current;

            void SaveAndRefresh(Action<DeskSpaceAppearanceV148> mutate,bool replayOpen=false)
            {
                var next=DeskSpaceAppearanceV148.Clone(DeskSpaceAppearanceV148.Current);
                mutate(next);
                DeskSpaceAppearanceV148.Save(next);
                Dispatcher.Invoke(()=>{
                    RefreshAppearanceV148();
                    if(replayOpen && ringExpanded && activeModule==null)
                    {
                        DeskSpaceDonutThemeV380.AnimateOpen(
                            canvas,
                            ringVisuals.Select(v=>(FrameworkElement)v.Wedge).ToArray(),
                            210,210,-90,isSubArc:false);
                    }
                });
            }

            foreach(string id in DeskSpaceAppearanceV148.ValidDonutStyles)
            {
                string cap=id;
                var mi=new Forms.ToolStripMenuItem(DeskSpaceDonutThemeV380.FriendlyStyleName(cap)){
                    Checked=string.Equals(cur.DonutStyle,cap,StringComparison.OrdinalIgnoreCase)
                };
                mi.Click+=(_,_)=>SaveAndRefresh(a=>DeskSpaceAppearanceV148.ApplyDonutPresetDefaults(a,cap));
                styleRoot.DropDownItems.Add(mi);
            }

            styleRoot.DropDownItems.Add(new Forms.ToolStripSeparator());
            var mixMenu=new Forms.ToolStripMenuItem("Mix Style Layers (combine any)");
            void AddMixToggle(string label,Func<DeskSpaceAppearanceV148,bool> get,Action<DeskSpaceAppearanceV148,bool> set)
            {
                var item=new Forms.ToolStripMenuItem(label){Checked=get(cur)};
                item.Click+=(_,_)=>SaveAndRefresh(a=>set(a,!get(a)));
                mixMenu.DropDownItems.Add(item);
            }
            AddMixToggle("Glass Frost",a=>a.MixGlassFrost,(a,v)=>a.MixGlassFrost=v);
            AddMixToggle("Neon Edge Glow",a=>a.MixNeonEdge,(a,v)=>a.MixNeonEdge=v);
            AddMixToggle("Floating Petals Gaps",a=>a.MixFloatingGaps,(a,v)=>a.MixFloatingGaps=v);
            AddMixToggle("Aurora Depth Gradient",a=>a.MixAuroraGradient,(a,v)=>a.MixAuroraGradient=v);
            AddMixToggle("Minimal Wireframe",a=>a.MixWireframe,(a,v)=>a.MixWireframe=v);
            styleRoot.DropDownItems.Add(mixMenu);

            var transMenu=new Forms.ToolStripMenuItem("Open Transitions & Motion");
            foreach(string tr in DeskSpaceAppearanceV148.ValidTransitions)
            {
                string capTr=tr;
                var ti=new Forms.ToolStripMenuItem(DeskSpaceDonutThemeV380.FriendlyTransitionName(capTr)){
                    Checked=string.Equals(cur.OpenTransition,capTr,StringComparison.OrdinalIgnoreCase)
                };
                ti.Click+=(_,_)=>SaveAndRefresh(a=>a.OpenTransition=capTr,replayOpen:true);
                transMenu.DropDownItems.Add(ti);
            }
            transMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            void AddMotionToggle(string label,Func<DeskSpaceAppearanceV148,bool> get,Action<DeskSpaceAppearanceV148,bool> set)
            {
                var item=new Forms.ToolStripMenuItem(label){Checked=get(cur)};
                item.Click+=(_,_)=>SaveAndRefresh(a=>set(a,!get(a)));
                transMenu.DropDownItems.Add(item);
            }
            AddMotionToggle("Motion: Radial Hover Lift",a=>a.MotionHoverLift,(a,v)=>a.MotionHoverLift=v);
            AddMotionToggle("Motion: Hover Accent Glow",a=>a.MotionHoverGlow,(a,v)=>a.MotionHoverGlow=v);
            AddMotionToggle("Motion: Staggered Ripple",a=>a.MotionStaggerRipple,(a,v)=>a.MotionStaggerRipple=v);
            AddMotionToggle("Motion: Fluid Scroll Glide",a=>a.MotionScrollGlide,(a,v)=>a.MotionScrollGlide=v);
            styleRoot.DropDownItems.Add(transMenu);
        };
        menu.Items.Add(styleRoot);
    }
'@ 'WidgetHost ClockFace and AddDonutStyleTrayMenuV380'
Swap-R380 @'
    private void RefreshAppearanceV148()
    {
        DeskSpaceAppearanceV148.Apply(this);
        if(activeModule==null)
        {
            if(ringExpanded)BuildDonut();
            else BuildCompact();
        }
        else UpdateCenter();
    }
'@ @'
    private void RefreshAppearanceV148()
    {
        DeskSpaceAppearanceV148.ReloadIfChanged(force:true);
        DeskSpaceAppearanceV148.Apply(this);
        if(activeModule==null)
        {
            if(ringExpanded)BuildDonut();
            else BuildCompact();
        }
        else UpdateCenter();
        informationOverlay?.RefreshThemeV380();
        appsWheel?.RefreshThemeV380();
        excelWheel?.RefreshThemeV380();
    }
'@ 'WidgetHost RefreshAppearanceV148 sync sub-donuts'
Finish-Patch $hostPath

# 4b. Surgical hook: DeskSpaceInformationV36.cs (tight concentric clipped bounds & monitor-aware shelf)
$infoPath = "$dstRoot/DeskSpaceInformationV36.cs"
Begin-Patch $infoPath
Swap-R380 @'
    private readonly ScrollViewer shelfScroll;
    private readonly TextBlock shelfTitle;
'@ @'
    private readonly ScrollViewer shelfScroll;
    private readonly TextBlock shelfTitle;
    private readonly Button headerAddBtn;
    private readonly Button headerAppsBtn;
'@ 'DeskSpaceInformationV36 header button fields'
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
    private string activeArcAppPath="";
    private double activeArcAppAngle=-90;
    private int filePage;
    private readonly HashSet<string> loadingArcApps=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,int> arcAppScanGens=new(StringComparer.OrdinalIgnoreCase);
    private long warmedAppsRev=-1;
    private DateTime warmedConfigStampUtc=DateTime.MinValue;
    private bool contextMenuOpen;
    private readonly DeskSpaceDonutThemeV380.SubDonutCalloutPillV380 calloutPill;
    private readonly List<(List<UIElement> LinkedVisuals,TextBlock Label,bool IsTruncated,string FullName,string? Badge,double MidAngle,Point OuterAnchor,int Depth,bool IsAction)> wedgeVisualsV380=new();
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
'@ 'DeskSpaceInformationV36 arcApps WarmStart'
Swap-R380 '    public int SavedCount => all.Count;' @'
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
'@ 'DeskSpaceInformationV36 SavedCount'
Swap-R380 '        arcCanvas=new Canvas{Width=488,Height=222,Focusable=true};' @'
        calloutPill=new DeskSpaceDonutThemeV380.SubDonutCalloutPillV380(owner);
        arcCanvas=new Canvas{Width=1,Height=1,Focusable=true};
'@ 'DeskSpaceInformationV36 tight initial canvas'
Swap-R380 @'
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(arcCanvas,
            ()=>all.Count>PageSize,
            ()=>arc.IsOpen&&!editorActive,
            dir=>Slide(dir));
'@ @'
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(arcCanvas,
            ()=>HasAnyArcOverflow(),
            ()=>arc.IsOpen&&!editorActive,
            dir=>HandleScrollStep(dir,SlotAtPointerOrHover()));
'@ 'DeskSpaceInformationV36 middleScroll predicate'
Swap-R380 @'
        arcCanvas.PreviewMouseLeftButtonDown+=OnArcPointerDown;
        arcCanvas.PreviewMouseLeftButtonUp+=OnArcPointerUp;
'@ @'
        arcCanvas.PreviewMouseLeftButtonDown+=OnArcPointerDown;
        arcCanvas.PreviewMouseLeftButtonUp+=OnArcPointerUp;
        arcCanvas.PreviewMouseRightButtonDown+=OnArcRightButtonDown;
'@ 'DeskSpaceInformationV36 right click handler wireup'
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
        newHoverTimer.Stop();
        middleScroll.Stop();
        pointerTimer.Stop();
        CloseTransientPreview();
        StopEdgeScroll();
        capturedDragSelection=false;
        if(!arc.IsOpen)return;
        arc.IsOpen=false;
        hoveredChild=-1;keyboardIndex=-1;
        changed();
    }
'@ @'
    internal void RefreshThemeV380()
    {
        if(!disposed && arc.IsOpen)
            RenderArc();
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
            if(!arcBounds.IsEmpty)
            {
                DeskSpaceDonutThemeV380.AnimateOpen(
                    arcCanvas,
                    childWedges.Cast<FrameworkElement>().ToArray(),
                    DeskSpaceSubArcGeometryV48.CX-arcBounds.Left+ArcMargin,
                    DeskSpaceSubArcGeometryV48.CY-arcBounds.Top+ArcMargin,
                    -90,isSubArc:true);
            }
            pointerTimer.Start();
            changed();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace information",
            MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    public void HideArc()
    {
        calloutPill.Hide();
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
'@ 'DeskSpaceInformationV36 Show and HideArc radial app modes'
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
    private int SlotAtPoint(Point point)
    {
        foreach(var entry in arcHitZones)
            if(entry.hit.FillContains(point))return entry.index;
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
        if(!editorActive && hoveredChild==arcHitZones.Count-1)
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
           editorActive)return;
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
        if(!arc.IsOpen || disposed)return;
        ProcessWheelPointer(e.GetPosition(arcCanvas));
    }
    private void ProcessWheelPointer(Point point)
    {
        if(disposed || !arc.IsOpen || editorActive)return;
        // Full-height edge corridors rather than tiny 32x29 arrow
        // controls. Pointer-position polling continues when the cursor
        // remains motionless even if pagination rebuilds its child wedges.
        // These bands lie OUTSIDE the curved saved-item sectors.
        int direction=point.Y>=0 && point.Y<=222 ?
            point.X<=82 && CanPage(-1)?-1:
            point.X>=406 && CanPage(1)?1:0:0;
        // Actual wedges take priority over edge corridors. A user pointing
        // at item 4 must be able to scroll, not trigger edge-page polling.
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
        string? keepHoverKind=(hoveredChild>=0 && hoveredChild<arcHitZones.Count)?arcHitZones[hoveredChild].kind:null;
        string keepHoverKey=(hoveredChild>=0 && hoveredChild<arcHitZones.Count)
            ? (keepHoverKind=="infofile"?arcHitZones[hoveredChild].filePath:arcHitZones[hoveredChild].appPath)
            : "";
        double keepMidAngle=(hoveredChild>=0 && hoveredChild<wedgeVisualsV380.Count)?wedgeVisualsV380[hoveredChild].MidAngle:double.NaN;
        bool keepPill=calloutPill.IsVisible && keepHoverKind is "infoapp" or "infofile";
        calloutPill.Hide();
        newHoverTimer.Stop();
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        double gapInset=DeskSpaceDonutThemeV380.GetSubArcGapInset(themeApp);
        arcCanvas.Children.Clear();
        childWedges.Clear();childLabels.Clear();arcHitZones.Clear();wedgeVisualsV380.Clear();
        hoveredChild=-1;

        var drawItems=new List<(string Label,string Kind,Guid? Id,string AppPath,string FilePath,double A,double B,int Depth,string Tip,bool IsAction)>();
        var fileMetaLookup=new Dictionary<string,AppInfoFileRecordV380>(StringComparer.OrdinalIgnoreCase);

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
                    if(!string.IsNullOrEmpty(mf.FullPath))
                        fileMetaLookup[mf.FullPath]=mf;
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
                        if(!string.IsNullOrEmpty(mf.FullPath))
                            fileMetaLookup[mf.FullPath]=mf;
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

        var rawShapes=drawItems.Select(x=>{
            double g=(x.B-x.A)>gapInset*2.5?gapInset:0;
            return InfoSector(x.Depth,x.A+g,x.B-g);
        }).ToArray();
        arcBounds=DeskSpaceSubArcGeometryV48.Bounds(rawShapes);
        arcCanvas.Width=arcBounds.Width+ArcMargin*2;
        arcCanvas.Height=arcBounds.Height+ArcMargin*2;

        var clippedGeoms=new Geometry[drawItems.Count];
        var d0ScrollWedges=new List<System.Windows.Shapes.Path>();
        var d1ScrollWedges=new List<System.Windows.Shapes.Path>();
        var sectorVisuals=new DonutWedgeVisualV380[drawItems.Count];

        // Pass 1: Wedge paths at Z=1
        for(int i=0;i<drawItems.Count;i++)
        {
            var item=drawItems[i];
            Geometry hit=DeskSpaceSubArcGeometryV48.Clip(rawShapes[i],arcBounds,ArcMargin);
            clippedGeoms[i]=hit;
            bool isSelectedParent=item.Kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                string.Equals(item.AppPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase);
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,item.Depth+2,i,item.IsAction,isSelectedParent,false);
            sectorVisuals[i]=vis;
            var sector=new System.Windows.Shapes.Path{
                Data=hit,
                Stroke=vis.Stroke,StrokeThickness=vis.StrokeThickness,
                Fill=vis.Fill,Effect=vis.Effect,
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
            var vis=sectorVisuals[i];
            double midA=(item.A+item.B)/2;
            var outerPt=DeskSpaceSubArcGeometryV48.Polar(InfoOuter(item.Depth),midA);
            Point outerAnchor=new(DeskSpaceSubArcGeometryV48.CX+outerPt.X-arcBounds.Left+ArcMargin,
                                  DeskSpaceSubArcGeometryV48.CY+outerPt.Y-arcBounds.Top+ArcMargin);

            if(item.Depth==0 && item.Kind is "collection" or "newcollection")
            {
                double rMidClassic=(InfoInner(0)+InfoOuter(0))/2;
                var p=DeskSpaceSubArcGeometryV48.Polar(rMidClassic,midA);
                double x=DeskSpaceSubArcGeometryV48.CX+p.X-arcBounds.Left+ArcMargin;
                double y=DeskSpaceSubArcGeometryV48.CY+p.Y-arcBounds.Top+ArcMargin;
                FontWeight fw=item.IsAction?FontWeights.SemiBold:FontWeights.Normal;
                var label=new TextBlock{
                    Text=item.Label,
                    Width=62,
                    TextAlignment=TextAlignment.Center,
                    TextTrimming=TextTrimming.CharacterEllipsis,
                    FontSize=11,
                    FontWeight=fw,
                    Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush,
                    ToolTip=item.Tip,
                    IsHitTestVisible=false};
                Panel.SetZIndex(label,10);
                Canvas.SetLeft(label,x-31);
                Canvas.SetTop(label,y-9);
                arcCanvas.Children.Add(label);
                bool isColTrunc=DeskSpaceDonutThemeV380.SubDonutCalloutPillV380.IsLabelTruncated(
                    item.Label,62,20,11,fw,wraps:false);
                wedgeVisualsV380.Add((new List<UIElement>{childWedges[i],label},label,isColTrunc,item.Label,
                    item.Kind=="collection"?"SAVED":null,midA,outerAnchor,item.Depth+2,item.IsAction));
                continue;
            }

            double rMid=(InfoInner(item.Depth)+InfoOuter(item.Depth))/2;
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
            double lblFontSize=item.Depth==0?(hasIcon?9.5:10):10;
            FontWeight lblWeight=item.IsAction?FontWeights.SemiBold:FontWeights.Normal;

            var lbl=new TextBlock{
                Text=item.Label,
                Width=boxWidth,
                MaxHeight=maxLabelH,
                FontSize=lblFontSize,
                FontWeight=lblWeight,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush,
                IsHitTestVisible=false};
            lbl.Measure(new Size(boxWidth,maxLabelH));
            double actualH=Math.Min(maxLabelH,Math.Max(12,lbl.DesiredSize.Height));
            var linked=new List<UIElement>{childWedges[i],lbl};
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
                linked.Add(appIcon);
            }
            else
            {
                Canvas.SetLeft(lbl,boxLeft);
                Canvas.SetTop(lbl,Math.Clamp(midY-actualH*.5,2,Math.Max(2,arcCanvas.Height-actualH-2)));
            }
            Panel.SetZIndex(lbl,10);
            arcCanvas.Children.Add(lbl);

            AppInfoFileRecordV380? meta=null;
            if(item.Kind=="infofile" && !string.IsNullOrEmpty(item.FilePath))
                fileMetaLookup.TryGetValue(item.FilePath,out meta);
            string fullName=meta!=null?meta.DisplayName:item.Label.Replace("\u200B",string.Empty);
            string? badge=meta!=null?meta.KindBadge:(item.Kind=="infoapp"?"APP":null);
            bool isTrunc=DeskSpaceDonutThemeV380.SubDonutCalloutPillV380.IsLabelTruncated(
                fullName,boxWidth,maxLabelH,lblFontSize,lblWeight,wraps:true);
            wedgeVisualsV380.Add((linked,lbl,isTrunc,fullName,badge,midA,outerAnchor,item.Depth+2,item.IsAction));
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
        if(keepPill && !string.IsNullOrEmpty(keepHoverKey))
        {
            int keepIdx=arcHitZones.FindIndex(z=>z.kind==keepHoverKind &&
                string.Equals(keepHoverKind=="infofile"?z.filePath:z.appPath,keepHoverKey,StringComparison.OrdinalIgnoreCase));
            if(keepIdx>=0 && keepIdx<wedgeVisualsV380.Count &&
               wedgeVisualsV380[keepIdx].IsTruncated &&
               Math.Abs(wedgeVisualsV380[keepIdx].MidAngle-keepMidAngle)<1.0)
            {
                hoveredChild=keepIdx;
                var kwv=wedgeVisualsV380[keepIdx];
                calloutPill.Show(arcCanvas,kwv.OuterAnchor,kwv.MidAngle,kwv.FullName,kwv.Badge);
            }
        }
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
        calloutPill.Hide();
        newHoverTimer.Stop();
        CloseTransientPreview();
        if(hoveredChild>=0 && hoveredChild<childWedges.Count)
        {
            var themeApp=DeskSpaceAppearanceV148.Current;
            bool hc=SystemParameters.HighContrast;
            for(int i=0;i<childWedges.Count;i++)
            {
                var z=arcHitZones[i];
                bool isSelApp=z.kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                    string.Equals(z.appPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase);
                bool isAction=z.kind is "manageapps" or "newcollection" or "choosefields";
                var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,z.depth+2,i,isAction,isSelApp,false);
                childWedges[i].Fill=vis.Fill;
                childWedges[i].Stroke=vis.Stroke;
                childWedges[i].StrokeThickness=vis.StrokeThickness;
                childWedges[i].Effect=vis.Effect;
                if(i<wedgeVisualsV380.Count)
                {
                    var wv=wedgeVisualsV380[i];
                    wv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
                    DeskSpaceDonutThemeV380.ApplyHoverLift(wv.LinkedVisuals,false,wv.MidAngle,themeApp);
                }
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
        calloutPill.Hide();
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
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int i=0;i<childWedges.Count;i++)
        {
            var w=childWedges[i];
            var z=arcHitZones[i];
            bool hov=i==sector;
            bool isSelApp=z.kind=="infoapp" && !string.IsNullOrEmpty(activeArcAppPath) &&
                string.Equals(z.appPath,activeArcAppPath,StringComparison.OrdinalIgnoreCase);
            bool isAction=z.kind is "manageapps" or "newcollection" or "choosefields";
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,z.depth+2,i,isAction,isSelApp,hov);
            w.Fill=vis.Fill;
            w.Stroke=vis.Stroke;
            w.StrokeThickness=vis.StrokeThickness;
            w.Effect=vis.Effect;
            if(i<wedgeVisualsV380.Count)
            {
                var wv=wedgeVisualsV380[i];
                wv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
                DeskSpaceDonutThemeV380.ApplyHoverLift(wv.LinkedVisuals,hov,wv.MidAngle,themeApp);
            }
        }
        if(sector>=0 && sector<wedgeVisualsV380.Count && wedgeVisualsV380[sector].IsTruncated)
            calloutPill.Show(arcCanvas,wedgeVisualsV380[sector].OuterAnchor,wedgeVisualsV380[sector].MidAngle,wedgeVisualsV380[sector].FullName,wedgeVisualsV380[sector].Badge);
        else
            calloutPill.Hide();
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
'@ 'DeskSpaceInformationV36 concentric 1-app and multi-app RenderArc and interaction'
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
'@ 'DeskSpaceInformationV36 keyboard and multi-depth paging support'
Swap-R380 @'
    private void Shelf(string title)
    {
        MaskRevealed();
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        warning.Text="";
        SetCompactShelfLayout(editorActive);
'@ @'
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
'@ 'DeskSpaceInformationV36 Shelf custom sizing and header button toggle'
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
'@ 'DeskSpaceInformationV36 Apps shelf header button'
Swap-R380 @'
    private void MaskRevealed()
'@ @'
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
'@ 'DeskSpaceInformationV36 clean File Info shelf, Choose Fields, Manage Apps'
Swap-R380 @'
        arcCanvas.MouseWheel-=ScrollArc;
        newHoverTimer.Stop();edgeTimer.Stop();pointerTimer.Stop();lockTimer.Stop();
        middleScroll.Dispose();
        vault.Dispose();
'@ @'
        arcCanvas.MouseWheel-=ScrollArc;
        newHoverTimer.Stop();edgeTimer.Stop();pointerTimer.Stop();lockTimer.Stop();
        calloutPill.Dispose();
        middleScroll.Dispose();
        vault.Dispose();
'@ 'DeskSpaceInformationV36 Dispose calloutPill'
Finish-Patch $infoPath

# 5. Surgical hook: DeskSpaceApplicationsV55.cs (CI R232 baseline does NOT include r262)
$appsPath = "$dstRoot/DeskSpaceApplicationsV55.cs"
Begin-Patch $appsPath
Swap-R380 @'
    private readonly List<(Geometry Hit,string Kind,string Path)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> fills=new();
    private readonly List<(string Label,string Kind,string Path,double A,double B,int Depth)> items=new();
'@ @'
    private readonly List<(Geometry Hit,string Kind,string Path)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> fills=new();
    private readonly List<(string Label,string Kind,string Path,double A,double B,int Depth)> items=new();
    private readonly List<(List<UIElement> LinkedVisuals,TextBlock Label,bool IsTruncated,string FullName,string? Badge,double MidAngle,Point OuterAnchor,int Depth,bool IsAction)> wedgeVisualsV380=new();
    private readonly DeskSpaceDonutThemeV380.SubDonutCalloutPillV380 calloutPill;
'@ 'DeskSpaceApplicationsV55 calloutPill and wedge visuals'
Swap-R380 @'
    private void Render()
    {
        items.Clear();hitZones.Clear();fills.Clear();canvas.Children.Clear();hovered=-1;
'@ @'
    private void Render()
    {
        string? keepHoverKind=(hovered>=0 && hovered<hitZones.Count)?hitZones[hovered].Kind:null;
        string? keepHoverPath=(hovered>=0 && hovered<hitZones.Count)?hitZones[hovered].Path:null;
        double keepMidAngle=(hovered>=0 && hovered<wedgeVisualsV380.Count)?wedgeVisualsV380[hovered].MidAngle:double.NaN;
        bool keepPill=calloutPill.IsVisible && keepHoverKind is "launch" or "launchfile";
        calloutPill.Hide();
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        double gapInset=DeskSpaceDonutThemeV380.GetSubArcGapInset(themeApp);
        items.Clear();hitZones.Clear();fills.Clear();wedgeVisualsV380.Clear();canvas.Children.Clear();hovered=-1;
'@ 'DeskSpaceApplicationsV55 Render theme header'
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
    public bool IsArcOpen=>arc.IsOpen;
    public int PinnedCount=>pinned().Count;
'@ @'
    public bool IsArcOpen=>arc.IsOpen;
    public int PinnedCount=>pinned().Count;
    private bool contextMenuOpen;
    internal bool IsContextMenuOpen=>contextMenuOpen;
    internal bool IsPointerNearArc()
    {
        if(contextMenuOpen)return true;
        if(disposed || !arc.IsOpen || !canvas.IsLoaded)return false;
        try
        {
            var pt=DeskSpaceWheelHoverV35.MouseIn(canvas);
            return pt.X>=-26 && pt.X<=canvas.Width+26 && pt.Y>=-26 && pt.Y<=canvas.Height+26;
        }
        catch(InvalidOperationException){return false;}
    }
'@ 'DeskSpaceApplicationsV55 context menu and pointer near arc guards'
Swap-R380 @'
        canvas=new Canvas{Width=1,Height=1,Background=null};
        canvas.MouseMove+=Move;
        canvas.PreviewMouseLeftButtonDown+=Down;
        canvas.MouseWheel+=Wheel;
        arc=new Popup{PlacementTarget=owner,Placement=PlacementMode.Relative,
            AllowsTransparency=true,StaysOpen=true,Child=canvas};
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
'@ @'
        calloutPill=new DeskSpaceDonutThemeV380.SubDonutCalloutPillV380(owner);
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
        var shapes=items.Select(x=>{
            double g=(x.B-x.A)>gapInset*2.5?gapInset:0;
            return AppSector(x.Depth,x.A+g,x.B-g);
        }).ToArray();
        bounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        canvas.Width=bounds.Width+margin*2;
        canvas.Height=bounds.Height+margin*2;
        var clippedGeoms=new Geometry[items.Count];
        var depth1Wedges=new List<System.Windows.Shapes.Path>();
        var depth2FileWedges=new List<System.Windows.Shapes.Path>();
        var sectorVisuals=new DonutWedgeVisualV380[items.Count];
        // Pass 1: all wedge geometries at Z=1
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];
            var geometry=DeskSpaceSubArcGeometryV48.Clip(shapes[i],bounds,margin);
            clippedGeoms[i]=geometry;
            bool isAction=item.Kind is "add" or "allfiles";
            bool isSelectedApp=item.Kind=="launch" && !string.IsNullOrEmpty(activeAppPath) &&
                string.Equals(item.Path,activeAppPath,StringComparison.OrdinalIgnoreCase);
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,item.Depth+2,i,isAction,isSelectedApp,false);
            sectorVisuals[i]=vis;
            ToolTip tip=item.Kind=="launch"
                ? CreateCompactTooltipV380(item.Label,"Click to open · Right-click for files or Unpin")
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
                Data=geometry,Stroke=vis.Stroke,StrokeThickness=vis.StrokeThickness,
                Fill=vis.Fill,Effect=vis.Effect,
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
            var vis=sectorVisuals[i];
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
            double labelFontSize=item.Depth==1?9.5:10;
            FontWeight labelWeight=isAction?FontWeights.SemiBold:FontWeights.Normal;
            var label=new TextBlock{
                Text=item.Label,
                Width=boxWidth,
                MaxHeight=maxLabelH,
                FontSize=labelFontSize,
                FontWeight=labelWeight,
                TextTrimming=TextTrimming.CharacterEllipsis,
                TextWrapping=TextWrapping.Wrap,
                TextAlignment=TextAlignment.Center,
                IsHitTestVisible=false,
                Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush};
            label.Measure(new Size(boxWidth,maxLabelH));
            double actualH=Math.Min(maxLabelH,Math.Max(12,label.DesiredSize.Height));
            var linked=new List<UIElement>{fills[i],label};
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
                linked.Add(appIcon);
            }
            else
            {
                Canvas.SetLeft(label,boxLeft);
                Canvas.SetTop(label,Math.Clamp(midY-actualH*.5,2,Math.Max(2,canvas.Height-actualH-2)));
            }
            Panel.SetZIndex(label,10);
            canvas.Children.Add(label);

            PinnedAppFileItemV380? meta=null;
            if(item.Kind=="launchfile")fileLookup.TryGetValue(item.Path,out meta);
            string fullName=meta!=null?meta.DisplayName:item.Label.Replace("\u200B",string.Empty);
            string? badge=meta!=null?meta.KindBadge:(item.Kind=="launch"?"PINNED":null);
            bool isTrunc=DeskSpaceDonutThemeV380.SubDonutCalloutPillV380.IsLabelTruncated(
                fullName,boxWidth,maxLabelH,labelFontSize,labelWeight,wraps:true);
            var outerPt=DeskSpaceSubArcGeometryV48.Polar(AppOuter(item.Depth),midA);
            Point outerAnchor=new(DeskSpaceSubArcGeometryV48.CX+outerPt.X-bounds.Left+margin,
                                  DeskSpaceSubArcGeometryV48.CY+outerPt.Y-bounds.Top+margin);
            wedgeVisualsV380.Add((linked,label,isTrunc,fullName,badge,midA,outerAnchor,item.Depth+2,isAction));
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
        if(keepPill && !string.IsNullOrEmpty(keepHoverPath))
        {
            int keepIdx=hitZones.FindIndex(h=>h.Kind==keepHoverKind &&
                string.Equals(h.Path,keepHoverPath,StringComparison.OrdinalIgnoreCase));
            if(keepIdx>=0 && keepIdx<wedgeVisualsV380.Count &&
               wedgeVisualsV380[keepIdx].IsTruncated &&
               Math.Abs(wedgeVisualsV380[keepIdx].MidAngle-keepMidAngle)<1.0)
            {
                hovered=keepIdx;
                var kwv=wedgeVisualsV380[keepIdx];
                calloutPill.Show(canvas,kwv.OuterAnchor,kwv.MidAngle,kwv.FullName,kwv.Badge);
            }
        }
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
    internal void RefreshThemeV380()
    {
        if(!disposed && arc.IsOpen)
            Render();
    }
    public void Show()
    {
        if(disposed)return;
        group="";childPage=0;ClearActiveAppFilesV380();
        Render();arc.IsOpen=owner.IsVisible;
        if(arc.IsOpen && !bounds.IsEmpty)
        {
            DeskSpaceDonutThemeV380.AnimateOpen(
                canvas,
                fills.Cast<FrameworkElement>().ToArray(),
                DeskSpaceSubArcGeometryV48.CX-bounds.Left+10,
                DeskSpaceSubArcGeometryV48.CY-bounds.Top+10,
                ApplicationsParentCenter,isSubArc:true);
        }
    }
    public void HideArc(){calloutPill.Hide();contextMenuOpen=false;middleScroll.Stop();arc.IsOpen=false;group="";childPage=0;ClearActiveAppFilesV380();}
    private int Slot(Point point)
    {
        for(int i=hitZones.Count-1;i>=0;i--)
            if(hitZones[i].Hit.FillContains(point))return i;
        return -1;
    }
    private void Hover(Point point)
    {
        if(contextMenuOpen)return;
        int i=Slot(point);
        if(i==hovered)return;
        hovered=i;
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int j=0;j<fills.Count;j++)
        {
            bool activeParent=hitZones[j].Kind=="launch" && !string.IsNullOrEmpty(activeAppPath) &&
                string.Equals(hitZones[j].Path,activeAppPath,StringComparison.OrdinalIgnoreCase);
            bool hov=j==i;
            var wv=j<wedgeVisualsV380.Count?wedgeVisualsV380[j]:default;
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,wv.LinkedVisuals!=null?wv.Depth:2,j,wv.IsAction,activeParent,hov);
            fills[j].Fill=vis.Fill;
            fills[j].Stroke=vis.Stroke;
            fills[j].StrokeThickness=vis.StrokeThickness;
            fills[j].Effect=vis.Effect;
            if(wv.Label!=null)
            {
                wv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
                DeskSpaceDonutThemeV380.ApplyHoverLift(wv.LinkedVisuals,hov,wv.MidAngle,themeApp);
            }
        }
        if(i>=0 && i<wedgeVisualsV380.Count && wedgeVisualsV380[i].IsTruncated)
            calloutPill.Show(canvas,wedgeVisualsV380[i].OuterAnchor,wedgeVisualsV380[i].MidAngle,wedgeVisualsV380[i].FullName,wedgeVisualsV380[i].Badge);
        else
            calloutPill.Hide();
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
        if(!disposed&&arc.IsOpen&&!contextMenuOpen)Hover(e.GetPosition(canvas));
    }
    internal bool UnpinAppV380(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        removePin(path);
        if(string.Equals(activeAppPath,path,StringComparison.OrdinalIgnoreCase))
            ClearActiveAppFilesV380();
        int maxStart=Math.Max(0,Children().Count-(DeskSpaceSubDonutStandardV62.Visible-1));
        childPage=Math.Clamp(childPage,0,maxStart);
        Render();
        return true;
    }
    internal void RefreshPinsV380()
    {
        if(disposed)return;
        if(!string.IsNullOrEmpty(activeAppPath) &&
           !pinned().Any(p=>string.Equals(p,activeAppPath,StringComparison.OrdinalIgnoreCase)))
        {
            ClearActiveAppFilesV380();
        }
        int maxStart=Math.Max(0,Children().Count-(DeskSpaceSubDonutStandardV62.Visible-1));
        childPage=Math.Clamp(childPage,0,maxStart);
        if(arc.IsOpen)Render();
    }
    private void OpenFilesRightClickV380(object sender,MouseButtonEventArgs e)
    {
        if(disposed || !arc.IsOpen)return;
        int index=Slot(e.GetPosition(canvas));
        if(index<0 || hitZones[index].Kind!="launch")return;
        string pinnedPath=hitZones[index].Path;
        if(!IsLaunchable(pinnedPath))return;
        string displayLabel=DeskSpacePinnedAppFilesV380.ResolvePinnedAppDisplayName(pinnedPath);
        if(string.IsNullOrWhiteSpace(displayLabel))
            displayLabel=System.IO.Path.GetFileNameWithoutExtension(pinnedPath);
        var cm=new ContextMenu();
        var openItem=new MenuItem{Header=$"Open {displayLabel}"};
        openItem.Click+=(_,_)=>{
            HideArc();
            owner.Dispatcher.BeginInvoke(new Action(()=>
            {
                try{DeskSpacePinnedAppFilesV380.TryLaunchPinnedAppDirect(pinnedPath,owner);}
                catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
            }));
        };
        var filesItem=new MenuItem{Header="Browse All Compatible Files…"};
        filesItem.Click+=(_,_)=>{
            HideArc();
            owner.Dispatcher.BeginInvoke(new Action(()=>
            {
                try{DeskSpacePinnedAppFilesV380.Show(owner,pinnedPath);}
                catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
            }));
        };
        var unpinItem=new MenuItem{Header=$"✕ Unpin {displayLabel}"};
        unpinItem.Click+=(_,_)=>UnpinAppV380(pinnedPath);
        cm.Items.Add(openItem);
        cm.Items.Add(filesItem);
        cm.Items.Add(new Separator());
        cm.Items.Add(unpinItem);
        cm.PlacementTarget=canvas;
        calloutPill.Hide();
        contextMenuOpen=true;
        cm.Closed+=(_,_)=>contextMenuOpen=false;
        cm.IsOpen=true;
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
        disposed=true;HideArc();calloutPill.Dispose();middleScroll.Dispose();
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
Swap-R380 @'
    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            DeskSpaceReducedMotionV209.Reset(canvas);
            return;
        }
'@ @'
    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        var app=DeskSpaceAppearanceV148.Current;
        if(!DeskSpaceReducedMotionV209.Enabled() ||
           !app.MotionScrollGlide ||
           string.Equals(app.OpenTransition,"Instant",StringComparison.OrdinalIgnoreCase))
        {
            DeskSpaceReducedMotionV209.Reset(canvas);
            return;
        }
'@ 'DeskSpaceSubDonutStandardV62 MotionScrollGlide guard'
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

# 8c. Copy DeskSpaceSettingsStudioV148.cs (upgraded appearance model, 6 styles, mixes, 7 transitions & live preview)
$studioSrc = 'src/DeskSpace/DeskSpaceSettingsStudioV148.cs'
$studioPath = "$dstRoot/DeskSpaceSettingsStudioV148.cs"
if (!(Test-Path -LiteralPath $studioSrc)) {
    throw 'R380 missing src/DeskSpace/DeskSpaceSettingsStudioV148.cs'
}
Copy-Item -LiteralPath $studioSrc -Destination $studioPath -Force
if ((Get-FileHash $studioSrc -Algorithm SHA256).Hash -ne (Get-FileHash $studioPath -Algorithm SHA256).Hash) {
    throw 'R380 DeskSpaceSettingsStudioV148.cs copy SHA256 mismatch'
}
if (!(Test-Path -LiteralPath "$dstRoot/DeskSpaceDpiAuditV285.cs")) {
    Begin-Patch $studioPath
    Swap-R380 @'
        body.Children.Add(Button(
            "Save actual monitor / DPI diagnostic to Downloads",()=>{
                var output=DeskSpaceDpiAuditV285.SaveToDownloads();
                status.Text="Monitor and DPI report saved: "+output;
            }));
'@ '' 'DeskSpaceSettingsStudioV148 omit R285-only DPI audit button when V285 absent'
    Finish-Patch $studioPath
}

# 8d. Surgical hook: DeskSpaceExcelWheelV42.cs
$excelWheelPath = "$dstRoot/DeskSpaceExcelWheelV42.cs"
Begin-Patch $excelWheelPath
Swap-R380 @'
    private readonly List<(int Index,Guid? Id,Geometry Hit)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> sectors=new();
'@ @'
    private readonly List<(int Index,Guid? Id,Geometry Hit)> hitZones=new();
    private readonly List<System.Windows.Shapes.Path> sectors=new();
    private readonly List<(List<UIElement> LinkedVisuals,TextBlock Label,bool IsTruncated,string FullName,string? Badge,double MidAngle,Point OuterAnchor,bool IsAction)> rootWedgeVisualsV380=new();
    private readonly DeskSpaceDonutThemeV380.SubDonutCalloutPillV380 calloutPill;
'@ 'DeskSpaceExcelWheelV42 calloutPill and root visuals'
Swap-R380 @'
        internal readonly List<Geometry> Zones=new();
        internal readonly List<System.Windows.Shapes.Path> Wedges=new();
'@ @'
        internal readonly List<Geometry> Zones=new();
        internal readonly List<System.Windows.Shapes.Path> Wedges=new();
        internal readonly List<(List<UIElement> LinkedVisuals,TextBlock Label,bool IsTruncated,string FullName,string? Badge,double MidAngle,Point OuterAnchor,bool IsAction)> VisualsV380=new();
'@ 'DeskSpaceExcelWheelV42 ChildRing VisualsV380'
Swap-R380 @'
    public bool IsOpen=>arc.IsOpen || shelf.IsOpen ||
        childRings.Any(r=>r.Popup.IsOpen);
    public bool IsArcOpen=>arc.IsOpen;
    public bool IsPasting=>pasting;
    public int SavedCount=>formats.Count+formatTemplatesV130.Count;

    public DeskSpaceExcelWheelV42(Window window,Action onCommit)
    {
        owner=window;collapseParent=onCommit;
'@ @'
    public bool IsOpen=>arc.IsOpen || shelf.IsOpen ||
        childRings.Any(r=>r.Popup.IsOpen);
    public bool IsArcOpen=>arc.IsOpen;
    public bool IsPasting=>pasting;
    public int SavedCount=>formats.Count+formatTemplatesV130.Count;

    internal bool IsPointerNearArc()
    {
        if(disposed || !arc.IsOpen || !canvas.IsLoaded)return false;
        try
        {
            var pt=DeskSpaceWheelHoverV35.MouseIn(canvas);
            if(pt.X>=-26 && pt.X<=canvas.Width+26 && pt.Y>=-26 && pt.Y<=canvas.Height+26)
                return true;
            foreach(var ring in childRings.ToArray())
            {
                if(!ring.Popup.IsOpen || !ring.Surface.IsLoaded)continue;
                var rp=DeskSpaceWheelHoverV35.MouseIn(ring.Surface);
                if(rp.X>=-26 && rp.X<=ring.Surface.Width+26 && rp.Y>=-26 && rp.Y<=ring.Surface.Height+26)
                    return true;
            }
            return false;
        }
        catch(InvalidOperationException){return false;}
    }

    internal void RefreshThemeV380()
    {
        if(disposed || !arc.IsOpen)return;
        var savedRings=childRings.Where(r=>r.Popup.IsOpen).ToList();
        Render();
        foreach(var oldRing in savedRings)
        {
            if(oldRing.Depth==1)
            {
                ExpandRing(oldRing.Depth,oldRing.Group,oldRing.Angle,oldRing.ParentSweep);
            }
        }
    }

    public DeskSpaceExcelWheelV42(Window window,Action onCommit)
    {
        owner=window;collapseParent=onCommit;
        calloutPill=new DeskSpaceDonutThemeV380.SubDonutCalloutPillV380(owner);
'@ 'DeskSpaceExcelWheelV42 IsPointerNearArc, RefreshThemeV380 and calloutPill ctor'
Swap-R380 @'
    private void CloseFrom(int depth)
    {
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
'@ @'
    private void CloseFrom(int depth)
    {
        calloutPill.Hide();
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
'@ 'DeskSpaceExcelWheelV42 CloseFrom hide pill'
Swap-R380 @'
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
        // Standard shared five-slot sweep and relative WPF Popup positioning.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ring.Angle,ring.ParentSweep).Half;
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
'@ @'
        ring.Popup.IsOpen=true;
        if(!ring.Bounds.IsEmpty)
        {
            DeskSpaceDonutThemeV380.AnimateOpen(
                ring.Surface,
                ring.Wedges.Cast<FrameworkElement>().ToArray(),
                DeskSpaceSubArcGeometryV48.CX-ring.Bounds.Left+10,
                DeskSpaceSubArcGeometryV48.CY-ring.Bounds.Top+10,
                ring.Angle,isSubArc:true);
        }
    }
    private void RenderRing(ChildRing ring)
    {
        calloutPill.Hide();
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        double gapInset=DeskSpaceDonutThemeV380.GetSubArcGapInset(themeApp);
        var all=BranchItems(ring.Group);
        if(ring.Depth>8)
            throw new InvalidOperationException(
                "Deep Excel groups require opening Workspace for editing.");
        ring.Start=Math.Clamp(ring.Start,0,Math.Max(0,all.Count-PerPage));
        ring.Items.Clear();
        ring.Items.AddRange(all.Skip(ring.Start).Take(PerPage));
        ring.Surface.Children.Clear();ring.Zones.Clear();ring.Wedges.Clear();ring.VisualsV380.Clear();
        ring.Hovered=-1;
        int count=ring.Items.Count;
        if(count==0)return;
        // Standard shared five-slot sweep and relative WPF Popup positioning.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ring.Angle,ring.ParentSweep).Half;
        double centre=DeskSpaceSubArcGeometryV48.FitAngle(
            owner,ring.Depth,ring.Angle,half);
        ring.StartAngle=centre-half;
        ring.StepAngle=2*half/count;
        var shapes=new List<Geometry>();
        for(int i=0;i<count;i++)
        {
            double a0=ring.StartAngle+i*ring.StepAngle;
            double a1=ring.StartAngle+(i+1)*ring.StepAngle;
            double g=(a1-a0)>gapInset*2.5?gapInset:0;
            shapes.Add(DeskSpaceSubArcGeometryV48.Sector(ring.Depth,a0+g,a1-g));
        }
        ring.Bounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        ring.OriginX=ring.Bounds.Left;ring.OriginY=ring.Bounds.Top;
        ring.Surface.Width=ring.Bounds.Width+2*margin;
        ring.Surface.Height=ring.Bounds.Height+2*margin;
        for(int i=0;i<count;i++)
        {
            var item=ring.Items[i];
            bool isAction=item.Command!="mapping" && item.Command!="format";
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,ring.Depth+2,i,isAction,false,false);
            var hit=DeskSpaceSubArcGeometryV48.Clip(shapes[i],ring.Bounds);
            var wedge=new System.Windows.Shapes.Path{
                Data=hit,Stroke=vis.Stroke,StrokeThickness=vis.StrokeThickness,
                Fill=vis.Fill,Effect=vis.Effect,
                Cursor=Cursors.Hand,ToolTip=item.Label
            };
            ring.Surface.Children.Add(wedge);
            ring.Wedges.Add(wedge);ring.Zones.Add(hit);
            double middle=ring.StartAngle+(i+.5)*ring.StepAngle;
            var p=DeskSpaceSubArcGeometryV48.Polar(
                (DeskSpaceSubArcGeometryV48.Inner(ring.Depth)+
                 DeskSpaceSubArcGeometryV48.Outer(ring.Depth))/2,middle);
            FontWeight fw=isAction?FontWeights.SemiBold:FontWeights.Normal;
            var label=new TextBlock{
                Text=item.Label,Width=80,FontSize=10,FontWeight=fw,
                Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=item.Label,IsHitTestVisible=false
            };
            Canvas.SetLeft(label,DeskSpaceSubArcGeometryV48.CX+p.X-
                ring.Bounds.Left+margin-40);
            Canvas.SetTop(label,DeskSpaceSubArcGeometryV48.CY+p.Y-
                ring.Bounds.Top+margin-9);
            ring.Surface.Children.Add(label);

            bool isTrunc=DeskSpaceDonutThemeV380.SubDonutCalloutPillV380.IsLabelTruncated(
                item.Label,80,20,10,fw,wraps:false);
            var outerPt=DeskSpaceSubArcGeometryV48.Polar(DeskSpaceSubArcGeometryV48.Outer(ring.Depth),middle);
            Point outerAnchor=new(DeskSpaceSubArcGeometryV48.CX+outerPt.X-ring.Bounds.Left+margin,
                                  DeskSpaceSubArcGeometryV48.CY+outerPt.Y-ring.Bounds.Top+margin);
            string? badge=item.Command=="format"?"FORMAT":(item.Command=="mapping"?"TEMPLATE":null);
            ring.VisualsV380.Add((new List<UIElement>{wedge,label},label,isTrunc,item.Label,badge,middle,outerAnchor,isAction));
        }
'@ 'DeskSpaceExcelWheelV42 ExpandRing AnimateOpen and RenderRing theme'
Swap-R380 @'
        ring.Hovered=slot;
        for(int i=0;i<ring.Wedges.Count;i++)
            ring.Wedges[i].Fill=i==slot?Selected:
                (ring.Items[i].Command=="mapping"?Idle:Accent);
        if(slot<0)return;
'@ @'
        ring.Hovered=slot;
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int i=0;i<ring.Wedges.Count;i++)
        {
            bool hov=i==slot;
            bool isAction=i<ring.VisualsV380.Count
                ?ring.VisualsV380[i].IsAction
                :(ring.Items[i].Command!="mapping" && ring.Items[i].Command!="format");
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,ring.Depth+2,i,isAction,false,hov);
            ring.Wedges[i].Fill=vis.Fill;
            ring.Wedges[i].Stroke=vis.Stroke;
            ring.Wedges[i].StrokeThickness=vis.StrokeThickness;
            ring.Wedges[i].Effect=vis.Effect;
            if(i<ring.VisualsV380.Count)
            {
                var rv=ring.VisualsV380[i];
                rv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
                DeskSpaceDonutThemeV380.ApplyHoverLift(rv.LinkedVisuals,hov,rv.MidAngle,themeApp);
            }
        }
        if(slot>=0 && slot<ring.VisualsV380.Count && ring.VisualsV380[slot].IsTruncated)
            calloutPill.Show(ring.Surface,ring.VisualsV380[slot].OuterAnchor,ring.VisualsV380[slot].MidAngle,ring.VisualsV380[slot].FullName,ring.VisualsV380[slot].Badge);
        else
            calloutPill.Hide();
        if(slot<0)return;
'@ 'DeskSpaceExcelWheelV42 HoverRing theme lift and callout pill'
Swap-R380 @'
            Position();
            arc.IsOpen=true;
            pointerTimer.Start();
'@ @'
            Position();
            arc.IsOpen=true;
            if(!rootBounds.IsEmpty)
            {
                DeskSpaceDonutThemeV380.AnimateOpen(
                    canvas,
                    sectors.Cast<FrameworkElement>().ToArray(),
                    DeskSpaceSubArcGeometryV48.CX-rootBounds.Left+10,
                    DeskSpaceSubArcGeometryV48.CY-rootBounds.Top+10,
                    ArcCenter,isSubArc:true);
            }
            pointerTimer.Start();
'@ 'DeskSpaceExcelWheelV42 Show AnimateOpen'
Swap-R380 @'
    public void HideArc()
    {
        middleScroll.Stop();
'@ @'
    public void HideArc()
    {
        calloutPill.Hide();
        middleScroll.Stop();
'@ 'DeskSpaceExcelWheelV42 HideArc hide pill'
Swap-R380 @'
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
        // Shared standard root arc, centered on Excel's existing outer wedge.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ArcCenter).Half;
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
'@ @'
    private void Render()
    {
        calloutPill.Hide();
        DeskSpaceAppearanceV148.ReloadIfChanged();
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        double gapInset=DeskSpaceDonutThemeV380.GetSubArcGapInset(themeApp);
        drillTimer.Stop();drillSlot=-1;drillDepth=-1;
        CloseFrom(1);
        canvas.Children.Clear();sectors.Clear();hitZones.Clear();displayed.Clear();rootWedgeVisualsV380.Clear();
        hovered=-1;
        var all=Entries();
        start=Math.Clamp(start,0,Math.Max(0,all.Count-PerPage));
        displayed.AddRange(all.Skip(start).Take(PerPage));
        int count=displayed.Count;
        if(count==0)return;
        // Shared standard root arc, centered on Excel's existing outer wedge.
        double half=DeskSpaceSubDonutStandardV62.Plan(
            count,0,ArcCenter).Half;
        rootStartAngle=ArcCenter-half;
        rootStep=2*half/count;
        var shapes=new List<Geometry>();
        for(int i=0;i<count;i++)
        {
            double a0=rootStartAngle+i*rootStep;
            double a1=rootStartAngle+(i+1)*rootStep;
            double g=(a1-a0)>gapInset*2.5?gapInset:0;
            shapes.Add(DeskSpaceSubArcGeometryV48.Sector(0,a0+g,a1-g));
        }
        rootBounds=DeskSpaceSubArcGeometryV48.Bounds(shapes);
        const double margin=10;
        canvas.Width=rootBounds.Width+2*margin;
        canvas.Height=rootBounds.Height+2*margin;
        for(int i=0;i<count;i++)
        {
            var item=displayed[i];
            bool isAction=item.Command!="mapping" && item.Command!="format";
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,2,i,isAction,false,false);
            var hit=DeskSpaceSubArcGeometryV48.Clip(shapes[i],rootBounds);
            var wedge=new System.Windows.Shapes.Path{
                Data=hit,Stroke=vis.Stroke,StrokeThickness=vis.StrokeThickness,
                Fill=vis.Fill,Effect=vis.Effect,
                Tag=i,Cursor=Cursors.Hand,ToolTip=item.Label
            };
            canvas.Children.Add(wedge);sectors.Add(wedge);
            hitZones.Add((i,item.Id,hit));
            double angle=rootStartAngle+(i+.5)*rootStep;
            var p=DeskSpaceSubArcGeometryV48.Polar(
                (DeskSpaceSubArcGeometryV48.Inner(0)+
                 DeskSpaceSubArcGeometryV48.Outer(0))/2,angle);
            FontWeight fw=isAction?FontWeights.SemiBold:FontWeights.Normal;
            var label=new TextBlock{
                Text=item.Label,Width=80,FontSize=10,FontWeight=fw,
                Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=item.Label,IsHitTestVisible=false
            };
            Canvas.SetLeft(label,DeskSpaceSubArcGeometryV48.CX+
                p.X-rootBounds.Left+margin-40);
            Canvas.SetTop(label,DeskSpaceSubArcGeometryV48.CY+
                p.Y-rootBounds.Top+margin-9);
            canvas.Children.Add(label);

            bool isTrunc=DeskSpaceDonutThemeV380.SubDonutCalloutPillV380.IsLabelTruncated(
                item.Label,80,20,10,fw,wraps:false);
            var outerPt=DeskSpaceSubArcGeometryV48.Polar(DeskSpaceSubArcGeometryV48.Outer(0),angle);
            Point outerAnchor=new(DeskSpaceSubArcGeometryV48.CX+outerPt.X-rootBounds.Left+margin,
                                  DeskSpaceSubArcGeometryV48.CY+outerPt.Y-rootBounds.Top+margin);
            string? badge=item.Command=="format"?"FORMAT":(item.Command=="mapping"?"TEMPLATE":null);
            rootWedgeVisualsV380.Add((new List<UIElement>{wedge,label},label,isTrunc,item.Label,badge,angle,outerAnchor,isAction));
        }
'@ 'DeskSpaceExcelWheelV42 Render themed and callout pill prep'
Swap-R380 @'
        hovered=slot;
        for(int i=0;i<sectors.Count;i++)
        {
            bool command=displayed[i].Command!="mapping";
            sectors[i].Fill=i==slot?Selected:(command?Accent:Idle);
        }
        if(slot<0||slot>=displayed.Count)return;
'@ @'
        hovered=slot;
        var themeApp=DeskSpaceAppearanceV148.Current;
        bool hc=SystemParameters.HighContrast;
        for(int i=0;i<sectors.Count;i++)
        {
            bool hov=i==slot;
            bool isAction=i<rootWedgeVisualsV380.Count
                ?rootWedgeVisualsV380[i].IsAction
                :(displayed[i].Command!="mapping" && displayed[i].Command!="format");
            var vis=DeskSpaceDonutThemeV380.ResolveWedge(themeApp,hc,2,i,isAction,false,hov);
            sectors[i].Fill=vis.Fill;
            sectors[i].Stroke=vis.Stroke;
            sectors[i].StrokeThickness=vis.StrokeThickness;
            sectors[i].Effect=vis.Effect;
            if(i<rootWedgeVisualsV380.Count)
            {
                var rv=rootWedgeVisualsV380[i];
                rv.Label.Foreground=hc?SystemColors.WindowTextBrush:vis.TextBrush;
                DeskSpaceDonutThemeV380.ApplyHoverLift(rv.LinkedVisuals,hov,rv.MidAngle,themeApp);
            }
        }
        if(slot>=0 && slot<rootWedgeVisualsV380.Count && rootWedgeVisualsV380[slot].IsTruncated)
            calloutPill.Show(canvas,rootWedgeVisualsV380[slot].OuterAnchor,rootWedgeVisualsV380[slot].MidAngle,rootWedgeVisualsV380[slot].FullName,rootWedgeVisualsV380[slot].Badge);
        else
            calloutPill.Hide();
        if(slot<0)
        {
            foreach(var r in childRings.ToArray())
                if(r.Hovered>=0)HoverRing(r,new Point(-9999,-9999));
        }
        if(slot<0||slot>=displayed.Count)return;
'@ 'DeskSpaceExcelWheelV42 Hover root theme lift and callout pill'
Swap-R380 @'
        CloseFrom(1);
        arc.IsOpen=false;shelf.IsOpen=false;
'@ @'
        CloseFrom(1);
        calloutPill.Dispose();
        arc.IsOpen=false;shelf.IsOpen=false;
'@ 'DeskSpaceExcelWheelV42 Dispose calloutPill'
Finish-Patch $excelWheelPath

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
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpaceModernHubV380.cs") -or
    !(Test-Path -LiteralPath "$dstRoot/ModernV380/DeskSpaceDonutThemeV380.cs")) {
    throw 'R380 missing copied ModernV380 or DeskSpaceDesignV283 files in target tree'
}

Write-Host "R380 PASS: Copied DeskSpaceDesignV283 + $($modernFiles.Count) ModernV380 modules and wired all hooks safely."
