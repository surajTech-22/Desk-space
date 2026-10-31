using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DeskSpace;

public sealed class WidgetSettingsV3
{
    public string CenterMode { get; set; } = "Clock";
    public bool ShowAtLaunch { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool AutoHideFullscreen { get; set; } = true;
    public bool AlwaysVisible { get; set; }
    public bool ShowWeather { get; set; } = true;
    public string WeatherCity { get; set; } = "Bengaluru";
    public bool Expanded { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public List<string> PinnedApps { get; set; } = new();
    private static string Filename => Path.Combine(WorkspaceStore.Root, "widget-v3.json");
    public static WidgetSettingsV3 Load()
    {
        try { return JsonSerializer.Deserialize<WidgetSettingsV3>(File.ReadAllText(Filename)) ?? new(); }
        catch { return new WidgetSettingsV3(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp = Filename + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, Filename, true);
    }
    public void ApplyWindowsStartup()
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (StartWithWindows)
            run?.SetValue("DeskSpaceWidget", $"\"{Environment.ProcessPath}\" --widget-host");
        else run?.DeleteValue("DeskSpaceWidget", false);
    }
}

public static class WidgetHostLauncher
{
    // Opens the existing widget's Search view. No second Search window.
    public static void OpenSearch()
    {
        EnsureRunning(force:true);
        _ = Task.Run(async () =>
        {
            for(int i=0;i<5;i++)
            {
                try
                {
                    using var client=new NamedPipeClientStream(".","DeskSpace.Widget.V3",PipeDirection.Out);
                    await client.ConnectAsync(700);
                    client.WriteByte(2);await client.FlushAsync();
                    return;
                }
                catch(TimeoutException){}
                catch(IOException){}
                await Task.Delay(250);
            }
        });
    }
    // Open Excel mapping inside the existing widget from the main application.
    public static void OpenExcel()
    {
        EnsureRunning(force:true);
        _=Task.Run(async()=>{
            for(int i=0;i<6;i++)
            {
                try{
                    using var client=new NamedPipeClientStream(
                        ".","DeskSpace.Widget.V3",PipeDirection.Out);
                    await client.ConnectAsync(700);
                    client.WriteByte(3);await client.FlushAsync();
                    return;
                }
                catch(TimeoutException){}
                catch(IOException){}
                await Task.Delay(250);
            }
        });
    }
    public static void EnsureRunning(bool force = false)
    {
        if (!force && !WidgetSettingsV3.Load().ShowAtLaunch) return;
        try
        {
            using var client = new NamedPipeClientStream(".", "DeskSpace.Widget.V3", PipeDirection.Out);
            client.Connect(100);
            client.WriteByte(1); client.Flush();
            return;
        }
        catch (TimeoutException) { }
        catch (IOException) { }
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
            Process.Start(new ProcessStartInfo(exe, "--widget-host") { UseShellExecute = false });
    }
}

public sealed class WidgetShellWindow : Window
{
    private readonly WidgetSettingsV3 prefs = WidgetSettingsV3.Load();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DeskSpaceDueShelfV74? dueShelf;
    private DeskSpaceCommandShortcutV85? commandShortcut;
    // Scale the ORIGINAL 292/420-DIP viewbox as a single unit, preserving
    // precise donut hit areas and all nested arc anchors.
    private static double CompactDiameter=>
        116*DeskSpaceAppearanceV148.Current.Scale;
    private static double OpenDiameter=>
        408*DeskSpaceAppearanceV148.Current.Scale;
    private readonly Canvas canvas = new() { Width = 292, Height = 292 };
    private bool ringExpanded;
    // Holding the compact centre opens the ring. Exiting the OUTER ring starts
    // window dragging; it never activates a command. Commands run only if the
    // user releases inside a selected sector.
    private readonly DispatcherTimer holdTimer = new() { Interval = TimeSpan.FromMilliseconds(230) };
    private bool holdPending;
    private bool ringGesture;
    private bool draggingGesture;
    private DateTime rightClickOpenedUntil = DateTime.MinValue;
    private bool shortcutMoveInProgress;
    private int highlightedSector = -1;
    private readonly List<System.Windows.Shapes.Path> ringPaths = new();
    private bool dockedManually;
    private Ellipse? clockFace;
    private readonly TextBlock content = new() { TextAlignment = TextAlignment.Center, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly Forms.NotifyIcon tray;
    private readonly Window edgeHandle;
    private readonly WorkspaceStore store = new();
    private Workspace workspace = new();
    private DateTime lastDataLoad = DateTime.MinValue;
    private DateTime lastFullscreenPoll = DateTime.MinValue;
    private bool previousFullscreen;
    private DateTime lastWeatherPoll = DateTime.MinValue;
    private Ellipse? weatherHalo;
    private void AddWeatherScene(double x,double y,double diameter)
    {
        if(activeModule!=null || hiddenAtEdge || prefs.CenterMode!="Clock" || !prefs.ShowWeather)
            return;
        var reading=DeskSpaceWeather.Latest;
        if(reading==null || !reading.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return;
        weatherScene=new DeskSpaceWeatherSceneV11(reading,diameter);
        Canvas.SetLeft(weatherScene,x);
        Canvas.SetTop(weatherScene,y);
        canvas.Children.Add(weatherScene);
    }
    private DateTime revealedUntil = DateTime.MinValue;
    private bool shutdown;
    private readonly System.Threading.CancellationTokenSource
        showRequestCancel=new();
    private bool hiddenAtEdge;
    private Window? detail;
    private readonly RecentFilesService recent = new();
    private Brush light=>DeskSpaceAppearanceV148.LightBrush;
    private Brush accent=>DeskSpaceAppearanceV148.AccentBrush;
    // The inner ring holds direct tools. The existing OUTER ring holds
    // collection/list modules that may open contextual partial sub-arcs.
    // Two new modules each retain a dedicated usable hit sector.
    // Existing six inner and six outer modules remain present, in the
    // same order. Excel is the seventh inner sector; Excel Clipboard
    // is the seventh outer sector. No duplicate/hidden module.
    private static readonly string[] Segments = {
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings",
        "Information", "Notes", "Reminders", "Clipboard", "Applications", "Excel Functions"
    };
    private static readonly string[] SegmentIcons = {
        "⌕", "▦", "⊞", "◷", "☀", "⚙",
        "◆", "✎", "◉", "▣", "◈", "▥"
    };
    private DeskSpaceModuleV10? activeModule;
    private DeskSpaceInformationV36? informationOverlay;
    private DeskSpaceSearchV11? activeSearch;
    private DeskSpaceExcelV16? activeExcel;
    private readonly DeskSpaceMediaV18 media=new();
    // Playback is rendered in a separately anchored Popup, not over Clock.

    private DeskSpaceDropShelfV15? attachedShelf;
    private DeskSpaceExcelClipboardV39? excelClipboardShelf;
    private DeskSpaceExcelWheelV42? excelWheel;
    private DeskSpaceApplicationsV55? appsWheel;
    private DeskSpaceInlineV36? activeInline;
    private DeskSpaceWeatherSceneV11? weatherScene;
    private object? previousSurface;
    private string previousCenter = "Clock";
    private double restoreLeft, restoreTop;
    private static readonly HashSet<string> CenterAllowed = new(StringComparer.OrdinalIgnoreCase) { "Clock", "Date", "Calendar", "Notes", "Reminders", "Tasks" };

    public WidgetShellWindow()
    {
        // One shared mouse-wheel routing implementation covers nested
        // panels in the main Workspace AND the independent floating circle.
        DeskSpaceSmoothScrollV41.Enable();
        Title = "DeskSpace Live Donut";
        Width = Height = CompactDiameter; AllowsTransparency = true; Background = Brushes.Transparent;
        WindowStyle = WindowStyle.None; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowActivated = false; WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => DeskSpaceWindowBehavior.Apply(this);
        MouseLeave += (_, _) =>
        {
            // Moving away without holding must close the opened donut.
            // With a button held, the existing outer-boundary gesture takes
            // precedence and moves the widget without choosing any module.
            if (ringExpanded && informationOverlay?.IsArcOpen!=true && !ringGesture && !draggingGesture &&
                Mouse.LeftButton != MouseButtonState.Pressed &&
                Mouse.RightButton != MouseButtonState.Pressed &&
                DateTime.UtcNow > rightClickOpenedUntil && !keyboardDonutModeV207) CollapseRing();
        };
        var area = SystemParameters.WorkArea;
        var old = WidgetPreferences.Load();
        Left = prefs.Left ?? old.Left ?? Math.Max(area.Left, area.Right - Width - 36);
        Top = prefs.Top ?? old.Top ?? area.Top + 70;
        // The ring is NEVER open by default, even when upgrading from R3 preferences.
        ringExpanded = false;
        // A replica's physical monitor is assigned after construction.
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))KeepOnVisibleDesktop();
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        DeskSpaceAppearanceV148.Apply(this);
        // Register only the primary host. Satellite windows have no secondary
        // update channel or monitor watchers.
        Loaded+=(_,_)=>{
            // The only primary widget owns short-crash recovery. Never create
            // satellites while recovery mode is active; preserve one tray,
            // all saved settings, existing held files and the normal updater.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceStartupRecoveryV183.Start(this);
            if(!DeskSpaceStartupRecoveryV183.SafeMode)
                DeskSpaceMultiMonitorV151.Register(this);
            // The already-loaded primary acknowledges only the matching
            // local supervised update transaction/version, no content read.
            // A sustained healthy WPF host must acknowledge the installer,
            // not a single Loaded frame that may immediately crash.
            DeskSpacePostInstallReceiptV185.ReadyAfterStabilityV224(this);
            DeskSpaceGracefulExitV230.Attach(this);
        };
        BuildCompact();
        tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            Environment.ProcessPath ?? "") ?? System.Drawing.SystemIcons.Application, Text = "DeskSpace Live Widget",
            Visible = !DeskSpaceMultiMonitorV151.ConstructingSatellite };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show widget", null, (_, _) => Dispatcher.Invoke(Reveal));
        menu.Items.Add("Expand / collapse ring", null, (_, _) => Dispatcher.Invoke(() => { Reveal(); ToggleRing(); }));
        menu.Items.Add("Navigate donut with keyboard (arrows + Enter)...", null, (_, _) =>
            Dispatcher.BeginInvoke(new Action(BeginKeyboardDonutV207)));
        menu.Items.Add("Choose centre display", null, (_, _) => Dispatcher.Invoke(ChooseCenter));
        menu.Items.Add("Expand selected information", null, (_, _) => Dispatcher.Invoke(() => ToggleDetails()));
        menu.Items.Add("Hide at screen edge", null, (_, _) => Dispatcher.Invoke(() => DockToEdge(manual: true)));
        menu.Items.Add("Check for updates", null, (_, _) => Dispatcher.Invoke(async () => await DeskSpaceUpdateCheckV42.CheckAsync(this)));
        menu.Items.Add("Universal Search", null, (_, _) => Dispatcher.Invoke(OpenSearch));
        menu.Items.Add("Recent files", null, (_, _) => Dispatcher.Invoke(OpenRecent));
        menu.Items.Add("Excel Functions · Clipboard / Information",null,(_,_)=>Dispatcher.Invoke(
            OpenExcelArc));
        menu.Items.Add("Watch folder for saved files...",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceDropShelfV15.SelectWatchedFolder));
        // Diagnostics are opt-in and generated ONLY when explicitly clicked.
        DeskSpaceDiagnosticsV161.Install();
        // The primary widget alone subscribes to OS clipboard-change
        // notifications. No clipboard content is read, stored or uploaded.
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            _=DeskSpaceClipboardSignalV175.Watch(this);
        menu.Items.Add("Component status (local only)...",null,(_,_)=>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceComponentHealthV175.Show(this,tray.Visible))));
        menu.Items.Add("Clipboard: convert text / Markdown / JSON...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceClipboardTransformsV177.Show(this))));
        menu.Items.Add("Window layout profiles...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceWindowLayoutsV179.Show(this))));
        menu.Items.Add("Action palette (Ctrl+K in widget)...",null,(_,_) =>
            Dispatcher.BeginInvoke(new Action(()=>
                DeskSpaceActionPaletteV197.Show(this,tray.Visible))));
        // Focused-widget shortcut only; never a global hotkey that can
        // interfere with Excel or another application on a different monitor.
        this.PreviewKeyDown+=(_,e)=>{
            if(e.Key==System.Windows.Input.Key.K &&
               (System.Windows.Input.Keyboard.Modifiers &
                System.Windows.Input.ModifierKeys.Control)!=0)
            {
                DeskSpaceActionPaletteV197.Show(this,tray.Visible);
                e.Handled=true;
            }
        };
        PreviewKeyDown+=KeyboardDonutKeyV207;
        Deactivated+=(_,_)=>{
            if(!keyboardDonutModeV207)return;
            EndKeyboardDonutV207();
            if(ringExpanded && activeModule==null)CollapseRing();
        };
        menu.Items.Add("Export privacy-safe diagnostics...",null,(_,_)=>Dispatcher.Invoke(()=>{
            try
            {
                var archive=DeskSpaceDiagnosticsV161.ExportToDownloads();
                MessageBox.Show(this,"Diagnostics ZIP saved to Downloads:"+
                    Environment.NewLine+System.IO.Path.GetFileName(archive)+
                    Environment.NewLine+"No private notes, passwords, clipboard or Excel data included.",
                    "DeskSpace diagnostics",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception error)
            {
                MessageBox.Show(this,"Diagnostics could not be exported ("+
                    error.GetType().Name+").","DeskSpace",
                    MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }));
        DeskSpaceWorkspaceProfilesV85.AddMenu(menu,this,prefs,()=>{
            if(activeModule==null && !ringExpanded)BuildCompact();
            else UpdateCenter();
        });
        menu.Items.Add("Donut toolbar, themes & presets",null,(_,_)=>Dispatcher.Invoke(
            ()=>DeskSpaceSettingsStudioV148.Show(this,prefs,
                RefreshAppearanceV148,
                ()=>new WidgetOptionsWindow(prefs,()=>{
                    prefs.Save();prefs.ApplyWindowsStartup();
                }).Show())));
        var deckMenu=new Forms.ToolStripMenuItem("Deck Visibility");
        deckMenu.DropDownOpening+=(_,_)=>{
            deckMenu.DropDownItems.Clear();
            bool shown=DeskSpaceDeckVisibilityV89.Visible;
            deckMenu.DropDownItems.Add(shown?"Hide deck":"Show deck",
                null,(_,_)=>Dispatcher.Invoke(()=>{
                    if(!DeskSpaceDeckVisibilityV89.TrySet(!shown,out var problem))
                        MessageBox.Show("Cannot update deck visibility: "+problem,
                            "DeskSpace");
                }));
        };
        menu.Items.Add(deckMenu);
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
            null,(_,_)=>Dispatcher.Invoke(()=>OpenInWidget("Search")));
        menu.Items.Add("Excel Clipboard shelf",null,(_,_)=>Dispatcher.Invoke(
            DeskSpaceExcelClipboardV39.ShowActive));
        menu.Items.Add("About / Version", null, (_, _) => Dispatcher.Invoke(ShowAbout));
        menu.Items.Add("Open DeskSpace", null, (_, _) => Dispatcher.Invoke(OpenMain));
        menu.Items.Add("Exit widget", null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Reveal);
        edgeHandle = CreateEdgeHandle();
        holdTimer.Tick += (_, _) =>
        {
            holdTimer.Stop();
            if (!holdPending || Mouse.LeftButton != MouseButtonState.Pressed) return;
            holdPending = false;
            ringGesture = true;
            if (!ringExpanded) ToggleRing();
            UpdateHighlightedSector(Mouse.GetPosition(this));
        };
        PreviewMouseRightButtonDown += OnShortcutRightMouseDown;
        PreviewMouseRightButtonUp += (_,e) => { if(activeModule==null)e.Handled=true; };
        PreviewMouseLeftButtonDown += OnGestureDown;
        PreviewMouseMove += OnGestureMove;
        PreviewMouseLeftButtonUp += OnGestureUp;
        LostMouseCapture += (_, _) =>
        {
            if (!draggingGesture && !ringGesture) { holdPending = false; holdTimer.Stop(); }
        };
        Loaded += (_, _) =>
        {
            DeskSpaceWeather.Updated += OnWeatherUpdated;
            attachedShelf ??= new DeskSpaceDropShelfV15(this);
            attachedShelf.Enable();
            // Attach to the existing primary icon when the widget's
            // startup handler enables the Deck; this is already on Loaded.
            // Keep a single tray icon, regardless of monitor count.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                _=DeskSpaceTrayRecoveryV166.Watch(this,tray);
            // First use registers Windows logon startup. Later the user
            // can turn it off in Settings; on every launch refresh the
            // Run entry to the current installed executable path.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceStartupV82.EnableOnFirstLaunch(prefs);
            // Ctrl+Alt+Space opens the EXISTING universal inline Search.
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            {
                commandShortcut ??= new DeskSpaceCommandShortcutV85(
                    this,()=>OpenInWidget("Search"));
                DeskSpaceSearchIndexV13.WarmStart();
            }
            // Pre-create and pre-read both expensive child modules. Hover
            // only renders; the original persisted files remain unchanged.
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
            // Independent Excel-only clipboard, not part of the Files shelf.
            excelClipboardShelf ??= new DeskSpaceExcelClipboardV39(this);
            excelClipboardShelf.Enable();
            media.VisibilityChanged+=OnMediaVisibility;
            media.Start();
            DeskSpaceUpdateProgress.Changed += OnUpdateProgress;
            if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
        };
        Closing += (_,_)=>{
            showRequestCancel.Cancel();
            media.VisibilityChanged-=OnMediaVisibility;
            media.Dispose();
            activeModule?.Dispose(); informationOverlay?.Dispose();
            attachedShelf?.Dispose();
            excelClipboardShelf?.Dispose();
            excelWheel?.Dispose();
            appsWheel?.Dispose();
        };
        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated;
            DeskSpaceUpdateProgress.Changed -= OnUpdateProgress; shutdown = true; holdTimer.Stop(); timer.Stop();
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))SavePosition();
            tray.Visible = false; tray.Dispose(); edgeHandle.Close(); detail?.Close(); recent.Dispose(); };
        timer.Tick += (_, _) => Tick(); timer.Start();
        // The scheduled shelf has its own system-clock checks, including
        // while a module editor is open, and follows this widget's location.
        dueShelf=new DeskSpaceDueShelfV74(this);
        Loaded+=(_,_)=>{
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))dueShelf?.Start();
        };
        Closing+=(_,_)=>{dueShelf?.Dispose();dueShelf=null;};
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))recent.Start();
        Tick();
        Loaded += (_, _) =>{
            if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
                DeskSpaceUpdater.StartAutomaticChecks(this);
        };
        if(!DeskSpaceMultiMonitorV151.IsSatellite(this))
            _ = ListenForShowRequests();
    }
    private async Task ListenForShowRequests()
    {
        while (!shutdown)
        {
            try
            {
                using var server = new NamedPipeServerStream("DeskSpace.Widget.V3", PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(showRequestCancel.Token).ConfigureAwait(false);
                if (shutdown) break;
                var command=server.ReadByte();
                if(command==1)await Dispatcher.InvokeAsync(Reveal);
                else if(command==2)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Search");
                });
                else if(command==3)await Dispatcher.InvokeAsync(()=>{
                    Reveal();
                    OpenInWidget("Excel");
                });
            }
            catch (System.OperationCanceledException) { break; }
            catch (IOException) {
                if(shutdown)break;
                await Task.Delay(350).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { break; }
        }
    }
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        // Ignore right clicks whose physical HWND belongs to the other widget.
        if(!DeskSpaceMonitorInputV158.OwnsRightClick(this))return;
        if(activeModule!=null)return;
        LeaveMediaMode();
        if(Mouse.LeftButton==MouseButtonState.Pressed)
        {
            e.Handled=true;
            StartShortcutDrag();
            return;
        }
        e.Handled=true;
        if(hiddenAtEdge)Reveal();
        if(ringExpanded)CollapseRing();
        else
        {
            ToggleRing();
            rightClickOpenedUntil=DateTime.UtcNow.AddMilliseconds(850);
        }
    }

    private void StartShortcutDrag()
    {
        if(activeModule!=null || shortcutMoveInProgress ||
            Mouse.LeftButton!=MouseButtonState.Pressed)return;
        shortcutMoveInProgress=true;
        holdTimer.Stop();
        holdPending=false;
        ringGesture=false;
        draggingGesture=true;
        highlightedSector=-1;
        try
        {
            if(IsMouseCaptured)ReleaseMouseCapture();
            CollapseRing();
            // Native Windows dragging remains active until left-mouse release.
            // Moving outside the ring without either shortcut NEVER drags.
            DragMove();
        }
        catch(InvalidOperationException){}
        finally
        {
            // Do not interpret monitor seams as magnetic edges. A completed
            // drag always leaves the widget where the user placed it,
            // constrained only by its OWN monitor's native fence.
            KeepOnVisibleDesktop();
            SavePosition();
            shortcutMoveInProgress=false;
            draggingGesture=false;
        }
    }
    private void OnGestureDown(object sender, MouseButtonEventArgs e)
    {
        if(activeModule!=null || hiddenAtEdge || e.ChangedButton!=MouseButton.Left)return;
        if((Keyboard.Modifiers & ModifierKeys.Shift)!=0 ||
            Mouse.RightButton==MouseButtonState.Pressed)
        {
            e.Handled=true;
            StartShortcutDrag();
            return;
        }
        if(ringExpanded && SectorAt(e.GetPosition(this))>=0)
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=true;
            draggingGesture=false;
            CaptureMouse();
            e.Handled=true;
            return;
        }
        if(!CanDragFrom(e))return;
        // Ring visible from the tray: pressing the centre immediately enters
        // the same selection/drag gesture, no second long-press required.
        holdPending = !ringExpanded;
        ringGesture = ringExpanded;
        draggingGesture = false;
        if (holdPending) holdTimer.Start();
        CaptureMouse();
        e.Handled = true;
    }

    private void OnGestureMove(object sender,MouseEventArgs e)
    {
        if(activeModule!=null || draggingGesture || shortcutMoveInProgress)return;
        var p=e.GetPosition(this);
        if(Mouse.LeftButton!=MouseButtonState.Pressed)
        {
            if(ringExpanded)
            {
                // Ordinary pointer motion selects the parent donut segment.
                // No click, hold or mouse capture is needed to open children.
                UpdateHighlightedSector(p);
                if(informationOverlay?.IsArcOpen==true)return;
                if(!ringGesture &&
                   Mouse.RightButton!=MouseButtonState.Pressed &&
                   DateTime.UtcNow>rightClickOpenedUntil)
                {
                    double radius=Math.Min(Width,Height)*196.0/420.0;
                    if((p-new Point(Width/2,Height/2)).Length>radius+4)
                        CollapseRing();
                }
            }
            return;
        }
        if(!ringGesture)return;
        // Parent WPF window owns CaptureMouse() while dragging. Convert
        // parent coordinates to screen coordinates and let the child popup
        // use PointFromScreen() for its real wedge geometry.
        if(informationOverlay?.IsArcOpen==true &&
           informationOverlay.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }
        if(appsWheel?.IsArcOpen==true &&
           appsWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }
        if(excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(PointToScreen(p),false))
        {
            e.Handled=true;
            return;
        }        informationOverlay?.EndCapturedDrag();
        UpdateHighlightedSector(p);
        e.Handled=true;
    }
    private void OnGestureUp(object sender, MouseButtonEventArgs e)
    {
        if(activeModule!=null || e.ChangedButton != MouseButton.Left || (!holdPending && !ringGesture)) return;
        if(ringGesture && appsWheel?.IsArcOpen==true &&
           appsWheel.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return;
        }
        if(ringGesture && excelWheel?.IsArcOpen==true &&
           excelWheel.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return;
        }        if(ringGesture && informationOverlay?.IsArcOpen==true &&
           informationOverlay.TryCapturedDragAtScreen(
               PointToScreen(e.GetPosition(this)),true))
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=false;
            highlightedSector=-1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
            return; // keep parent + child donut visible
        }
        if(ringGesture)
        {
            int addSector=QuickAddHitAt(e.GetPosition(this));
            if(addSector>=0)
            {
                holdTimer.Stop();
                holdPending=false;ringGesture=false;highlightedSector=-1;
                if(IsMouseCaptured)ReleaseMouseCapture();
                string target=Segments[addSector]=="Tasks"?"Reminders":Segments[addSector];
                CollapseRing();
                Dispatcher.BeginInvoke(new Action(()=>OpenInWidget(target,true)));
                e.Handled=true;return;
            }
        }
        informationOverlay?.EndCapturedDrag();
        holdTimer.Stop();
        holdPending = false;
        bool hadRingGesture = ringGesture;
        ringGesture = false;
        int selection = hadRingGesture ? SectorAt(e.GetPosition(this)) : -1;
        highlightedSector = -1;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (selection >= 0)
            Dispatcher.BeginInvoke(new Action(() => ClickSegment(Segments[selection])));
        CollapseRing();
        e.Handled = true;
    }

    // The media view occupies exactly the compact circle rather than
    // creating another popup or taskbar app. Search/editors have priority.
    // The original circle remains unchanged. Media floats BELOW it.
    // The header is the actual current module text, not its module name.
    private void OnMediaVisibility(bool active)
    {
        if(!Dispatcher.CheckAccess()){
            Dispatcher.BeginInvoke(new Action(()=>OnMediaVisibility(active)));
            return;
        }
        if(!active || activeModule!=null || ringExpanded || hiddenAtEdge){
            LeaveMediaMode();
            return;
        }
        media.ShowBelow(this,()=>content.Text);
        attachedShelf?.SetMediaOffset(media.ShelfOffset);
    }
    private void LeaveMediaMode()
    {
        media.CloseBelow();
        attachedShelf?.SetMediaOffset(5);
    }
    private void ResumeMediaMode()
    {
        if(media.Visible && activeModule==null && !ringExpanded && !hiddenAtEdge)
            OnMediaVisibility(true);
    }
    private void OpenInWidget(string name,bool quickAddRequested=false)
    {
        if(name=="Information"){OpenInformationArc();return;}
        informationOverlay?.Hide();
        if(closingMorph)CompleteModuleReturn();
        LeaveMediaMode();
        if(activeModule!=null)ReturnFromModule();
        CollapseRing();
        previousCenter=prefs.CenterMode;
        restoreLeft=Left;restoreTop=Top;
        previousSurface=Content;
        activeModule=new DeskSpaceModuleV10(prefs,previousCenter,ReturnFromModule);
        activeModule.CalendarCompactRequested=OpenCalendarCompact;
        FrameworkElement surface;
        if(name=="Search")
        {
            var limits=ModuleArea();
            // A 116-DIP circle is too small for Search, Actions and Close.
            Width=DeskSpaceSearchDeckLayoutV225.SearchWidth(limits.Width);
            Height=48;
            activeSearch=new DeskSpaceSearchV11(prefs,ReturnFromModule,ResizeLiveSearch);
            surface=activeSearch.Create();
        }
        else if(name is "Notes" or "Reminders" or "Clipboard")
        {
            var limits=ModuleArea();
            Width=Math.Min(limits.Width,Math.Clamp(limits.Width*.31,275,380));
            // Compact modules should not reserve 390px when the item list
            // is empty; inner ScrollViewer still grows on demand.
            Height=Math.Min(limits.Height,
                name=="Notes"?256:name=="Reminders"?525:291);
            activeInline=new DeskSpaceInlineV36(name,previousCenter,ReturnFromModule,quickAddRequested);
            surface=activeInline.Create();
        }
        else if(name=="Calendar")
        {
            if(quickAddRequested)
            {
                var bounds=ModuleArea();
                Width=Math.Min(bounds.Width,455);
                Height=Math.Min(bounds.Height,550);
                surface=activeModule.Build("Calendar");
            }
            else
            {
                var bounds=ModuleArea();
                double size=Math.Min(342,Math.Min(bounds.Width,bounds.Height));
                Width=size;Height=size;
                surface=new Viewbox{Stretch=Stretch.Uniform,
                    Child=new DeskSpaceCompactCalendarV36().Create(OpenCalendarExpanded,ReturnFromModule,OpenCalendarQuickAdd)};
            }
        }
        else if(name=="Calculator")
        {
            // The Windows-native Calculator is the requested application,
            // not DeskSpace's legacy internal arithmetic keypad.
            var space=ModuleArea();
            Width=Math.Min(space.Width,282);
            Height=Math.Min(space.Height,132);
            var controls=new StackPanel{Margin=new Thickness(12)};
            controls.Children.Add(new TextBlock{
                Text="Windows Calculator",FontSize=17,
                Foreground=new SolidColorBrush(Color.FromRgb(34,43,75)),
                FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(0,0,0,10)});
            var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};
            var back=new Button{Content="← Circle",Padding=new Thickness(9,6,9,6),
                Margin=new Thickness(2),MinWidth=94};
            back.Click+=(_,_)=>ReturnFromModule();
            var open=new Button{Content="Open Calculator ↗",
                Padding=new Thickness(9,6,9,6),Margin=new Thickness(2),MinWidth=133};
            open.Click+=(_,_)=>LaunchWindowsCalculator();
            buttons.Children.Add(back);buttons.Children.Add(open);
            controls.Children.Add(buttons);
            surface=new Border{
                Background=new SolidColorBrush(Color.FromRgb(248,249,253)),
                CornerRadius=new CornerRadius(19),ClipToBounds=true,
                Child=controls
            };
            // Launch the genuine Microsoft/Windows built-in app immediately,
            // while leaving the DeskSpace Circle back control available.
            Dispatcher.BeginInvoke(new Action(LaunchWindowsCalculator),
                System.Windows.Threading.DispatcherPriority.Background);
        }
        else if(name=="Excel")
        {
            var area=ModuleArea();
            Width=Math.Min(area.Width,710);
            Height=Math.Min(area.Height,720);
            activeExcel=new DeskSpaceExcelV16(ReturnFromModule);
            surface=activeExcel.Create();
        }
        else
        {
            var available=ModuleArea();
            double w=name switch {
                "Settings"=>382,
                "Information"=>385,
                "Password"=>385,
                "Calculator"=>340,
                _=>418
            };
            double h=name switch {
                "Settings"=>414,
                "Information"=>385,
                "Password"=>425,
                "Calculator"=>405,
                _=>440
            };
            Width=Math.Min(available.Width,w);
            Height=Math.Min(available.Height,h);
            surface=activeModule.Build(name);
        }
        // Search temporarily suppresses this widget's Deck popup so that
        // a full-width field and running-app drawer never visually overlap.
        // No held-file data or user Deck visibility setting is changed.
        attachedShelf?.SetSearchActiveV225(name=="Search");
        PositionModuleFromCircle(restoreLeft,restoreTop,CompactDiameter,CompactDiameter);
        Content=surface;
        AnimateModuleOpen(surface);
        DeskSpaceWindowBehavior.AllowInput(this,true);
        ShowActivated=true;
        Show();Activate();
        if(quickAddRequested && name=="Calendar")
            Dispatcher.BeginInvoke(new Action(()=>activeModule?.StartNewItem()),
                System.Windows.Threading.DispatcherPriority.Input);
    }
    // Per-monitor working area in WPF device-independent pixels.
    private Size ModuleArea()
    {
        var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen=Forms.Screen.FromHandle(handle);
        var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);
        return new Size(Math.Max(230,screen.WorkingArea.Width/dpi.DpiScaleX-28),
            Math.Max(220,screen.WorkingArea.Height/dpi.DpiScaleY-28));
    }
    // An animated WPF Window.Width/Height defaults to HoldEnd, which can
    // permanently override the Width=OpenDiameter assignment used by the donut.
    // Explicit Stop and clear every active clock before changing surface mode.
    // One transition controller owns the animation clocks. Previous clocks
    // are cleared before a different module/window state is shown.
    private void PositionModuleFromCircle(double sourceLeft,double sourceTop,
        double sourceWidth,double sourceHeight)
    {
        Left=sourceLeft+sourceWidth/2-Width/2;
        Top=sourceTop+sourceHeight/2-Height/2;
        KeepOnVisibleDesktop();
    }
    private void StopMorphWindowAnimations()
    {
        BeginAnimation(Window.WidthProperty,null);
        BeginAnimation(Window.HeightProperty,null);
        BeginAnimation(Window.LeftProperty,null);
        BeginAnimation(Window.TopProperty,null);
    }
    private void AnimateModuleOpen(FrameworkElement surface)
    {
        AnimateModuleOpenFrom(surface,CompactDiameter,CompactDiameter,restoreLeft,restoreTop);
    }
    private void AnimateModuleOpenFrom(FrameworkElement surface,double startW,double startH,double startLeft,double startTop)
    {
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            StopMorphWindowAnimations();
            DeskSpaceReducedMotionV209.Reset(surface);
            return;
        }
        StopMorphWindowAnimations();
        double targetW=Width,targetH=Height,targetLeft=Left,targetTop=Top;
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut};
        var duration=new Duration(TimeSpan.FromMilliseconds(340));
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startW,targetW,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startH,targetH,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.LeftProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startLeft,targetLeft,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.TopProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startTop,targetTop,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        // A small entry scale plus delayed opacity removes the harsh
        // content-swap flash as the window expands from the centre circle.
        surface.RenderTransformOrigin=new Point(.5,.5);
        var transform=new ScaleTransform(1,1);
        surface.RenderTransform=transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
            Duration=duration,FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0,System.Windows.Media.Animation.KeyTime.FromPercent(.17)));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            1,System.Windows.Media.Animation.KeyTime.FromPercent(1)){EasingFunction=easing});
        surface.BeginAnimation(UIElement.OpacityProperty,fade);
    }
    private void AnimateModuleClose(double oldWidth,double oldHeight,double oldLeft,double oldTop)
    {
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            StopMorphWindowAnimations();
            if(Content is FrameworkElement immediate)
                DeskSpaceReducedMotionV209.Reset(immediate);
            return;
        }
        StopMorphWindowAnimations();
        var duration=new Duration(TimeSpan.FromMilliseconds(285));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldWidth,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldHeight,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.LeftProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldLeft,Left,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.TopProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldTop,Top,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        if(Content is FrameworkElement circle)
        {
            // Keep the clock invisible while the outgoing large panel shrinks,
            // then reveal it only when almost back at actual compact size.
            circle.RenderTransformOrigin=new Point(.5,.5);
            var t=new ScaleTransform(1,1);
            circle.RenderTransform=t;
            t.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                    FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
            t.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                    FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
            var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
                Duration=duration,FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(.69)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(1)){EasingFunction=easing});
            circle.BeginAnimation(UIElement.OpacityProperty,fade);
        }
    }
    private void OpenCalendarExpanded()
    {
        if(activeModule==null)return;
        double previousWidth=Width,previousHeight=Height;
        double previousLeft=Left,previousTop=Top;
        StopMorphWindowAnimations();
        var available=ModuleArea();
        Width=Math.Min(available.Width,602);
        Height=Math.Min(available.Height,686);
        PositionModuleFromCircle(previousLeft,previousTop,previousWidth,previousHeight);
        var surface=activeModule.Build("Calendar");
        Content=surface;
        AnimateModuleOpenFrom(surface,previousWidth,previousHeight,previousLeft,previousTop);
    }
    private void OpenCalendarCompact()
    {
        if(activeModule==null)return;
        double previousWidth=Width,previousHeight=Height;
        double previousLeft=Left,previousTop=Top;
        StopMorphWindowAnimations();
        var available=ModuleArea();
        double diameter=Math.Min(OpenDiameter,Math.Min(available.Width,available.Height));
        Width=diameter;Height=diameter;
        PositionModuleFromCircle(previousLeft,previousTop,previousWidth,previousHeight);
        FrameworkElement surface=new Viewbox{Stretch=Stretch.Uniform,
            Child=new DeskSpaceCompactCalendarV36().Create(OpenCalendarExpanded,ReturnFromModule,OpenCalendarQuickAdd)};
        Content=surface;
        AnimateModuleOpenFrom(surface,previousWidth,previousHeight,previousLeft,previousTop);
    }
    private void ResizeLiveSearch(double height)
    {
        if(activeSearch==null || activeModule==null)return;
        Width=DeskSpaceSearchDeckLayoutV225.SearchWidth(ModuleArea().Width);
        Height=Math.Min(ModuleArea().Height,Math.Clamp(height,48,340));
        KeepOnVisibleDesktop();
    }
    private bool closingMorph;

    // The outgoing module remains visible DURING shrink. This avoids the R24
    // flash where the clock and full module abruptly exchanged content.
    private void ReturnFromModule()
    {
        if(activeModule==null||closingMorph)return;
        closingMorph=true;
        StopMorphWindowAnimations();
        double fromWidth=Width,fromHeight=Height;
        double fromLeft=Left,fromTop=Top;
        double toWidth=CompactDiameter,toHeight=CompactDiameter;
        double toLeft=restoreLeft,toTop=restoreTop;
        // Establish base dimensions without painting them: active animation
        // clocks immediately supply the from-values for the first frame.
        Width=toWidth;Height=toHeight;
        Left=toLeft;Top=toTop;
        var duration=new Duration(TimeSpan.FromMilliseconds(265));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        if(Content is FrameworkElement leaving)
        {
            var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
                Duration=duration,
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(.28)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(.90)){EasingFunction=easing});
            leaving.BeginAnimation(UIElement.OpacityProperty,fade);
        }
        var width=new System.Windows.Media.Animation.DoubleAnimation(
            fromWidth,toWidth,duration){
            FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
            EasingFunction=easing};
        // One completion owner, so rapid clicking cannot dispose a surface
        // twice or resurrect a previous module after another view is opened.
        width.Completed+=(_,_)=>CompleteModuleReturn();
        BeginAnimation(Window.WidthProperty,width);
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromHeight,toHeight,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.LeftProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromLeft,toLeft,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.TopProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromTop,toTop,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
    }
    private void CompleteModuleReturn()
    {
        if(!closingMorph)return;
        closingMorph=false;
        StopMorphWindowAnimations();
        var old=activeModule;activeModule=null;
        var search=activeSearch;activeSearch=null;
        var inline=activeInline;activeInline=null;
        // Return to the normal shared Deck state after the original circle
        // has been restored, not during the Search-to-donut animation.
        // Restore the original circle BEFORE disposing the outgoing view.
        var restore=previousSurface ??
            (canvas.Parent as Viewbox ?? new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
        Content=restore;previousSurface=null;
        search?.Dispose();inline?.Dispose();
        activeExcel?.Dispose();activeExcel=null;
        old?.Dispose();
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        Width=Height=CompactDiameter;
        Left=restoreLeft;Top=restoreTop;
        KeepOnVisibleDesktop();
        BuildCompact();SavePosition();UpdateCenter();
        attachedShelf?.SetSearchActiveV225(false);
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
            System.Windows.Threading.DispatcherPriority.Background);
    }
    private bool CanDragFrom(MouseButtonEventArgs e)
    {
        DependencyObject? target = e.OriginalSource as DependencyObject;
        while (target != null && target != this)
        {
            if (target is Button || target is MenuItem) return false;
            target = target is Visual ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target);
        }
        if(activeModule!=null)return false;
        if (!ringExpanded) return true;
        var p = e.GetPosition(this);
        return (p - new Point(Width / 2, Height / 2)).Length <= Width * 69.0 / 420.0;
    }

    private int SectorAt(Point p)
    {
        if(!ringExpanded || activeModule!=null)return -1;
        double scale=420.0/Width;
        double x=210+(p.X-Width/2)*scale;
        double y=210+(p.Y-Height/2)*scale;
        double dx=x-210,dy=y-210;
        double radius=Math.Sqrt(dx*dx+dy*dy);
        if(radius<70 || radius>195 ||
           (radius>124 && radius<132))return -1;
        bool outer=radius>=132;
        double sectors=outer?6:6;
        double step=360.0/sectors;
        double angle=Math.Atan2(dy,dx)*180/Math.PI;
        double offset=(angle+90+step/2+720)%360;
        int slot=(int)Math.Floor(offset/step);
        if(slot<0 || slot>=sectors || offset-slot*step>=step-1.5)return -1;
        return (outer?6:0)+slot;
    }
    private void UpdateHighlightedSector(Point p)
    {
        int selected=SectorAt(p);
        // Exactly one contextual parent is active at any time. Moving into
        // ANY other real module sector immediately closes Information's
        // child donut and lower shelf. A separator (-1) does not close it:
        // users must cross the small blank gap to reach the child arc.
        if(selected>=0 && selected!=6)
            CloseInformationContext();
        else if(selected==6 && ringExpanded && activeModule==null)
            OpenInformationArc();

        // Excel Functions is outer sector 11; Information stays outer 6.
        // Hover cannot paste Excel data; child click/drag-release is required.
        if(selected==11 && ringExpanded && activeModule==null)
            OpenExcelArc();
        if(selected>=0 && selected!=11 && excelWheel?.IsArcOpen==true)
            excelWheel.HideArc();        // Applications replaces the duplicate Files sector at outer index 10.
        if(selected==10 && ringExpanded && activeModule==null)
            OpenApplicationsArc();
        if(selected>=0 && selected!=10 && appsWheel?.IsArcOpen==true)
            appsWheel.HideArc();
        if(selected==highlightedSector)return;
        highlightedSector=selected;
        for(int i=0;i<ringPaths.Count;i++)
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==selected,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
    }
    private void ApplyWeatherContrast()
    {
        if(SystemParameters.HighContrast)
        {
            content.Foreground=SystemColors.WindowTextBrush;
            return;
        }
        var weather=DeskSpaceWeather.Latest;
        // Location-based and cached city labels must not disable contrast:
        // the current scene already uses this exact WeatherReading.
        DeskSpaceWeatherContrastV64.Apply(content,weather,prefs.ShowWeather);
    }
    private void OpenCalendarQuickAdd()
    {
        OpenCalendarExpanded();
        Dispatcher.BeginInvoke(new Action(()=>activeModule?.StartNewItem()),
            System.Windows.Threading.DispatcherPriority.Input);
    }
    private void LaunchWindowsCalculator()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{
                FileName="calc.exe",UseShellExecute=true});
        }
        catch(Exception ex)
        {
            MessageBox.Show("Windows Calculator could not be opened: "+ex.Message,
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private void BuildCompact()
    {
        content.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brushes.White;
        appsWheel?.HideArc();
        excelWheel?.HideArc();
        if(informationOverlay?.IsArcOpen==true)
            informationOverlay.HideArc();
        if(activeModule==null && !ringExpanded)
        {
            StopMorphWindowAnimations();
            Width=Height=CompactDiameter;
            KeepOnVisibleDesktop();
        }
        SuspendWeatherMotion();
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
            Cursor = Cursors.Hand,
            ToolTip = null };
        Canvas.SetLeft(circle, 10); Canvas.SetTop(circle, 9);
        canvas.Children.Add(circle);
        clockFace = circle;
        AddWeatherScene(10,9,272);
        content.FontSize = 35;
        content.IsHitTestVisible=false;
        bool liveClock=prefs.CenterMode=="Clock" && prefs.ShowWeather &&
            DeskSpaceWeather.Latest!=null &&
            DeskSpaceWeather.Latest.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase);
        if(liveClock)
        {
            // Weather text overlays the circle directly without an extra plate.
            content.Width=208;content.Height=110;
            Canvas.SetLeft(content,42);Canvas.SetTop(content,142);
        }
        else
        {
            content.Width=244;content.Height=176;
            Canvas.SetLeft(content,24);Canvas.SetTop(content,74);
        }
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
    }

    // R231: no implicit 22-pixel edge attraction while dragging. The
    // explicit tray menu "Hide at screen edge" remains supported.

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
        if(selected!=mainWheelSector || add!=mainWheelQuick)
        {
            mainWheelSector=selected;
            mainWheelQuick=add;
            mainWheelEnteredUtc=DateTime.UtcNow;
            return;
        }
        if(!DeskSpaceWheelHoverV35.Dwelled(mainWheelEnteredUtc,
            DeskSpaceWheelHoverV35.MainCommitMilliseconds))return;
        // The pointer has been stable on this specific sector / + badge.
        // Commit once; a new ring expansion starts with a fresh timer.
        StopWheelHover();
        if(add>=0)
        {
            string target=Segments[add]=="Tasks"?"Reminders":Segments[add];
            if(target=="Clipboard")
                return; // capture into clipboard shelf still requires click
            OpenInWidget(target,true);
        }
        else
            ClickSegment(Segments[selected]);
    }
    // Automatic screen-edge accommodation must never become a permanent
    // relocation. Only the user dragging the widget changes its home.
    private readonly DeskSpaceDonutHomeV65 donutHome=new();
    // Keyboard mode is activated only by the explicit tray item; the normal
    // widget retains WS_EX_NOACTIVATE and never steals focus from Excel.
    private bool keyboardDonutModeV207;
    private int keyboardDonutSectorV207=-1;

    private void BeginKeyboardDonutV207()
    {
        if(activeModule!=null)return;
        Reveal();
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        // Windows can now focus this widget only until the user selects,
        // presses Esc, or activates another application.
        DeskSpaceWindowBehavior.AllowInput(this,true);
        ShowActivated=true;
        keyboardDonutModeV207=true;
        keyboardDonutSectorV207=0;
        System.Windows.Automation.AutomationProperties.SetLiveSetting(
            content,System.Windows.Automation.AutomationLiveSetting.Polite);
        SetKeyboardDonutHighlightV207();
        Activate();
        Focus();
        System.Windows.Input.Keyboard.Focus(this);
    }

    private void SetKeyboardDonutHighlightV207()
    {
        if(!ringExpanded || activeModule!=null ||
           keyboardDonutSectorV207<0 ||
           keyboardDonutSectorV207>=Segments.Length)return;
        CloseInformationContext();
        if(appsWheel?.IsArcOpen==true)appsWheel.HideArc();
        if(excelWheel?.IsArcOpen==true)excelWheel.HideArc();
        highlightedSector=keyboardDonutSectorV207;
        for(int i=0;i<ringPaths.Count;i++)
        {
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==highlightedSector,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
            ringPaths[i].StrokeThickness=i==highlightedSector?4:2;
        }
        var name=Segments[keyboardDonutSectorV207];
        content.Text=SegmentIcons[keyboardDonutSectorV207]+" "+name+
            "\n← → select · ↑ ↓ ring · Enter open · Esc close";
        System.Windows.Automation.AutomationProperties.SetName(content,
            "DeskSpace donut sector "+(keyboardDonutSectorV207+1)+
            " of "+Segments.Length+": "+name);
        System.Windows.Automation.AutomationProperties.SetHelpText(this,
            "Use Left or Right to select a sector, Up or Down to switch rings, "+
            "Enter to open, Escape to close keyboard navigation.");
    }

    private void EndKeyboardDonutV207()
    {
        if(!keyboardDonutModeV207)return;
        keyboardDonutModeV207=false;
        keyboardDonutSectorV207=-1;
        // Return to the original non-activating widget behavior. The
        // module UI will explicitly enable input again if it needs it.
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        System.Windows.Automation.AutomationProperties.SetName(content,
            "DeskSpace widget centre");
    }

    private void KeyboardDonutKeyV207(object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if(!keyboardDonutModeV207 || activeModule!=null || !ringExpanded)
            return;
        if(e.Key==System.Windows.Input.Key.Escape)
        {
            EndKeyboardDonutV207();
            CollapseRing();
            e.Handled=true;
            return;
        }
        if(e.Key==System.Windows.Input.Key.Enter)
        {
            var index=keyboardDonutSectorV207;
            EndKeyboardDonutV207();
            if(index>=0 && index<Segments.Length)
                ClickSegment(Segments[index]);
            e.Handled=true;
            return;
        }
        if(!DeskSpaceDonutKeyboardV207.IsNavigation(e.Key))return;
        keyboardDonutSectorV207=DeskSpaceDonutKeyboardV207.Next(
            keyboardDonutSectorV207,e.Key);
        SetKeyboardDonutHighlightV207();
        e.Handled=true;
    }
    private void ToggleRing()
    {
        if(!ringExpanded && activeModule==null) donutHome.Capture(this);
        if(ringExpanded)
        {
            if(keyboardDonutModeV207)EndKeyboardDonutV207();
            CloseInformationContext();
        }
        if (hiddenAtEdge) return;
        double centreX = Left + Width / 2;
        double centreY = Top + Height / 2;
        ringExpanded = !ringExpanded;
        Width = Height = ringExpanded ? OpenDiameter : CompactDiameter;
        Left = centreX - Width / 2;
        Top = centreY - Height / 2;
        KeepOnVisibleDesktop();
        if (ringExpanded)
        {
            BuildDonut();
            StartWheelHover();
        }
        else
        {
            StopWheelHover();
            BuildCompact();
        }
        SavePosition();
        UpdateCenter();
    }

    private void CollapseRing()
    {
        appsWheel?.HideArc();
        excelWheel?.HideArc();
        if(informationOverlay?.IsArcOpen==true)
            informationOverlay.HideArc();
        if (ringExpanded) ToggleRing();
        if(activeModule==null && donutHome.Restore(this)) SavePosition();
    }

    private void AddWeatherHalo()
    {
        if (prefs.CenterMode != "Clock" || !prefs.ShowWeather || hiddenAtEdge) return;
        var halo = new Ellipse
        {
            Width=258, Height=258,
            Fill=Brushes.Transparent,
            Stroke=new SolidColorBrush(Color.FromArgb(165,255,255,255)),
            StrokeThickness=4,
            IsHitTestVisible=false,
            Opacity=0.4
        };
        Canvas.SetLeft(halo,17);Canvas.SetTop(halo,17);
        canvas.Children.Add(halo);weatherHalo=halo;
        var motion = new System.Windows.Media.Animation.DoubleAnimation(0.28,0.75,
            new Duration(TimeSpan.FromSeconds(9)))
        {
            AutoReverse=true,
            RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        halo.BeginAnimation(UIElement.OpacityProperty,motion);
    }
    private void SuspendWeatherMotion()
    {
        weatherHalo?.BeginAnimation(UIElement.OpacityProperty,null);
        weatherHalo=null;
        weatherScene?.StopMotion();
        weatherScene=null;
    }

    private static bool HasModuleQuickAdd(string module) =>
        module is "Notes" or "Reminders" or "Tasks" or "Calendar" or "Clipboard";

    private int QuickAddHitAt(Point p)
    {
        if(!ringExpanded || activeModule!=null)return -1;
        double scale=420.0/Width;
        double x=210+(p.X-Width/2)*scale;
        double y=210+(p.Y-Height/2)*scale;
        for(int i=0;i<Segments.Length;i++)
        {
            if(!HasModuleQuickAdd(Segments[i]))continue;
            bool outer=i>=6;
            int slot=outer?i-6:i;
            double angle=(-90+slot*(360.0/(outer?6:6)))*Math.PI/180;
            double distance=outer?182:113;
            double px=210+distance*Math.Cos(angle);
            double py=210+distance*Math.Sin(angle);
            double dx=x-px,dy=y-py;
            if(dx*dx+dy*dy<=121)return i; // eleven-DIP target radius
        }
        return -1;
    }
    private void BuildDonut()
    {
        content.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brushes.White;
        DeskSpaceMonitorSessionsV154.PrepareExpansion(this,OpenDiameter);
        // All twelve wedges are defined on a full 420-DIP canvas. They must
        // NEVER be rendered into the 116-DIP compact Window after module close.
        StopMorphWindowAnimations();
        Width=OpenDiameter;
        Height=OpenDiameter;
        KeepOnVisibleDesktop();
        SuspendWeatherMotion();
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
                Text=SegmentIcons[i]+"\n"+Segments[i],FontSize=outer?12:11,
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
        AddWeatherScene(145,145,130);
        content.FontSize=20;content.Width=116;content.Height=98;
        Canvas.SetLeft(content,152);Canvas.SetTop(content,161);canvas.Children.Add(content);
        UpdateCenter();
    }
    private static Geometry Sector(double cx,double cy,double inner,double outer,double deg1,double deg2)
    {
        Point At(double radius,double d) { double a=d*Math.PI/180; return new Point(cx+radius*Math.Cos(a),cy+radius*Math.Sin(a)); }
        var fig=new PathFigure { StartPoint=At(inner,deg1),IsClosed=true,IsFilled=true };
        fig.Segments.Add(new LineSegment(At(outer,deg1),true));
        fig.Segments.Add(new ArcSegment(At(outer,deg2),new Size(outer,outer),0,false,SweepDirection.Clockwise,true));
        fig.Segments.Add(new LineSegment(At(inner,deg2),true));
        fig.Segments.Add(new ArcSegment(At(inner,deg1),new Size(inner,inner),0,false,SweepDirection.Counterclockwise,true));
        return new PathGeometry(new []{ fig });
    }
    private string ClockWithWeather()
    {
        var time=DateTime.Now.ToString("hh:mm tt");
        var value=DeskSpaceWeather.Latest;
        if(!prefs.ShowWeather || value==null ||
            !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return time+"\n"+DateTime.Now.ToString("ddd dd MMM");
        var degrees=Math.Round(value.Celsius)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool cached=DeskSpaceWeather.IsOffline ||
            DateTime.UtcNow-value.FetchedUtc>TimeSpan.FromMinutes(40);
        return time+"\n"+degrees+"°C"+(cached?" · saved":"")+
            "\n"+DateTime.Now.ToString("ddd dd MMM");
    }
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
    private void OnUpdateProgress()
    {
        if (shutdown) return;
        _ = Dispatcher.BeginInvoke(new Action(() => { if (!shutdown) UpdateCenter(); }));
    }

    private void OnWeatherUpdated()
    {
        if (shutdown) return;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (shutdown) return;
            if(activeModule==null)
            {
                if(ringExpanded)BuildDonut();
                else BuildCompact();
                UpdateCenter();
            }
        }));
    }
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
    private void ShowAbout()
    {
        MessageBox.Show("DeskSpace " + DeskSpaceUpdater.CurrentVersion +
            "\n\nLive donut widget, universal search, modules, reminders and optional weather." +
            "\nUpdates: authenticated private GitHub releases" +
            "\nLocal workspace: " + WorkspaceStore.Root,
            "About DeskSpace",MessageBoxButton.OK,MessageBoxImage.Information);
    }    private void ChooseCenter()
    {
        var cm = new ContextMenu();
        foreach (string mode in new[]{"Clock","Date","Calendar","Notes","Reminders"})
        {
            var item=new MenuItem { Header=mode,IsCheckable=true,IsChecked=prefs.CenterMode.Equals(mode,StringComparison.OrdinalIgnoreCase) };
            item.Click+=(_,_)=>{prefs.CenterMode=mode;prefs.Save();UpdateCenter();};
            cm.Items.Add(item);
        }
        cm.PlacementTarget=canvas;cm.IsOpen=true;
    }
    private bool informationArcOpening;
    private int informationArcVersion;

    private void CloseInformationContext()
    {
        // Also cancel any deferred opening from an earlier press/release.
        // Otherwise the old submenu reappears after another module is hovered.
        if(informationOverlay?.IsArcOpen!=true && !informationArcOpening)return;
        informationArcVersion++;
        informationArcOpening=false;
        if(informationOverlay?.IsArcOpen==true)
            informationOverlay.HideArc();
    }

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
        if(!appsWheel.IsArcOpen)appsWheel.Show();
        if(appsWheel.IsArcOpen)
            content.Text="Applications"+Environment.NewLine+
                appsWheel.PinnedCount+" pinned";
    }

    private void OpenExcelArc()
    {
        if(activeModule!=null)return;
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        excelWheel??=new DeskSpaceExcelWheelV42(this,()=>{
            if(ringExpanded)CollapseRing();
        });
        excelWheel.SetExcelAvailabilityChanged(open=>{
            if(ringExpanded && activeModule==null && highlightedSector==11)
                content.Text=open
                    ? "Excel Functions"+Environment.NewLine+
                      excelWheel.SavedCount+" saved"
                    : "Excel Functions"+Environment.NewLine+"Excel is not open";
        });
        if(!excelWheel.IsPasting && !excelWheel.IsArcOpen)
            excelWheel.Show();
        if(excelWheel.IsArcOpen)
            content.Text=DeskSpaceExcelWheelV42.ExcelIsOpen()
                ? "Excel Functions"+Environment.NewLine+excelWheel.SavedCount+" saved"
                : "Excel Functions"+Environment.NewLine+"Excel is not open";
    }
    private void OpenInformationArc()
    {
        if(activeModule!=null ||
           informationOverlay?.IsArcOpen==true ||
           informationArcOpening)return;
        informationArcOpening=true;
        int version=++informationArcVersion;
        void ShowAttachedArc()
        {
            // Stale dispatcher operations must not reopen a submenu after the
            // pointer has already moved to Notes, Calendar, Search, etc.
            if(version!=informationArcVersion)return;
            informationArcOpening=false;
            if(activeModule!=null || informationOverlay?.IsArcOpen==true)return;
            if(!ringExpanded)ToggleRing();
            if(!ringExpanded)return;
            // SystemParameters.WorkArea describes the PRIMARY display.
            // Never pull a secondary widget toward its neighbour when the
            // Information arc opens. The R152/R153 native HWND fence already
            // constrains this widget and its popups to the assigned monitor.
            DeskSpaceMonitorFenceV152.ClampNow(this);
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
        }
        // Hover opens instantly; only pressed gestures need deferred opening.
        if(ringExpanded)
            ShowAttachedArc(); // needed for a held drag entering a child arc
        else
            Dispatcher.BeginInvoke(new Action(ShowAttachedArc),
                System.Windows.Threading.DispatcherPriority.Background);
    }
    private void ClickSegment(string name)
    {
        if(name=="Applications")
        {
            OpenApplicationsArc();
            return;
        }
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
        if(informationOverlay?.IsEditing==true &&
           !informationOverlay.ConfirmNavigation())return;
        CloseInformationContext();
        informationOverlay?.Hide();
        if(name is "Clock" or "Weather")
        {
            prefs.CenterMode=name=="Tasks"?"Tasks":name=="Date"?"Date":"Clock";
            prefs.Save();CollapseRing();BuildCompact();UpdateCenter();
            return;
        }
        if(name=="Calculator")
        {
            OpenInWidget(name=="Files"?"Search":name);
            return;
        }
        if(name is "Files" or "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Information" or "Settings" or "Excel")
            OpenInWidget(name=="Files"?"Search":name);
    }
    private void ApplySettings()
    {
        prefs.Save();prefs.ApplyWindowsStartup();UpdateCenter();
        if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
    }
    private void ToggleDetails(bool show=false)
    {
        if (detail?.IsVisible==true) {detail.Close();detail=null;if(!show)return;}
        detail = new WidgetDetailsWindow(prefs.CenterMode,()=>ReadWorkspace());
        var a=SystemParameters.WorkArea;
        detail.Left = Math.Clamp(Left+Width+12,a.Left,Math.Max(a.Left,a.Right-detail.Width));
        detail.Top = Math.Clamp(Top,a.Top,Math.Max(a.Top,a.Bottom-detail.Height));
        detail.Show();
    }
    private void OpenSearch() {OpenInWidget("Search");}
    private void OpenRecent() {OpenInWidget("Search");}
    private void OpenMain()
    {
        var exe=Environment.ProcessPath;
        if(!string.IsNullOrEmpty(exe))Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});
    }
    private Workspace ReadWorkspace()
    {
        try {workspace=store.Load();} catch(IOException) { }
        return workspace;
    }
    private void Tick()
    {
        if (prefs.CenterMode == "Clock" && prefs.ShowWeather &&
            (DateTime.UtcNow-lastWeatherPoll).TotalMinutes>=1)
        {
            lastWeatherPoll=DateTime.UtcNow;
            _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
        }
        DeskSpaceClipboardV10.Poll();
        if(activeModule!=null)return;
        if ((DateTime.Now-lastDataLoad).TotalSeconds>=15) { ReadWorkspace();lastDataLoad=DateTime.Now; }
        UpdateCenter();
        if ((DateTime.UtcNow-lastFullscreenPoll).TotalSeconds>=2)
        {
            previousFullscreen=FullscreenDetector.IsOtherFullscreen();
            lastFullscreenPoll=DateTime.UtcNow;
        }
        bool isFullscreen=previousFullscreen;
        if (!dockedManually && !prefs.AlwaysVisible && prefs.AutoHideFullscreen &&
            DateTime.Now > revealedUntil && isFullscreen)
        { if (!hiddenAtEdge) DockToEdge(); }
        else if (hiddenAtEdge && !dockedManually && !isFullscreen) Reveal();
    }
    private void UpdateCenter()
    {
        if(informationOverlay?.IsOpen==true)
        {
            content.Text="Information"+Environment.NewLine+
                informationOverlay.SavedCount+" saved";
            return;
        }
        if (!CenterAllowed.Contains(prefs.CenterMode)) prefs.CenterMode="Clock";
        content.ToolTip=DeskSpaceWeather.WeatherStatus;
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
        var sky=DeskSpaceWeather.Latest;
        var hasSky=prefs.CenterMode=="Clock" && prefs.ShowWeather && sky!=null;
        content.Foreground=hasSky && sky!.IsDay && sky.Code<=1
            ? new SolidColorBrush(Color.FromRgb(24,31,60)) : Brushes.White;
        content.Effect=hasSky
            ? new System.Windows.Media.Effects.DropShadowEffect{
                Color=sky!.IsDay && sky.Code<=1
                    ? Color.FromArgb(160,255,250,233):Color.FromArgb(200,10,17,40),
                BlurRadius=4,ShadowDepth=0,Opacity=.85}
            : null;

        var progress=DeskSpaceUpdateProgress.Snapshot();
        if (progress.Active)
        {
            var message = "UPDATE\n" + progress.Label +
                (progress.Percent.HasValue ? "\n" + progress.Percent.Value + "%" : "");
            if (content.Text != message) content.Text = message;
            content.FontSize=ringExpanded?16:23;
            return;
        }
        string nextCentreText=prefs.CenterMode switch {
            "Date" => DateTime.Now.ToString("ddd\ndd MMM\nyyyy"),
            "Calendar" => DateTime.Now.ToString("ddd dd MMM")+"\n"+workspace.Events.Count(e=>e.Date.Date==DateTime.Today)+" events",
            "Notes" => workspace.Notes.Count==0?"No notes":Trim(workspace.Notes.OrderByDescending(n=>n.Updated).First().Title,ringExpanded ? 28 : 20),
            "Reminders" => workspace.Reminders.Where(x=>!x.Done).OrderBy(x=>x.Due).FirstOrDefault() is ReminderItem rem?Trim(rem.Title,20)+"\n"+rem.Due.ToString("ddd HH:mm"):"No reminders",
            "Tasks" => workspace.Reminders.Count(x=>!x.Done)+" pending\ntasks",
            _=> ClockWithWeather()
        };
        if (!string.Equals(content.Text,nextCentreText,StringComparison.Ordinal))
            content.Text=nextCentreText;
        ApplyWeatherContrast();
    }
    private static string Trim(string s,int n)=>s.Length>n?s[..(n-1)]+"…":s;
    private Window CreateEdgeHandle()
    {
        var w=new Window {Title="DeskSpace Edge Tab",Width=30,Height=80,ShowInTaskbar=false,
            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowActivated=false};
        w.SourceInitialized += (_, _) => DeskSpaceWindowBehavior.Apply(w);
        var button=new Button {Content="❯",Background=accent,Foreground=Brushes.White,FontSize=18,
            ToolTip="Show DeskSpace donut",BorderThickness=new Thickness(0)};
        button.MouseEnter+=(_,_)=> Reveal();button.Click+=(_,_)=>Reveal();w.Content=button;return w;
    }
    private void DockToEdge(bool manual = false)
    {
        if (hiddenAtEdge) return;
        dockedManually = manual;
        SavePosition();
        // WPF Left/Top are DIPs, not Windows screen pixels. Using FromPoint
        // with DIPs assigned a mixed-DPI widget to the wrong monitor.
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var monitor=(DeskSpaceMonitorFenceV152.BoundScreen(this) ??
            Forms.Screen.FromHandle(hwnd)).WorkingArea;
        var area=new Rect(monitor.Left, monitor.Top, monitor.Width, monitor.Height);
        double leftDist=Math.Abs(Left-area.Left),rightDist=Math.Abs(area.Right-(Left+Width));
        edgeHandle.Left=leftDist<=rightDist?area.Left:area.Right-edgeHandle.Width;
        edgeHandle.Top=Math.Clamp(Top + Height / 2 - edgeHandle.Height / 2,area.Top,area.Bottom-edgeHandle.Height);
        SuspendWeatherMotion();
        hiddenAtEdge=true;Hide();edgeHandle.Show();edgeHandle.Topmost=true;
    }
    private void Reveal()
    {
        revealedUntil=DateTime.Now.AddMinutes(2);
        if(hiddenAtEdge)
        {
            // prefs.Left/Top are the PRIMARY widget's legacy settings.
            // A secondary revealing from its edge handle must never
            // teleport to that shared location. Hide() never moved the
            // widget HWND; simply reveal it at its own saved location.
            hiddenAtEdge=false;
            dockedManually=false;
            edgeHandle.Hide();
        }
        Show();Topmost=true;
        if (!ringExpanded) BuildCompact(); // resume paused weather animation
    }
}

public static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)] private struct MONITORINFO { public uint cbSize;public RECT rcMonitor;public RECT rcWork;public uint dwFlags; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RECT r);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    public static bool IsOtherFullscreen()
    {
        try
        {
            var w=GetForegroundWindow(); if(w==IntPtr.Zero) return false;
            GetWindowThreadProcessId(w,out var pid);if(pid==Environment.ProcessId)return false;
            if(!GetWindowRect(w,out var rect))return false;
            var info=new MONITORINFO {cbSize=(uint)Marshal.SizeOf<MONITORINFO>()};
            if(!GetMonitorInfo(MonitorFromWindow(w,2),ref info))return false;
            return rect.Left<=info.rcMonitor.Left+2&&rect.Top<=info.rcMonitor.Top+2&&
                rect.Right>=info.rcMonitor.Right-2&&rect.Bottom>=info.rcMonitor.Bottom-2;
        }
        catch{return false;}
    }
}
