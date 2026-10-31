using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// Owned by the actual donut window. A Popup is anchored to the parent widget:
// no taskbar entry, no independent application and no Clipboard-module dependency.
internal sealed class DeskSpaceDropShelfV15 : IDisposable
{
    // Auto-discovered files remain ORIGINAL references until explicitly
    // dragged onto the circle; manual/copied entries remain staged copies.
    private sealed record Held(string Name,string StagedPath,bool AutoDetected=false);
    private readonly Window host;
    private readonly Popup popup;
    // A choice is owned by the widget that received the drag. Only the
    // completed staged result is shared with other monitor Deck views.
    private System.Windows.Documents.AdornerLayer? folderLayer;
    private DeskSpaceFolderDragAdornerV160? folderAdorner;
    private UIElement? folderAdorned;
    private System.Windows.Documents.AdornerLayer? smartLayerV178;
    private DeskSpaceSmartTextAdornerV178? smartAdornerV178;
    private string? pendingSmartTextV178;
    private bool pendingSmartUrlV178;
    private string[]? pendingFolderDrop;
    private DeskSpaceFolderPreDropV256.Choice hoverChoiceV256;
    private readonly Border panel;
    private readonly StackPanel list;
    private static readonly List<Held> held=new();
    // Source-path deduplication applies only to staged copies, not external
    // recent-file discovery. Paths are never persisted or exported.
    private static readonly Dictionary<string,string> originalByStaged=
        new(StringComparer.OrdinalIgnoreCase);
    internal static bool AlreadyHeldSource(string source)
    {
        var canonical=Path.GetFullPath(source);
        return originalByStaged.Values.Any(original=>
            string.Equals(original,canonical,StringComparison.OrdinalIgnoreCase));
    }
    private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();
    private static void RefreshViews()
    {
        foreach(var view in Views.ToArray())
            if(!view.disposed)view.Render();
    }
    private readonly List<Held> detected=new();
    // A successfully held original must not reappear as another new suggestion.
    private readonly HashSet<string> pinnedSourcePaths=new(StringComparer.OrdinalIgnoreCase);
    private DeskSpaceNewFileWatchV53? newFiles;
    private static readonly string staging=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","TemporaryShelf","Retained");
    private static bool busy;
    // A content match is a question, never permission to replace files.
    // The pending decision is shared between Deck views on all monitors.
    private sealed class DuplicateChoiceV211
    {
        internal readonly string SourceName;
        internal readonly string ExistingName;
        internal readonly TaskCompletionSource<bool> Decision=
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal DuplicateChoiceV211(string source,string existing)
        { SourceName=source;ExistingName=existing; }
    }
    private static DuplicateChoiceV211? pendingDuplicateV211;
    private static bool zipInProgress;
    // A single shared copy transaction is visible and cancellable from
    // either monitor's Deck. Closing a widget does not discard shared data.
    private static bool copyInProgress;
    // Keep copy failures visible inside the shared Deck. Do not show a
    // modal MessageBox while a WPF drag-drop transaction is unwinding.
    private static string? lastCopyError;
    private static CancellationTokenSource? copyCancellation;
    private static long copyBytesWritten,copyBytesTotal;
    private static int copyFilesWritten,copyFilesTotal;
    private static int zipFilesWritten;
    private static CancellationTokenSource? zipCancellation;
    // A temporary archive has a different path from the ORIGINAL folder.
    // The origin must be shared across Copy and ZIP dedup paths.
    private static readonly Dictionary<string,string> pendingZipSources=
        new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;
    private bool heldExpanded=true,recentExpanded=true;
    private bool deckMinimized,deckDismissed,appRenderPending;
    private bool searchActiveV225;
    // Foreground Search hides ONLY this widget's Deck HWND; source data,
    // retained staged files and other displays stay untouched.
    internal void SetSearchActiveV225(bool active)
    {
        if(disposed||searchActiveV225==active)return;
        searchActiveV225=active;
        if(active)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
        }
        else Render();
    }
    private bool previewOnHover=true;
    private DeskSpaceDeckPreviewV99? deckPreview;
    private string dismissedNonAppSignature="";
    private IReadOnlyList<DeskSpaceRunningAppsV88.Entry> runningWindows=
        Array.Empty<DeskSpaceRunningAppsV88.Entry>();
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath)
            .Concat(excelMatchesV122.Select(x=>x.Id.ToString())));
    private IReadOnlyList<DeskSpaceRunningAppsV88.Entry> runningApps=
        Array.Empty<DeskSpaceRunningAppsV88.Entry>();
    private readonly DispatcherTimer runningAppsTimer=new(){
        Interval=TimeSpan.FromSeconds(2)
    };
    private bool runningAppsTimerConnected,refreshingRunningApps;
    // Compatibility pointer for older Deck UI hooks only; files, clipboard,
    // lifecycle and refresh always use the shared store and Views.
    private static DeskSpaceDropShelfV15? active;
    public static bool IsActive=>Views.Count>0;
    public static IReadOnlyList<string> ActivePaths=>
        held.Select(x=>x.StagedPath).ToArray();
    public static void ImportClipboardFiles()=>Views.FirstOrDefault()?.ImportFromClipboard();
    public static void ClearActive()=>Views.FirstOrDefault()?.ClearAll();
    public static void SelectWatchedFolder()
    {
        var self=active;
        if(self==null||self.disposed)return;
        using var dialog=new System.Windows.Forms.FolderBrowserDialog{
            Description="Select an ordinary work folder for recent saved files",
            ShowNewFolderButton=false
        };
        if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
        try
        {
            string folder=Path.GetFullPath(dialog.SelectedPath);
            if(!DeskSpaceNewFileWatchV53.IsAllowedWatchFolder(folder))
            {
                MessageBox.Show("Only ordinary work folders can be monitored. " +
                    "Hidden, system, application and cache folders are excluded.",
                    "DeskSpace saved files",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }
            var store=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace","RecentFileFolders.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(store)!);
            var entries=File.Exists(store)?File.ReadAllLines(store):Array.Empty<string>();
            if(!entries.Contains(folder,StringComparer.OrdinalIgnoreCase))
                File.AppendAllLines(store,new[]{folder});
            self.newFiles?.Dispose();
            self.newFiles=new DeskSpaceNewFileWatchV53(self.host,
                self.OfferNewFile,onRemoved:self.RemoveRecentFile);
            self.newFiles.Start();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Cannot monitor that folder: "+ex.Message,
                "DeskSpace saved files",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private int placementNudge;
    static DeskSpaceDropShelfV15()
    {
        // Only prune app-owned GUID temporary ZIP directories older than one
        // day, never active ZIPs, user files or the held-file staging path.
        _=Task.Run(()=>{
            try
            {
                // Recover only app-marked unfinished staged copies. Never
                // delete complete held files, source files, or another live
                // DeskSpace process's exclusive copy lock.
                DeskSpaceInterruptedCopyV165.PruneAbandoned(
                    Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                        "DeskSpace","TemporaryShelf"),
                    TimeSpan.FromHours(24));
            }
            catch(Exception error){System.Diagnostics.Debug.WriteLine(
                "Copy temporary recovery skipped: "+error.GetType().Name);}
            try{DeskSpaceFolderZipV162.PruneAbandoned(
                Path.Combine(Path.GetTempPath(),"DeskSpaceFolderZip"),
                TimeSpan.FromHours(24));}
            catch(Exception error){System.Diagnostics.Debug.WriteLine(
                "ZIP temporary cleanup skipped: "+error.GetType().Name);}
        });
        // Display disconnect must not delete files used by the other widget.
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{
            try{if(Directory.Exists(staging))Directory.Delete(staging,true);}
            catch(IOException){}catch(UnauthorizedAccessException){}
        };
    }
    public DeskSpaceDropShelfV15(Window window)
    {
        host=window;
        Views.Add(this);
        active ??= this;
        host.AllowDrop=true;
        host.PreviewDragOver+=DragOver;
        host.PreviewDrop+=Receive;
        host.LocationChanged+=Position;
        host.SizeChanged+=Position;
        host.IsVisibleChanged+=Visible;
        host.Closed+=Closed;
        list=new StackPanel {Margin=new Thickness(7,6,7,6)};
        panel=new Border{
            CornerRadius=new CornerRadius(15),
            Background=new SolidColorBrush(Color.FromArgb(204,20,28,52)),
            BorderBrush=new SolidColorBrush(Color.FromArgb(170,187,201,245)),
            BorderThickness=new Thickness(1.1),
            MinWidth=180,MaxWidth=310,
            Child=new ScrollViewer{
                Content=list,MaxHeight=295,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled
            },AllowDrop=true
        };
        panel.PreviewDragOver+=DragOver;
        panel.PreviewDrop+=Receive;
        DeskSpaceDeckVisibilityV89.Changed+=DeckVisibilityChanged;
        popup=new Popup {
            PlacementTarget=host,Placement=PlacementMode.Custom,
            HorizontalOffset=0,VerticalOffset=5,StaysOpen=true,
            AllowsTransparency=true,PopupAnimation=PopupAnimation.Fade,
            Child=panel
        };
        popup.CustomPopupPlacementCallback=(size,target,offset)=>{
            var widget=new Rect(host.Left,host.Top,
                host.ActualWidth>0?host.ActualWidth:host.Width,
                host.ActualHeight>0?host.ActualHeight:host.Height);
            var chosen=DeskSpaceRecentPlacementV82.Choose(
                widget,SystemParameters.WorkArea,size);
            return new[]{new CustomPopupPlacement(
                chosen,PopupPrimaryAxis.None)};
        };
        // Held-file popup belongs to the physical display hosting this widget.
        // The previous bottom placement was relative to the virtual desktop
        // and WPF could flip/clip the dock onto the adjacent monitor.
        // Keep independent dock HWNDs on their OWN monitor, with a scrollable
        // viewport. Staged content remains shared across all dock views.
        DeskSpaceMonitorDockV190.Attach(host,popup,panel);
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
        // Folder actions are drawn inside this widget's actual WPF window,
        // before the user releases a dragged folder. Never use a Popup.
        host.PreviewDragEnter+=FolderDragEnter;
        host.PreviewDragLeave+=SmartDragLeaveV178;
        host.PreviewDragLeave+=FolderDragLeave;
        host.IsVisibleChanged+=FolderChoiceVisible;
        host.Closed+=(_,_)=>{HideFolderChoice();HideSmartDropV178();};
    }
    private bool restoredHeldFiles;
    public void Enable()
    {
        if(disposed)return;
        if(!restoredHeldFiles)
        {
            restoredHeldFiles=true;
            RestoreHeldFiles();
        }
        if(!runningAppsTimerConnected)
        {
            runningAppsTimerConnected=true;
            runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();
        }
        if(DeskSpaceDeckVisibilityV89.Visible)
        {
            runningAppsTimer.Start();
            RefreshRunningApps();
        }
        newFiles ??= new DeskSpaceNewFileWatchV53(host,OfferNewFile,onRemoved:RemoveRecentFile);
        newFiles.Start();
        Render();
    }
    // Reconstruct retained copies after restart without touching originals.
    // Each GUID staging folder holds exactly one copied file or directory.
    private void RestoreHeldFiles()
    {
        try
        {
            if(!Directory.Exists(staging))return;
            foreach(var slot in Directory.EnumerateDirectories(staging)
                .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                if(held.Count>=25)break;
                try
                {
                    if((File.GetAttributes(slot)&FileAttributes.ReparsePoint)!=0)continue;
                    var items=Directory.EnumerateFileSystemEntries(slot).Take(2).ToArray();
                    if(items.Length!=1)continue;
                    if((File.GetAttributes(items[0])&FileAttributes.ReparsePoint)!=0)continue;
                    held.Add(new Held(Path.GetFileName(items[0]),items[0]));
                }
                catch(IOException){}
                catch(UnauthorizedAccessException){}
            }
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
    private void RemoveRecentFile(string path)
    {
        if(disposed)return;
        DeskSpaceSearchIndexV13.UnregisterLivePath(path);
        if(detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase))>0)Render();
    }
    private void OfferNewFile(string path)
    {
        if(disposed||!File.Exists(path))return;
        // Previously dismissed version stays dismissed across app restarts.
        // A newly saved version with a different timestamp/size is eligible.
        if(DeskSpaceRecentFileStateV99.IsDismissed(path))return;
        // Register the name/path with Search before any Search window opens.
        DeskSpaceSearchIndexV13.RegisterLivePath(path);
        if(pinnedSourcePaths.Contains(Path.GetFullPath(path)))return;
        if(held.Any(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase)))return;
        // Saving an existing Excel/Word/PDF should bring it to the top as
        // recently saved, not get discarded as already discovered.
        detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase));
        detected.Insert(0,new Held(Path.GetFileName(path),path,true));
        while(detected.Count>30)detected.RemoveAt(detected.Count-1);
        ScheduleRecentRender();
    }
    private readonly System.Windows.Threading.DispatcherTimer recentRenderTimer=
        new(){Interval=TimeSpan.FromMilliseconds(180)};
    private bool recentTimerAttached;
    private void ScheduleRecentRender()
    {
        if(disposed)return;
        if(!recentTimerAttached)
        {
            recentTimerAttached=true;
            recentRenderTimer.Tick+=(_,_)=>{
                recentRenderTimer.Stop();
                if(!disposed)Render();
            };
        }
        recentRenderTimer.Stop();
        recentRenderTimer.Start();
    }
    public void SetMediaOffset(double offset)
    {
        if(disposed)return;
        popup.VerticalOffset=Math.Max(5,offset);
    }
    private void DeckVisibilityChanged(bool visible)
    {
        if(disposed)return;
        if(!host.Dispatcher.CheckAccess())
        {
            host.Dispatcher.BeginInvoke(new Action(()=>DeckVisibilityChanged(visible)));
            return;
        }
        if(!visible)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
            runningAppsTimer.Stop();
        }
        else
        {
            deckDismissed=false;
            runningAppsTimer.Start();
            RefreshRunningApps();
            Render();
        }
    }
    private void Position(object? sender,EventArgs e)
    {
        // A placement update is required on some WPF builds after moving a Window.

        if(!popup.IsOpen)return;
        popup.HorizontalOffset=(++placementNudge%2==0)?0:0.01;
    }
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(disposed)return;
        // Parentheses matter: Media presence must not reopen the Deck
        // while Search owns input or while the source widget is hidden.
        bool contents=held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count>0 ||
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;
        popup.IsOpen=!deckDismissed &&
            DeskSpaceSearchDeckLayoutV225.ShowDeck(
                DeskSpaceDeckVisibilityV89.Visible,(bool)e.NewValue,
                searchActiveV225,contents);
    }
    private void Closed(object? sender,EventArgs e)=>Dispose();
    private static bool IsOwnPath(string path,string basePath)
    {
        try{return Path.GetFullPath(path).StartsWith(
            Path.GetFullPath(basePath)+Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);}
        catch{return false;}
    }
    private void DragOver(object sender,DragEventArgs e)
    {
        // A user-initiated text or URL drag has its own choices.
        // Ordinary files and the folder ZIP/Copy arc remain unchanged.
        if(DeskSpaceSmartTextDropV178.TryExtract(e.Data,
            out var smartText,out var smartUrl))
        {
            HideFolderChoice();
            if(!busy)
                ShowSmartDropV178(smartText,smartUrl);
            var point=e.GetPosition(host);
            smartAdornerV178?.Hover(point);
            e.Effects=busy||
                DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),point)==null
                ?DragDropEffects.None:DragDropEffects.Copy;
            e.Handled=true;
            return;
        }
        HideSmartDropV178();
        // DragEnter/DragOver, not Drop, must reveal the two choice wedges.
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(paths?.Any(Directory.Exists)==true)
            {
                if(!busy)
                    ShowFolderChoice(paths);
                var point=e.GetPosition(host);
                folderAdorner?.Hover(point);
                var hovered=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),point);
                hoverChoiceV256=hovered.HasValue?
                    (hovered.Value?DeskSpaceFolderPreDropV256.Choice.Zip:
                        DeskSpaceFolderPreDropV256.Choice.Copy):
                    DeskSpaceFolderPreDropV256.Choice.None;
                e.Effects=busy||
                    DeskSpaceFolderDragAdornerV160.ChoiceAt(
                        new Size(host.ActualWidth,host.ActualHeight),point)==null
                        ?DragDropEffects.None:DragDropEffects.Copy;
                e.Handled=true;
                return;
            }
        }
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        e.Effects=(busy||disposed||held.Count>=25)?DragDropEffects.None:DragDropEffects.Copy;
        e.Handled=true;
    }
    private void Receive(object sender,DragEventArgs e)
    {
        if(DeskSpaceSmartTextDropV178.TryExtract(e.Data,
            out var smartText,out var smartUrl))
        {
            var target=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                new Size(host.ActualWidth,host.ActualHeight),e.GetPosition(host));
            HideSmartDropV178();
            e.Effects=target.HasValue?DragDropEffects.Copy:DragDropEffects.None;
            e.Handled=true;
            if(!busy && !disposed && target is bool left)
                host.Dispatcher.BeginInvoke(new Action(()=>
                    DeskSpaceSmartTextDropV178.Commit(host,smartText,smartUrl,left)),
                    System.Windows.Threading.DispatcherPriority.Background);
            return;
        }
        HideSmartDropV178();
        // DragOver already showed the two halves of the circle.
        // Releasing over ZIP (left) or Copy (right) immediately performs
        // the corresponding action; there is NO after-drop confirmation.
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var dropped=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(dropped?.Any(Directory.Exists)==true)
            {
                var selected=DeskSpaceFolderDragAdornerV160.ChoiceAt(
                    new Size(host.ActualWidth,host.ActualHeight),e.GetPosition(host));
                // ZIP/Copy requires the same sources and same selected
                // half during BOTH DragOver and Drop. No late prompt.
                bool approved=DeskSpaceFolderPreDropV256.Authorized(
                    hoverChoiceV256,pendingFolderDrop,dropped) &&
                    selected.HasValue &&
                    (selected.Value?
                        hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Zip:
                        hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Copy);
                bool zip=hoverChoiceV256==DeskSpaceFolderPreDropV256.Choice.Zip;
                HideFolderChoice();
                e.Effects=approved?DragDropEffects.Copy:DragDropEffects.None;
                e.Handled=true;
                if(!busy && approved)
                    _=CommitFolderDropAsync(dropped,zip);
                return;
            }
        }
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        // Never claim a drop succeeded if a copy is already in progress or
        // the shelf is full. Leave the new-file suggestion available to retry.
        if(disposed||busy||held.Count>=25)
        {
            e.Effects=DragDropEffects.None;
            e.Handled=true;
            return;
        }
        e.Effects=DragDropEffects.Copy;
        e.Handled=true;
        var files=e.Data.GetData(DataFormats.FileDrop) as string[];
        if(files?.Length>0)_=StageFilesAsync(files);
    }
    private void FolderDragEnter(object sender,DragEventArgs e)=>DragOver(sender,e);

    private void FolderDragLeave(object sender,DragEventArgs e)
    {
        if(pendingFolderDrop==null)return;
        // Nested WPF elements can generate a DragLeave without leaving
        // the host; do not flicker when crossing internal donut graphics.
        var p=e.GetPosition(host);
        if(p.X>=0&&p.Y>=0&&p.X<=host.ActualWidth&&p.Y<=host.ActualHeight)
            return;
        HideFolderChoice();
    }

    private void FolderChoiceVisible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!(bool)e.NewValue){HideFolderChoice();HideSmartDropV178();}
    }
    private void SmartDragLeaveV178(object sender,DragEventArgs e)
    {
        if(pendingSmartTextV178==null)return;
        var p=e.GetPosition(host);
        if(p.X>=0&&p.Y>=0&&p.X<=host.ActualWidth&&p.Y<=host.ActualHeight)
            return;
        HideSmartDropV178();
    }
    private void ShowSmartDropV178(string text,bool web)
    {
        if(disposed||busy)return;
        if(smartAdornerV178!=null &&
           pendingSmartUrlV178==web)
        {
            pendingSmartTextV178=text;
            return;
        }
        HideSmartDropV178();
        if(host.Content is not UIElement decorated)return;
        System.Windows.Documents.AdornerLayer? layer=null;
        if(decorated is System.Windows.Documents.AdornerDecorator existing &&
           existing.Child is UIElement inner)
        {
            layer=existing.AdornerLayer;
            decorated=inner;
        }
        layer??=System.Windows.Documents.AdornerLayer.GetAdornerLayer(decorated);
        if(layer==null)
        {
            host.Content=null;
            var wrapper=new System.Windows.Documents.AdornerDecorator{
                Child=decorated};
            host.Content=wrapper;
            layer=wrapper.AdornerLayer;
        }
        pendingSmartTextV178=text;
        pendingSmartUrlV178=web;
        smartAdornerV178=new DeskSpaceSmartTextAdornerV178(decorated,web);
        smartLayerV178=layer;
        layer.Add(smartAdornerV178);
    }
    private void HideSmartDropV178()
    {
        pendingSmartTextV178=null;
        if(smartAdornerV178!=null && smartLayerV178!=null)
        {
            try{smartLayerV178.Remove(smartAdornerV178);}
            catch(InvalidOperationException){}
        }
        smartAdornerV178=null;
        smartLayerV178=null;
    }

    private void ShowFolderChoice(string[] incoming)
    {
        if(disposed||busy)return;
        var valid=incoming.Where(path=>
            (File.Exists(path)||Directory.Exists(path))&&
            !IsOwnPath(path,staging)).ToArray();
        if(valid.Length==0)return;
        // The only shared state is the *completed* Deck item. This drag
        // choice belongs exclusively to the widget under the pointer.
        pendingFolderDrop=valid;
        if(folderAdorner!=null)return;
        if(host.Content is not UIElement content)return;
        System.Windows.Documents.AdornerLayer? layer=null;
        if(content is System.Windows.Documents.AdornerDecorator priorDecorator &&
           priorDecorator.Child is UIElement inner)
        {
            layer=priorDecorator.AdornerLayer;
            content=inner;
        }
        layer??=System.Windows.Documents.AdornerLayer.GetAdornerLayer(content);
        if(layer==null)
        {
            // Default WPF Window template normally has an AdornerLayer.
            // Provide one locally if a customized host template omits it.
            // Detach before reparenting; otherwise WPF throws because the
            // existing content already has a logical/visual parent.
            host.Content=null;
            var decorator=new System.Windows.Documents.AdornerDecorator{Child=content};
            host.Content=decorator;
            layer=decorator.AdornerLayer;
        }
        folderAdorned=content;
        folderLayer=layer;
        folderAdorner=new DeskSpaceFolderDragAdornerV160(content);
        layer.Add(folderAdorner);
    }

    private void HideFolderChoice()
    {
        pendingFolderDrop=null;
        hoverChoiceV256=DeskSpaceFolderPreDropV256.Choice.None;
        if(folderAdorner!=null&&folderLayer!=null)
        {
            try{folderLayer.Remove(folderAdorner);}
            catch(InvalidOperationException){}
        }
        folderAdorner=null;
        folderLayer=null;
        folderAdorned=null;
    }

    private void CommitFolderDrop(bool zipFolders)
    {
        // Only called by tests/compatibility; the actual interaction commits
        // the hovered half directly in Receive when the folder is dropped.
        var paths=pendingFolderDrop;
        HideFolderChoice();
        if(disposed||paths==null||paths.Length==0)return;
        host.Dispatcher.BeginInvoke(new Action(()=>
            _=CommitFolderDropAsync(paths,zipFolders)),
            System.Windows.Threading.DispatcherPriority.Background);
    }
    private async Task CommitFolderDropAsync(string[] paths,bool zipFolders)
    {
        // PreviewDrop must finish before any disk activity or Deck rendering.
        await Task.Yield();
        if(!zipFolders)
        {
            // Copy keeps the existing folder staging path unchanged.
            await StageFilesAsync(paths);
            return;
        }
        foreach(var source in paths)
        {
            if(!Directory.Exists(source))
            {
                await StageFilesAsync(new[]{source});
                continue;
            }
            // Both ZIP and Copy count as holding the same original source.
            if(AlreadyHeldSource(source))continue;
            if(busy)return;
            var temporary=Path.Combine(Path.GetTempPath(),
                "DeskSpaceFolderZip",Guid.NewGuid().ToString("N"));
            var zipPath=Path.Combine(temporary,DeskSpaceFolderZipV159.ZipName(source));
            using var cancellation=new CancellationTokenSource();
            try
            {
                zipCancellation=cancellation;
                zipFilesWritten=0;
                zipInProgress=true;
                busy=true;
                RefreshViews();
                // Keep R162's existing donut cancellation and file count.
                // Add bounded ZIP creation, byte progress and explicit Cancel UI.
                var progressWindow=DeskSpaceZipProgressWindowV206.Show(host,cancellation);
                try
                {
                    var reporter=new Progress<DeskSpaceFolderZipV206.Progress>(item=>{
                        progressWindow.Report(item);
                        if(item.Files==1||item.Files%5==0)
                        {
                            if(zipInProgress&&ReferenceEquals(zipCancellation,cancellation))
                            {
                                zipFilesWritten=item.Files;
                                RefreshViews();
                            }
                        }
                    });
                    await Task.Run(()=>DeskSpaceFolderZipV206.Create(
                        source,zipPath,reporter,cancellation.Token));
                }
                finally{progressWindow.Close();}
                cancellation.Token.ThrowIfCancellationRequested();
                zipInProgress=false;
                zipCancellation=null;
                busy=false;
                RefreshViews();
                pendingZipSources[zipPath]=source;
                try{await StageFilesAsync(new[]{zipPath});}
                finally{pendingZipSources.Remove(zipPath);}
            }
            catch(OperationCanceledException)
            {
                // A cancellation is normal; R162's writer removes .partial.
                System.Diagnostics.Debug.WriteLine("DeskSpace ZIP cancelled");
            }
            catch(Exception ex)
            {
                MessageBox.Show("Could not ZIP the folder: "+ex.Message,
                    "DeskSpace folder ZIP",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            finally
            {
                if(ReferenceEquals(zipCancellation,cancellation))
                    zipCancellation=null;
                if(zipInProgress){zipInProgress=false;busy=false;RefreshViews();}
                try{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
    private void ImportFromClipboard()
    {
        try{
            if(!Clipboard.ContainsFileDropList())return;
            _=StageFilesAsync(Clipboard.GetFileDropList().Cast<string>().ToArray());
        }catch(Exception){}
    }
    private async Task StageFilesAsync(string[] files)
    {
        if(busy||disposed||files.Length==0)return;
        lastCopyError=null;
        System.Diagnostics.Debug.WriteLine("R173 stage: StageFilesAsync entry");
        Console.WriteLine("R173 stage: StageFilesAsync entry");
        busy=true;
        Console.WriteLine("R173 stage: before initial RefreshViews");
        RefreshViews();
        Console.WriteLine("R173 stage: after initial RefreshViews");
        try
        {
            foreach(var source in files)
            {
                if(held.Count>=25)break;
                if(!File.Exists(source)&&!Directory.Exists(source))continue;
                if(IsOwnPath(source,staging))continue;
                // The shared Deck is one queue across widgets: another monitor
                // must not duplicate files already held from the same source.
                var originalSource=pendingZipSources.TryGetValue(source,
                    out var remembered)?remembered:source;
                if(AlreadyHeldSource(originalSource))continue;
                // File content checks run only after a user-initiated drop.
                // No filesystem work is performed on the WPF UI thread.
                if(File.Exists(source) && DeskSpaceDeckVisibilityV89.Visible &&
                    Views.Count>0)
                {
                    var currentlyHeld=held.Where(x=>File.Exists(x.StagedPath))
                        .Select(x=>x.StagedPath).ToArray();
                    var duplicate=await Task.Run(()=>
                        DeskSpaceContentDuplicateV211.FindMatching(
                            source,currentlyHeld,CancellationToken.None));
                    if(duplicate!=null)
                    {
                        var decision=new DuplicateChoiceV211(
                            Path.GetFileName(source),Path.GetFileName(duplicate));
                        pendingDuplicateV211=decision;
                        RefreshViews();
                        bool keepBoth=false;
                        try
                        {
                            // Never wait forever after an abandoned drop.
                            var completed=await Task.WhenAny(decision.Decision.Task,
                                Task.Delay(TimeSpan.FromMinutes(3)));
                            keepBoth=ReferenceEquals(completed,decision.Decision.Task)
                                && await decision.Decision.Task;
                        }
                        finally
                        {
                            if(ReferenceEquals(pendingDuplicateV211,decision))
                                pendingDuplicateV211=null;
                            RefreshViews();
                        }
                        if(!keepBoth)continue;
                    }
                }
                var id=Guid.NewGuid().ToString("N");
                var destination=Path.Combine(staging,id,Path.GetFileName(source.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)));
                                using var cancellation=new CancellationTokenSource();
                copyCancellation=cancellation;
                copyInProgress=true;
                copyBytesWritten=0;copyBytesTotal=0;
                copyFilesWritten=0;copyFilesTotal=0;
                RefreshViews();
                try
                {
                    // A tiny exclusive marker lets next startup distinguish
                    // a crashed copy from a real file named *.partial.
                    // Dispose always removes it on success or cancellation.
                    // The entire exclusive-file marker and copy lifetime is
                    // background work, never synchronous disk IO on WPF UI.
                    // The copy worker writes only to a GUID-owned *.partial
                    // path and atomically renames it when completely copied.
                    Console.WriteLine("R173 stage: before starting background Copy");
                    await Task.Run(()=>{
                        Console.WriteLine("R173 stage: worker before marker lease");
                        using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
                        Console.WriteLine("R173 stage: worker lease acquired");
                        DeskSpaceFolderCopyV164.Copy(
                        source,destination,cancellation.Token,
                        (written,total,filesCopied,filesTotal)=>{
                            host.Dispatcher.BeginInvoke(new Action(()=>{
                                if(copyInProgress&&ReferenceEquals(
                                    copyCancellation,cancellation))
                                {
                                    copyBytesWritten=written;copyBytesTotal=total;
                                    copyFilesWritten=filesCopied;
                                    copyFilesTotal=filesTotal;
                                    RefreshViews();
                                }
                            }));
                        });
                    });
                    Console.WriteLine("R173 stage: worker Copy complete");
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                catch
                {
                    // A cancellation can arrive after atomic rename but
                    // before the Deck adopts the item. Remove only that copy.
                    DeletePath(destination);
                    throw;
                }
                finally
                {
                    if(ReferenceEquals(copyCancellation,cancellation))
                    {
                        copyCancellation=null;
                        copyInProgress=false;
                        RefreshViews();
                    }
                }
                // Detached monitor views do not own these shared copies.
                // The user deliberately dragged this newly detected source
                // ONTO the circle: convert the discovery row into the same
                // durable, staged copy already used for manually dropped files.
                int removed=detected.RemoveAll(x=>string.Equals(x.StagedPath,source,
                    StringComparison.OrdinalIgnoreCase));
                if(removed>0)pinnedSourcePaths.Add(Path.GetFullPath(source));
                held.Add(new Held(Path.GetFileName(destination),destination));
                originalByStaged[destination]=Path.GetFullPath(originalSource);
                RefreshViews();
            }
        }
        catch(OperationCanceledException)
        {
            // An explicit cancellation is not an error dialog.
            System.Diagnostics.Debug.WriteLine("DeskSpace Deck copy cancelled");
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine("R173 COPY FAILED: "+ex.GetType().Name+
                ": "+ex.Message);
            System.Diagnostics.Debug.WriteLine("DeskSpace Deck Copy error: "+ex);
            lastCopyError=ex.GetType().Name;
            RefreshViews();
            // A modal warning in PreviewDrop freezes desktop automation and
            // can permanently trap a hidden widget after the Explorer switch.
            _=Task.Delay(TimeSpan.FromSeconds(12)).ContinueWith(_=>
            {
                try{host.Dispatcher.BeginInvoke(new Action(()=>{
                    lastCopyError=null;
                    RefreshViews();
                }));}
                catch(TaskCanceledException){}
                catch(InvalidOperationException){}
            });
        }
        finally{busy=false;RefreshViews();}
    }
    private void ClearAll()
    {
        foreach(var item in held.ToArray())Remove(item);
    }
    // FileSystemWatcher.Created can arrive before Save As / browser writes
    // finish. An exclusive read prevents retaining an incomplete copy.
    // Only the background Task.Run worker waits; WPF remains responsive.
    private static void CopyFileWhenReady(string source,string destination)
    {
        IOException? last=null;
        for(int attempt=0;attempt<20;attempt++)
        {
            try
            {
                using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None);
                using var output=new FileStream(destination,FileMode.Create,FileAccess.Write,FileShare.None);
                input.CopyTo(output);
                output.Flush(true);
                return;
            }
            catch(IOException ex)
            {
                last=ex;
                try{if(File.Exists(destination))File.Delete(destination);}
                catch(IOException){}
                catch(UnauthorizedAccessException){}
                if(attempt<19)System.Threading.Thread.Sleep(200);
            }
        }
        throw new IOException("File is still being saved. Finish saving and drag it again.",last);
    }
    private static void CopyFolder(string source,string destination)
    {
        // Never traverse junctions or symbolic links into unrelated trees.
        Directory.CreateDirectory(destination);
        var pending=new Stack<(string Source,string Destination)>();
        pending.Push((source,destination));
        while(pending.Count>0)
        {
            var (folder,target)=pending.Pop();
            foreach(var file in Directory.EnumerateFiles(folder))
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);
            }
            foreach(var dir in Directory.EnumerateDirectories(folder))
            {
                if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
                var child=Path.Combine(target,Path.GetFileName(dir));
                Directory.CreateDirectory(child);
                pending.Push((dir,child));
            }
        }
    }
    private static void DeletePath(string path)
    {
        try {
            if(File.Exists(path))File.Delete(path);
            else if(Directory.Exists(path))Directory.Delete(path,true);
            var parent=Path.GetDirectoryName(path);
            if(parent!=null&&Directory.Exists(parent)&&!Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent);
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    private void Remove(Held item)
    {
        if(item.AutoDetected)
        {
            DeskSpaceRecentFileStateV99.Dismiss(item.StagedPath);
            detected.Remove(item); // Never delete the user's original file
            if(string.Equals(deckPreview?.CurrentPath,item.StagedPath,
                StringComparison.OrdinalIgnoreCase))deckPreview?.Close();
        }
        else
        {
            if(!held.Remove(item))return;
            originalByStaged.Remove(item.StagedPath);
            DeletePath(item.StagedPath);
            RefreshViews();
        }
        Render();
    }
    private void CopyToClipboard(Held item)
    {
        try{
            var paths=new StringCollection();paths.Add(item.StagedPath);
            Clipboard.SetFileDropList(paths);
            // Windows gives no reliable confirmation of a Ctrl+V into another app.
            // Leave the temporary copy until manually cleared or dragged successfully.
        }catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace temporary shelf");}
    }
    private static TextBlock Text(string text,int size,Brush foreground)=>new(){
        Text=text,FontSize=size,Foreground=foreground,
        TextTrimming=TextTrimming.CharacterEllipsis,
        VerticalAlignment=VerticalAlignment.Center
    };
    // Excel suggestion state is independent of Running Apps expansion.
    private bool excelExpandedV122=true,excelRefreshingV122;
    private readonly List<(Guid Id,string Name,int Writable)> excelMatchesV122=new();
    private readonly DispatcherTimer excelSuggestTimerV122=new(){
        Interval=TimeSpan.FromSeconds(18)
    };
    private bool excelSuggestTimerConnectedV122;
    // Read-only previews only. Requires an ACTUAL foreground Excel window,
    // a unique saved reference-label signature and at least one safe value.
    // Never silently change a workbook.
    private async void RefreshExcelSuggestionsV122()
    {
        if(disposed||excelRefreshingV122)return;
        excelRefreshingV122=true;
        try
        {
            var found=new List<(Guid Id,string Name,int Writable)>();
            if(DeskSpaceExcelActiveV122.ForegroundExcel())
            {
                var templates=DeskSpaceExcelStoreV16.Load().Where(x=>
                    x.CoordinatesVerified &&
                    x.Cells.Any(c=>c.Role=="Value" && c.ValueKind!="blank") &&
                    (x.Anchors.Count>0 || x.Cells.Any(c=>c.Role=="Reference")))
                    .Take(8).ToArray();
                foreach(var item in templates)
                {
                    if(disposed||!DeskSpaceExcelActiveV122.ForegroundExcel())break;
                    try {
                        var proposed=await DeskSpaceExcelWorkerV41.PreviewAsync(
                            item,false,false);
                        if(DeskSpaceExcelActiveV122.SafeToSuggest(
                            DeskSpaceExcelActiveV122.ForegroundExcel(),
                            proposed.Verified,proposed.Writable))
                            found.Add((item.Id,item.Name,proposed.Writable));
                    }catch(Exception){} // No match never changes Excel.
                }
            }
            if(disposed)return;
            var prior=excelMatchesV122.ToArray();
            excelMatchesV122.Clear();excelMatchesV122.AddRange(found);
            if(!prior.SequenceEqual(found))Render();
        }
        catch(Exception ex){
            System.Diagnostics.Debug.WriteLine("DeskSpace Excel suggestions: "+ex);
        }
        finally{excelRefreshingV122=false;}
    }
    private async void ReviewExcelSuggestionV122(Guid id)
    {
        try
        {
            var saved=DeskSpaceExcelStoreV16.Load();
            var item=saved.FirstOrDefault(x=>x.Id==id);
            if(item==null)throw new InvalidOperationException(
                "The saved mapping changed. Refresh suggestions.");
            var proposal=await DeskSpaceExcelWorkerV41.PreviewAsync(
                item,false,false);
            if(!proposal.Verified || proposal.Writable<1)
                throw new InvalidOperationException(
                    "The Excel headers or destination changed. No values pasted.");
            if(MessageBox.Show(host,
                "Match: "+item.Name+"\n"+proposal.Reason+
                "\n\nPaste "+proposal.Writable+
                " values into "+proposal.SheetName+"?\n"+
                "Existing values and formulas stay protected. "+
                "A workbook backup is made first; Excel is not auto-saved.",
                "DeskSpace · Review header/value paste",
                MessageBoxButton.YesNo,MessageBoxImage.Question)!=
                MessageBoxResult.Yes)return;
            // The user approved a particular mapping and destination.
            // Excel or the saved mapping may change during the dialog.
            // A second, read-only COM preview MUST match every cell before
            // any write or backup workflow is entered.
            var currentMappingV263=DeskSpaceExcelStoreV16.Load()
                .FirstOrDefault(x=>x.Id==id);
            if(currentMappingV263==null)
                throw new InvalidOperationException(
                    "The mapping changed after review. Refresh suggestions.");
            var currentProposalV263=await DeskSpaceExcelWorkerV41.PreviewAsync(
                currentMappingV263,false,false);
            if(!DeskSpaceExcelSuggestionReviewV263.SameTarget(
                proposal,currentProposalV263))
                throw new InvalidOperationException(
                    "Excel or the destination changed during review. Preview again.");
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                currentProposalV263,currentMappingV263,false);
            MessageBox.Show(host,
                "Value mapping applied. Review Excel and save manually.\n"+
                "Safety backup: "+backup,"DeskSpace Excel",
                MessageBoxButton.OK,MessageBoxImage.Information);
            RefreshExcelSuggestionsV122();
        }
        catch(Exception ex)
        {
            MessageBox.Show(host,
                "Excel suggestion blocked. Recheck the active workbook, "+
                "mapping and selected cells before trying again ("+
                ex.GetType().Name+"). Nothing was automatically pasted.",
                "DeskSpace Excel",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    // Scan in the background. Do not block circle hover, dragging, or file drops.
    private async void RefreshRunningApps()
    {
        if(!disposed && !excelSuggestTimerConnectedV122)
        {
            excelSuggestTimerConnectedV122=true;
            excelSuggestTimerV122.Tick+=(_,_)=>RefreshExcelSuggestionsV122();
            excelSuggestTimerV122.Start();
            RefreshExcelSuggestionsV122();
        }
        if(disposed||refreshingRunningApps)return;
        refreshingRunningApps=true;
        try
        {
            var windows=await Task.Run(DeskSpaceRunningAppsV88.SnapshotWindows);
            if(disposed)return;
            var snapshot=DeskSpaceRunningAppsV88.GroupVisible(windows);
            var previous=runningWindows.Select(x=>x.Name+"|"+x.Handle+
                "|"+x.WindowTitle).ToArray();
            var current=windows.Select(x=>x.Name+"|"+x.Handle+
                "|"+x.WindowTitle).ToArray();
            if(!previous.SequenceEqual(current))
            {
                if(DeskSpaceStableHoverV93.IsHoverActive)
                    appRenderPending=true;
                else
                {
                    runningWindows=windows;
                    runningApps=snapshot;
                    Render();
                }
            }
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DeskSpace running apps refresh: "+ex);
        }
        finally{refreshingRunningApps=false;}
    }
    private static Button DeckHeaderButton(string symbol,string tooltip,Action click)
    {
        var button=new Button{
            Content=symbol,ToolTip=tooltip,
            Width=22,Height=22,MinWidth=22,MinHeight=22,
            FontSize=13,FontWeight=FontWeights.Medium,
            Foreground=Brushes.LightSteelBlue,
            BorderThickness=new Thickness(0),
            Background=Brushes.Transparent,
            Padding=new Thickness(0),Margin=new Thickness(0,0,1,0),
            VerticalAlignment=VerticalAlignment.Center,
            HorizontalContentAlignment=HorizontalAlignment.Center,
            VerticalContentAlignment=VerticalAlignment.Center,
            Cursor=Cursors.Hand
        };
        var chrome=new FrameworkElementFactory(typeof(Border));
        chrome.Name="Chrome";
        chrome.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));
        chrome.SetValue(Border.BackgroundProperty,
            new System.Windows.TemplateBindingExtension(
                System.Windows.Controls.Control.BackgroundProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);
        chrome.AppendChild(presenter);
        button.Template=new ControlTemplate(typeof(Button)){VisualTree=chrome};
        button.MouseEnter+=(_,_)=>button.Background=new SolidColorBrush(
            Color.FromArgb(115,101,126,167));
        button.MouseLeave+=(_,_)=>button.Background=Brushes.Transparent;
        button.Click+=(_,_)=>click();
        return button;
    }
    // Only refresh the deck when the media title/playback state actually
    // changes. The transport controls never auto-start or interrupt playback.
    private readonly DispatcherTimer mediaDeckTimerV123=new(){
        Interval=TimeSpan.FromSeconds(1.8)
    };
    private string mediaDeckStampV123="";
    private bool mediaDeckTimerConnectedV123;
    private void RefreshMediaDeckV123()
    {
        if(disposed)return;
        var player=DeskSpaceMediaV18.ActiveV123;
        var stamp=player?.DeckStampV123??"";
        if(stamp==mediaDeckStampV123)return;
        mediaDeckStampV123=stamp;
        Render();
    }
    private void Render()
    {
        if(disposed)return;
        if(searchActiveV225)
        {
            // Two-second running-app refresh and shared Deck updates must
            // not resurrect the Popup in front of the Search field.
            deckPreview?.Close();
            popup.IsOpen=false;
            return;
        }
        DeskSpaceMediaV18.DeckIntegrationV123=true;
        if(!mediaDeckTimerConnectedV123)
        {
            mediaDeckTimerConnectedV123=true;
            mediaDeckTimerV123.Tick+=(_,_)=>RefreshMediaDeckV123();
            mediaDeckTimerV123.Start();
        }
        deckPreview ??= new DeskSpaceDeckPreviewV99(panel);
        deckPreview.CloseIfMissing();
        list.Children.Clear();
        if(pendingDuplicateV211 is { } duplicateV211)
        {
            var row=new StackPanel{Margin=new Thickness(5,3,5,6)};
            row.Children.Add(new TextBlock{
                Text="Identical file already held: "+duplicateV211.SourceName+
                    " matches "+duplicateV211.ExistingName,
                FontSize=11,TextWrapping=TextWrapping.Wrap,
                Foreground=Brushes.White
            });
            var choices=new StackPanel{Orientation=Orientation.Horizontal};
            var keep=new Button{Content="Keep both",FontSize=10,
                ToolTip="Retain an additional copy; original and existing held file remain intact",
                Padding=new Thickness(7,2,7,2),Margin=new Thickness(0,3,5,0)};
            keep.Click+=(_,_)=>duplicateV211.Decision.TrySetResult(true);
            var skip=new Button{Content="Skip",FontSize=10,
                ToolTip="Do not stage another copy; leave both original and existing held item unchanged",
                Padding=new Thickness(7,2,7,2),Margin=new Thickness(0,3,0,0)};
            skip.Click+=(_,_)=>duplicateV211.Decision.TrySetResult(false);
            choices.Children.Add(keep);choices.Children.Add(skip);
            row.Children.Add(choices);
            list.Children.Add(row);
            // A prior dismissed Deck must not silently hide the user's
            // explicit keep/skip decision.
            deckDismissed=false;
        }
        if(lastCopyError!=null)
        {
            list.Children.Add(new TextBlock{
                Text="Copy could not finish ("+lastCopyError+"). Original preserved.",
                Foreground=Brushes.Orange,FontSize=11,TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(5,2,5,6)});
            popup.IsOpen=host.IsVisible;
            return; // error row remains visible even with an empty shelf
        }
        if(copyInProgress)
        {
            var copyRow=new DockPanel{
                Margin=new Thickness(5,2,5,4),LastChildFill=true};
            var cancelCopy=new Button{Content="Cancel Copy",FontSize=10,
                ToolTip="Cancel staging; source files and existing Deck items remain unchanged",
                Padding=new Thickness(6,2,6,2),MinWidth=80};
            cancelCopy.Click+=(_,_)=>copyCancellation?.Cancel();
            DockPanel.SetDock(cancelCopy,Dock.Right);
            copyRow.Children.Add(cancelCopy);
            var copiedMiB=copyBytesWritten/(1024.0*1024.0);
            var totalMiB=copyBytesTotal/(1024.0*1024.0);
            copyRow.Children.Add(new TextBlock{
                Text=$"Copy: {copyFilesWritten}/{copyFilesTotal} files · "+
                    $"{copiedMiB:F1}/{totalMiB:F1} MiB",
                Foreground=Brushes.White,FontSize=11,
                TextTrimming=TextTrimming.CharacterEllipsis,
                VerticalAlignment=VerticalAlignment.Center
            });
            list.Children.Add(copyRow);
        }
        if(zipInProgress)
        {
            // Display this transaction in the existing shared Deck, never
            // as a floating popup outside the circle.
            var progressRow=new DockPanel{
                Margin=new Thickness(5,2,5,4),LastChildFill=true};
            var cancel=new Button{Content="Cancel ZIP",FontSize=10,
                ToolTip="Cancel ZIP safely; the original folder is unchanged",
                Padding=new Thickness(6,2,6,2),MinWidth=75};
            cancel.Click+=(_,_)=>zipCancellation?.Cancel();
            DockPanel.SetDock(cancel,Dock.Right);progressRow.Children.Add(cancel);
            progressRow.Children.Add(new TextBlock{
                Text="ZIP: "+zipFilesWritten+" files",
                Foreground=Brushes.White,FontSize=11,
                VerticalAlignment=VerticalAlignment.Center
            });
            list.Children.Add(progressRow);
        }
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
            return;
        }
        // Opening new apps never resurrects a dismissed deck. A new
        // held or recently created file does.
        if(deckDismissed)
        {
            string currentNonApp=NonAppSignature();
            if(held.Count+detected.Count>0 &&
               !string.Equals(currentNonApp,dismissedNonAppSignature,
                   StringComparison.Ordinal))
                deckDismissed=false;
            else
            {
                popup.IsOpen=false;
                return;
            }
        }
        // Display live discovery entries AND existing manually staged files
        // on exactly the same below-circle Popup, without blocking the ring.
        detected.RemoveAll(x=>!File.Exists(x.StagedPath));
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 &&
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123!=true && !busy)
        {
            popup.IsOpen=false;
            return;
        }
        // Slim header is part of the existing floating deck. No second panel.
        var title=new DockPanel{
            Margin=new Thickness(1,0,1,3),
            Background=new SolidColorBrush(Color.FromArgb(145,34,48,79)),
            LastChildFill=true,MinHeight=26
        };
        var close=DeckHeaderButton("×",
            "Dismiss deck until a new non-app action occurs",()=>{
            dismissedNonAppSignature=NonAppSignature();
            deckDismissed=true;
            deckPreview?.Close();
            popup.IsOpen=false;
        });
        DockPanel.SetDock(close,Dock.Right);
        title.Children.Add(close);
        var previewMode=DeckHeaderButton(previewOnHover?"◉":"◎",
            previewOnHover?"Disable file preview on hover":
                "Preview files on hover (or use each row's eye button)",()=>{
                previewOnHover=!previewOnHover;
                if(!previewOnHover)deckPreview?.Close();
                Render();
            });
        DockPanel.SetDock(previewMode,Dock.Right);
        title.Children.Add(previewMode);
        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",
            deckMinimized?"Expand deck":"Minimise deck to header",()=>{
                deckMinimized=!deckMinimized;
                Render();
            });
        DockPanel.SetDock(minimise,Dock.Right);
        title.Children.Add(minimise);
        var heading=Text(
            DeskSpaceDeckHeaderV226.Format(busy,runningApps.Count,
                held.Count,detected.Count),11,Brushes.White);
        heading.Margin=new Thickness(6,0,0,0);
        // WPF ScrollViewer may measure its StackPanel with unbounded width:
        // cap header text separately from the Deck's three right buttons.
        // Ellipsis prevents the title from pushing an owned Popup across
        // the source monitor when data counts grow.
        heading.MaxWidth=Math.Max(70,panel.MaxWidth-103);
        heading.MinWidth=0;
        heading.TextTrimming=TextTrimming.CharacterEllipsis;
        heading.ToolTip="Running applications / held copies / recent files";
        title.Children.Add(heading);
        list.Children.Add(title);
        if(deckMinimized)
        {
            deckPreview?.Close();
            popup.IsOpen=host.IsVisible;
            return;
        }        // Running Apps is hover-only: first hover reveals app icons,
        // then app hover shows any additional windows.
        DeskSpaceRunningAppsV88.AddHoverGroupLive(list,runningApps,runningWindows,
            (appsNow,windowsNow)=>{
                if(disposed)return;
                runningApps=appsNow;
                runningWindows=windowsNow;
                heading.Text=DeskSpaceDeckHeaderV226.Format(
                    busy,appsNow.Count,held.Count,detected.Count);
                if(appsNow.Count==0)Render();
            },
            ()=>{
                if(disposed||!appRenderPending)return;
                appRenderPending=false;
                // Let WPF close the popup HWND before touching the deck.
                host.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    Render();
                    RefreshRunningApps();
                }));
            });
        var activeMedia=DeskSpaceMediaV18.ActiveV123;
        if(activeMedia?.DeckDismissedV272==true)
        {
            // Close removes transport buttons; only an explicit click
            // restores them for the same track. Keep a compact affordance
            // instead of forcing the user to wait for a new song to begin.
            var restore=new Button{
                Content="♫  Show media controls",
                ToolTip="Restore the closed Media Player controls without playing music",
                FontSize=11,Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(4,3,4,3)
            };
            System.Windows.Automation.AutomationProperties.SetName(
                restore,"Restore media Deck controls");
            restore.Click+=(_,_)=>{
                if(activeMedia.RestoreDeckV272())Render();
            };
            list.Children.Add(restore);
        }
        if(activeMedia?.DeckVisibleV123==true)
        {
            var mediaPanel=new StackPanel{Margin=new Thickness(3,3,3,5)};
            var header=new DockPanel{LastChildFill=true};
            var mediaCloseButtonV123=new Button{Content="×",FontSize=13,
                MinWidth=23,Padding=new Thickness(3,1,3,1),
                ToolTip="Close media controls without stopping playback"};
            mediaCloseButtonV123.Click+=(_,_)=>{activeMedia.DismissPanelV123();Render();};
            DockPanel.SetDock(mediaCloseButtonV123,Dock.Right);
            header.Children.Add(mediaCloseButtonV123);
            header.Children.Add(Text("♫ Media Player",12,Brushes.White));
            mediaPanel.Children.Add(header);
            var track=Text(activeMedia.DeckTitleV123,11,Brushes.White);
            track.ToolTip=activeMedia.DeckTitleV123;
            mediaPanel.Children.Add(track);
            if(!string.IsNullOrWhiteSpace(activeMedia.DeckArtistV123))
                mediaPanel.Children.Add(Text(activeMedia.DeckArtistV123,10,
                    Brushes.LightSteelBlue));
            var transport=new WrapPanel{
                HorizontalAlignment=HorizontalAlignment.Center,
                Margin=new Thickness(0,4,0,0)};
            void Action(string label,string command,string tooltip)
            {
                var b=new Button{Content=label,MinWidth=40,Height=29,
                    Padding=new Thickness(3),Margin=new Thickness(2),
                    ToolTip=tooltip};
                b.Click+=(_,_)=>activeMedia.DeckCommandV123(command);
                transport.Children.Add(b);
            }
            Action("«","previous","Previous track");
            Action(activeMedia.DeckPlayingV123?"Ⅱ":"▶","toggle","Play / Pause");
            Action("»","next","Next track");
            Action("Mute","mute","Mute or unmute system audio");
            Action("■","stop","Stop playback");
            mediaPanel.Children.Add(transport);
            list.Children.Add(new Border{
                Background=new SolidColorBrush(Color.FromRgb(39,53,83)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(72,88,125)),
                BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(7),Child=mediaPanel,
                Margin=new Thickness(2,3,2,4)
            });
        }
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
            excelExpandedV122,()=>{
                excelExpandedV122=!excelExpandedV122;Render();
            });
        if(excelExpandedV122)
            foreach(var suggestion in excelMatchesV122.ToArray())
            {
                var id=suggestion.Id;
                var action=new Button{
                    Content="Review & paste · "+suggestion.Name+" ("+
                        suggestion.Writable+" values)",
                    ToolTip="Verified Excel header match · preview again and "+
                        "confirm before writing; never automatic",
                    FontSize=11,Padding=new Thickness(6,5,6,5),
                    Margin=new Thickness(1,2,1,2),
                    HorizontalContentAlignment=HorizontalAlignment.Left
                };
                action.Click+=(_,_)=>ReviewExcelSuggestionV122(id);
                suggestionGroup.Children.Add(action);
            }
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
            held.Count,heldExpanded,()=>{
                heldExpanded=!heldExpanded;Render();
            });
        if(heldExpanded && held.Count>0)
        {
            var clearCopies=new Button{Content="Clear copies",FontSize=11,
                HorizontalAlignment=HorizontalAlignment.Right,
                Padding=new Thickness(5,2,5,2),Margin=new Thickness(2),
                ToolTip="Discard held copies only; originals remain untouched"};
            clearCopies.Click+=(_,_)=>ClearAll();
            heldGroup.Children.Add(clearCopies);
        }
        var recentGroup=DeskSpaceShelfGroupsV88.Add(list,"Recent Files",
            detected.Count,recentExpanded,()=>{
                recentExpanded=!recentExpanded;Render();
            });
        if(recentExpanded && detected.Count>0)
        {
            var dismissRecent=new Button{Content="Dismiss new",FontSize=11,
                HorizontalAlignment=HorizontalAlignment.Right,
                Padding=new Thickness(5,2,5,2),Margin=new Thickness(2),
                ToolTip="Dismiss only recent suggestions; never delete original files"};
            dismissRecent.Click+=(_,_)=>{
                DeskSpaceRecentFileStateV99.DismissMany(
                    detected.Select(x=>x.StagedPath).ToArray());
                deckPreview?.Close();
                detected.Clear();Render();
            };
            recentGroup.Children.Add(dismissRecent);
        }
        bool recentHeading=false;
        foreach(var item in
            (heldExpanded?held:Enumerable.Empty<Held>()).Concat(
             recentExpanded?detected:Enumerable.Empty<Held>()).ToArray())
        {
            if(item.AutoDetected && !recentHeading)
            {
                recentHeading=true;
                // The "Recent Files" group already identifies these rows.
            }
            var row=new Border{
                CornerRadius=new CornerRadius(8),
                Background=new SolidColorBrush(Color.FromArgb(145,39,50,84)),
                Padding=new Thickness(6,5,3,5),
                Margin=new Thickness(1,2,1,2),
                ToolTip=item.AutoDetected
                    ?"New file; drag onto DeskSpace circle to hold a copy"
                    :"Held copy; drag to another application or click Copy"
            };
            var content=new DockPanel {LastChildFill=true};
            var remove=new Button{Content="×",FontSize=13,MinWidth=22,Padding=new Thickness(1),
                ToolTip=item.AutoDetected
                    ?"Dismiss this suggestion (original file stays)"
                    :"Discard temporary copy"};
            remove.Click+=(_,_)=>Remove(item);
            DockPanel.SetDock(remove,Dock.Right);content.Children.Add(remove);
            var previewFile=new Button{Content="◉",FontSize=12,MinWidth=25,
                Padding=new Thickness(1),ToolTip="Preview this file without opening it"};
            previewFile.Foreground=Brushes.LightSteelBlue;
            previewFile.Background=Brushes.Transparent;
            previewFile.BorderThickness=new Thickness(0);
            previewFile.ToolTip="Preview file in the compact deck";
            deckPreview?.Bind(previewFile,row,item.StagedPath,()=>previewOnHover);
            DockPanel.SetDock(previewFile,Dock.Right);
            content.Children.Add(previewFile);
            // Open the original location, without importing or moving data.
            if(item.AutoDetected)
            {
                var holdCopy=new Button{Content="+",FontSize=13,MinWidth=21,
                    Foreground=Brushes.LightSteelBlue,
                    Background=Brushes.Transparent,BorderThickness=new Thickness(0),
                    Padding=new Thickness(1),
                    ToolTip="Hold a copy of this recently saved file"};
                holdCopy.Click+=(sender,args)=>{
                    _=StageFilesAsync(new[]{item.StagedPath});
                };
                DockPanel.SetDock(holdCopy,Dock.Right);
                content.Children.Add(holdCopy);
            }
            var locate=new Button{Content="⌕",FontSize=13,MinWidth=22,
                Foreground=Brushes.LightSteelBlue,
                Background=Brushes.Transparent,BorderThickness=new Thickness(0),
                Padding=new Thickness(1),
                ToolTip="Show this file in File Explorer"};
            locate.Click+=(_,_)=>DeskSpaceDeckPreviewV99.OpenContainingFolder(item.StagedPath);
            DockPanel.SetDock(locate,Dock.Right);
            content.Children.Add(locate);
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
            copy.Click+=(_,_)=>CopyToClipboard(item);
            DockPanel.SetDock(copy,Dock.Right);content.Children.Add(copy);
            var fileInfo=new StackPanel{Orientation=Orientation.Horizontal};
            var ico=DeskSpaceShellIconV14.Get(item.StagedPath);
            if(ico!=null)fileInfo.Children.Add(new Image{Source=ico,Width=18,Height=18,
                Margin=new Thickness(0,0,6,0)});
            fileInfo.Children.Add(Text(
                (item.AutoDetected?"Recent · ":"Held · ")+item.Name,
                12,Brushes.White));
            content.Children.Add(fileInfo);row.Child=content;
            row.MouseEnter+=(_,_)=>row.Background=new SolidColorBrush(
                Color.FromArgb(217,67,82,137));
            row.MouseLeave+=(_,_)=>row.Background=new SolidColorBrush(
                Color.FromArgb(145,39,50,84));
            Point origin=new();
            row.PreviewMouseLeftButtonDown+=(_,e)=>origin=e.GetPosition(row);
            row.PreviewMouseMove+=(_,e)=>{
                if(busy||e.LeftButton!=MouseButtonState.Pressed)return;
                var point=e.GetPosition(row);
                if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance &&
                   Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                var child=e.OriginalSource as DependencyObject;
                while(child!=null){
                    if(child is Button)return;
                    child=VisualTreeHelper.GetParent(child);
                }
                var data=new DataObject(DataFormats.FileDrop,new[]{item.StagedPath});
                var effect=DragDrop.DoDragDrop(row,data,DragDropEffects.Copy);
                // A successful drop effect is not a completed destination read.
                // Retain the staged file until explicit Clear or Remove.
                _=effect;
            };
            (item.AutoDetected?recentGroup:heldGroup).Children.Add(row);
        }
        popup.IsOpen=DeskSpaceSearchDeckLayoutV225.ShowDeck(
            DeskSpaceDeckVisibilityV89.Visible,host.IsVisible,
            searchActiveV225,true);
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        Views.Remove(this);
        // The media player stays inside the Deck until the FINAL widget
        // goes away. Closing monitor 1 must not open an extra floating
        // player over monitor 2. Preserve other teardown hooks.
        DeskSpaceMediaV18.DeckIntegrationV123=Views.Count>0;
        if(Views.Count==0)pendingDuplicateV211?.Decision.TrySetResult(false);
        if(ReferenceEquals(active,this))active=Views.FirstOrDefault();
        popup.IsOpen=false;
        recentRenderTimer.Stop();
        runningAppsTimer.Stop();
        excelSuggestTimerV122.Stop();
        mediaDeckTimerV123.Stop();
        // R243: no per-widget global reset here. Another monitor may
        // still have its own active Deck. Recompute after Views.Remove.
        deckPreview?.Dispose();
        deckPreview=null;
        DeskSpaceDeckVisibilityV89.Changed-=DeckVisibilityChanged;
        newFiles?.Dispose();
        newFiles=null;
        host.PreviewDragOver-=DragOver;
        host.PreviewDrop-=Receive;
        host.LocationChanged-=Position;
        host.SizeChanged-=Position;
        host.IsVisibleChanged-=Visible;
        host.Closed-=Closed;
        // Retained bytes survive restart; Remove and Clear explicitly discard
        // only the staged copy, not its original source path.
        // Keep staged bytes after app shutdown so delayed external paste reads
        // cannot unexpectedly see an empty/missing file. User can Clear.
    }
}
