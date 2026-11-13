using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskSpace;

// Excel cells have their OWN volatile clipboard shelf. It never adds XLS
// content to the Files shelf and never writes copied cells into the vault.
internal sealed class DeskSpaceExcelClipboardV39 : IDisposable
{
    private const int WM_CLIPBOARDUPDATE=0x031D;
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr window);
    private readonly Window host;
    private readonly Popup popup;
    private readonly StackPanel list;
    internal static readonly List<DeskSpaceExcelTemplateV16> SharedClips=new();
    internal static event Action? ClipsChanged;
    internal static event Action? ShowDeckRequested;
    internal static void NotifyChanged()=>ClipsChanged?.Invoke();
    private static string lastStatus="";
    private static DateTime lastStatusUntilUtc=DateTime.MinValue;
    internal static string CurrentStatus()=>DateTime.UtcNow<=lastStatusUntilUtc?lastStatus:"";
    internal static void SetStatus(string message)
    {
        lastStatus=message??"";
        lastStatusUntilUtc=string.IsNullOrEmpty(lastStatus)?DateTime.MinValue:DateTime.UtcNow.AddSeconds(7);
        NotifyChanged();
    }
    private HwndSource? source;
    private IntPtr hwnd;
    private static bool ownClipboardWrite;
    private bool disposed,manualOpen,userHidden,collapsed,dragging,customPosition,captureBusy;
    private Point dragStart;
    private double dragOffsetX,dragOffsetY;
    private FrameworkElement? dragHeader;
    private static readonly List<DeskSpaceExcelClipboardV39> Instances=new();
    private static DeskSpaceExcelClipboardV39? active;
    private int reposition;
    public static void CaptureNow()=>CaptureSelectionNow();
    internal static async void CaptureSelectionNow()
    {
        if(active!=null)
        {
            active.CaptureSelection(false);
            return;
        }
        try
        {
            var entry=await DeskSpaceExcelWorkerV41.CaptureAsync(
                "Cells · "+DateTime.Now.ToString("HH:mm:ss"),"",true);
            if(entry==null || entry.Cells.Count==0)return;
            entry.Cells.ForEach(c=>c.Role="Value");
            RecordSharedClip(entry);
            SetStatus("Captured "+entry.Cells.Count+" cells from "+
                entry.SourceSheet+" ("+entry.SourceAddress+")");
        }
        catch(Exception ex)
        {
            MessageBox.Show(ex.Message,"DeskSpace Excel clipboard",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private static bool SameClipSnapshot(DeskSpaceExcelTemplateV16 a,DeskSpaceExcelTemplateV16 b)=>
        a.SourceWorkbook==b.SourceWorkbook &&
        a.SourceSheet==b.SourceSheet &&
        a.SourceAddress==b.SourceAddress &&
        a.Cells.Count==b.Cells.Count &&
        a.Cells.Zip(b.Cells,(x,y)=>x.RawValue==y.RawValue).All(x=>x);
    private static bool RecordSharedClip(DeskSpaceExcelTemplateV16 entry)
    {
        if(SharedClips.Count>0 && SameClipSnapshot(SharedClips[0],entry))
            return false;
        SharedClips.Insert(0,entry);
        if(SharedClips.Count>8)SharedClips.RemoveAt(SharedClips.Count-1);
        NotifyChanged();
        return true;
    }
    public static async void ShowActive()
    {
        ShowDeckRequested?.Invoke();
        try
        {
            if(SharedClips.Count==0 && active!=null && !active.disposed && !active.captureBusy)
            {
                var entry=await DeskSpaceExcelWorkerV41.CopyEventAsync();
                if(entry!=null && entry.Cells.Count>0)
                {
                    entry.Cells.ForEach(c=>c.Role="Value");
                    RecordSharedClip(entry);
                }
            }
        }
        catch{}
        if(active==null)return;
        active.manualOpen=true;
        active.userHidden=false;
        active.Render();
    }

    public DeskSpaceExcelClipboardV39(Window hostWindow)
    {
        host=hostWindow;Instances.Add(this);active=this;
        list=new StackPanel{Margin=new Thickness(8)};
        var panel=new Border{
            MinWidth=224,MaxWidth=304,
            CornerRadius=new CornerRadius(14),
            Background=new SolidColorBrush(Color.FromArgb(236,23,38,66)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(111,169,221)),
            BorderThickness=new Thickness(1),Child=list
        };
        popup=new Popup{
            PlacementTarget=host,Placement=PlacementMode.Bottom,
            VerticalOffset=6,AllowsTransparency=true,StaysOpen=true,
            PopupAnimation=PopupAnimation.Fade,Child=panel
        };
        host.SourceInitialized+=SourceReady;
        host.LocationChanged+=Reposition;
        host.SizeChanged+=Reposition;
        host.IsVisibleChanged+=Visible;
        host.Closed+=Closed;
        if(new WindowInteropHelper(host).Handle!=IntPtr.Zero)SourceReady(host,EventArgs.Empty);
    }
    private void SourceReady(object? sender,EventArgs e)
    {
        if(disposed||source!=null)return;
        source=PresentationSource.FromVisual(host) as HwndSource;
        if(source==null)return;
        hwnd=source.Handle;
        source.AddHook(ClipboardMessage);
        if(!AddClipboardFormatListener(hwnd))
        {
            source.RemoveHook(ClipboardMessage);source=null;hwnd=IntPtr.Zero;
        }
        Reposition(this,EventArgs.Empty);
    }
    private IntPtr ClipboardMessage(IntPtr h,int message,IntPtr wp,IntPtr lp,
        ref bool handled)
    {
        if(message==WM_CLIPBOARDUPDATE&&!disposed&&!ownClipboardWrite&&
           (active==null||ReferenceEquals(active,this)))
            host.Dispatcher.BeginInvoke(new Action(()=>CaptureSelection(true)));
        return IntPtr.Zero;
    }
    private async void CaptureSelection(bool clipboardEvent)
    {
        if(disposed || captureBusy)return;
        captureBusy=true;
        try
        {
            // Read native Excel data on a separate STA thread. Clipboard
            // notifications never block scrolling or WPF repainting.
            var entry=clipboardEvent
                ? await DeskSpaceExcelWorkerV41.CopyEventAsync()
                : await DeskSpaceExcelWorkerV41.CaptureAsync(
                    "Cells · "+DateTime.Now.ToString("HH:mm:ss"),"",true);
            if(clipboardEvent && !disposed && (entry==null || entry.Cells.Count==0))
            {
                await System.Threading.Tasks.Task.Delay(220);
                if(!disposed)entry=await DeskSpaceExcelWorkerV41.CopyEventAsync();
            }
            if(disposed || entry==null || entry.Cells.Count==0)return;
            entry.Cells.ForEach(c=>c.Role="Value");
            bool added=RecordSharedClip(entry);
            if(!clipboardEvent && added)
                SetStatus("Captured "+entry.Cells.Count+" cells from "+
                    entry.SourceSheet+" ("+entry.SourceAddress+")");
            else if(!clipboardEvent && !added)
                SetStatus("Selection unchanged ("+entry.SourceSheet+" · "+entry.SourceAddress+")");
            Render();
        }
        catch(Exception ex)
        {
            if(!clipboardEvent&&!disposed)
                MessageBox.Show(ex.Message,"DeskSpace Excel clipboard",
                    MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally{captureBusy=false;}
    }
    public void Enable()
    {
        if(disposed)return;
        Render();
        if(SharedClips.Count==0 && !captureBusy &&
           (active==null || ReferenceEquals(active,this)) &&
           DeskSpaceExcelActiveV122.AnyExcelOpen())
            host.Dispatcher.BeginInvoke(new Action(()=>PrefetchActiveCopyV377()));
    }
    private async void PrefetchActiveCopyV377()
    {
        if(disposed || captureBusy || SharedClips.Count>0)return;
        captureBusy=true;
        try
        {
            var entry=await DeskSpaceExcelWorkerV41.CopyEventAsync();
            if(disposed || entry==null || entry.Cells.Count==0)return;
            entry.Cells.ForEach(c=>c.Role="Value");
            RecordSharedClip(entry);
            Render();
        }
        catch{}
        finally{captureBusy=false;}
    }
    private void Reposition(object? sender,EventArgs e)
    {
        if(disposed||customPosition)return;
        var working=SystemParameters.WorkArea;
        // Begin alongside the ordinary Files shelf. The user can move this
        // clipboard freely afterwards; window movement preserves offsets.
        popup.HorizontalOffset=host.Left+host.Width+315<working.Right ? 314 : -315;
    }
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(!disposed)popup.IsOpen=false;}
    private void HideShelf()
    {
        userHidden=true;
        manualOpen=false;
        dragging=false;
        dragHeader?.ReleaseMouseCapture();
        popup.IsOpen=false; // Hide only. Retain all copied cell snapshots.
    }
    private void DragStart(object sender,MouseButtonEventArgs e)
    {
        if(disposed||e.ChangedButton!=MouseButton.Left)return;
        if(sender is not FrameworkElement handle)return;
        dragHeader=handle;
        dragStart=e.GetPosition(host);
        dragOffsetX=popup.HorizontalOffset;
        dragOffsetY=popup.VerticalOffset;
        dragging=handle.CaptureMouse();
        if(dragging)e.Handled=true;
    }
    private void DragMove(object sender,MouseEventArgs e)
    {
        if(!dragging)return;
        var now=e.GetPosition(host);
        double nextX=dragOffsetX+now.X-dragStart.X;
        double nextY=dragOffsetY+now.Y-dragStart.Y;
        // Bound movement to the visible working area, including multi-monitor
        // DPI-aware WPF device-independent coordinates of this host.
        var work=SystemParameters.WorkArea;
        popup.HorizontalOffset=Math.Clamp(nextX,
            work.Left-host.Left-host.Width,work.Right-host.Left-20);
        popup.VerticalOffset=Math.Clamp(nextY,
            work.Top-host.Top-host.Height,work.Bottom-host.Top-20);
        customPosition=true;
        e.Handled=true;
    }
    private void DragEnd(object sender,MouseButtonEventArgs e)
    {
        if(!dragging)return;
        dragging=false;
        dragHeader?.ReleaseMouseCapture();
        e.Handled=true;
    }
    private void Closed(object? sender,EventArgs e)=>Dispose();
    private static TextBlock Label(string text,int size=12)=>new(){
        Text=text,Foreground=Brushes.White,FontSize=size,
        TextTrimming=TextTrimming.CharacterEllipsis,
        VerticalAlignment=VerticalAlignment.Center
    };
    private Button Button(string text,Action action)
    {
        var b=new Button{Content=text,FontSize=11,Padding=new Thickness(6,3,6,3),
            Margin=new Thickness(2),MinHeight=25};
        b.Click+=(_,_)=>action();return b;
    }
    private void Render()
    {
        if(disposed)return;
        list.Children.Clear();
        if(SharedClips.Count==0&&!manualOpen){popup.IsOpen=false;return;}
        var head=new DockPanel{Margin=new Thickness(2,2,2,6),
            Background=new SolidColorBrush(Color.FromArgb(180,35,62,99))};
        // Close and collapse are always visible, even if zero cells exist.
        // Drag only on the title, never on actionable buttons.
        var close=Button("×",HideShelf);
        close.ToolTip="Close Excel clipboard (copied cells remain available)";
        DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
        var collapse=Button(collapsed?"+":"−",()=>{collapsed=!collapsed;Render();});
        collapse.ToolTip=collapsed?"Expand Excel clipboard":"Minimize Excel clipboard";
        DockPanel.SetDock(collapse,Dock.Right);head.Children.Add(collapse);
        var clear=Button("Clear",()=>{SharedClips.Clear();NotifyChanged();manualOpen=true;Render();});
        clear.ToolTip="Delete Excel clipboard copies";
        DockPanel.SetDock(clear,Dock.Right);head.Children.Add(clear);
        var title=Label("⋮⋮ Excel clipboard · "+SharedClips.Count,13);
        title.Cursor=Cursors.SizeAll;
        title.ToolTip="Hold and drag to move the Excel clipboard";
        title.MouseLeftButtonDown+=DragStart;
        title.MouseMove+=DragMove;
        title.MouseLeftButtonUp+=DragEnd;
        head.Children.Add(title);
        list.Children.Add(head);
        if(!collapsed && SharedClips.Count==0)
        {
            list.Children.Add(Label("Copy a range in Microsoft Excel to collect cells here."));
            list.Children.Add(Button("Capture selected Excel cells",()=>CaptureSelection(false)));
        }
        foreach(var clip in (collapsed?Array.Empty<DeskSpaceExcelTemplateV16>():SharedClips.ToArray()))
        {
            var row=new StackPanel{Margin=new Thickness(2,2,2,6)};
            var header=new DockPanel();
            var drop=Button("×",()=>{SharedClips.Remove(clip);NotifyChanged();Render();});
            DockPanel.SetDock(drop,Dock.Right);header.Children.Add(drop);
            header.Children.Add(Label(clip.SourceSheet+" · "+clip.SourceAddress));
            row.Children.Add(header);
            var caption=Label(clip.RowCount+" rows × "+clip.ColumnCount+
                " cols · "+clip.Cells.Count+" copied cells",10);
            caption.Foreground=Brushes.LightSteelBlue;
            row.Children.Add(caption);
            var actions=new WrapPanel();
            actions.Children.Add(Button("Copy values",()=>CopyPlain(clip)));
            actions.Children.Add(Button("Paste to Excel",()=>PasteExcel(clip)));
            row.Children.Add(actions);
            list.Children.Add(row);
        }
        Reposition(this,EventArgs.Empty);
        popup.IsOpen=false;
    }
    internal static string Tsv(DeskSpaceExcelTemplateV16 entry)
    {
        int rows=Math.Min(entry.RowCount,250),cols=Math.Min(entry.ColumnCount,60);
        var matrix=new string[rows,cols];
        foreach(var cell in entry.Cells)
        {
            if(cell.RowOffset<0||cell.ColumnOffset<0||
               cell.RowOffset>=rows||cell.ColumnOffset>=cols)continue;
            matrix[cell.RowOffset,cell.ColumnOffset]=cell.RawValue;
        }
        var lines=new List<string>();
        for(int r=0;r<rows;r++)
        {
            var values=new string[cols];
            for(int c=0;c<cols;c++)
            {
                // TSV/Excel clipboard: quote multiline/tab/quoted text.
                var v=matrix[r,c]??"";
                values[c]=v.IndexOfAny(new[]{'\t','\n','\r','"'})>=0 ?
                    "\""+v.Replace("\"","\"\"")+"\"" : v;
            }
            lines.Add(string.Join("\t",values));
        }
        return string.Join("\r\n",lines);
    }
    internal static string ColumnLetter(int oneBasedCol)
    {
        if(oneBasedCol<1)return "?";
        int n=oneBasedCol;
        var sb=new StringBuilder();
        while(n>0)
        {
            n--;
            sb.Insert(0,(char)('A'+(n%26)));
            n/=26;
        }
        return sb.ToString();
    }
    internal static string CellDisplayText(DeskSpaceExcelCellV16 cell)=>
        !string.IsNullOrWhiteSpace(cell.Display)?cell.Display:(cell.RawValue??"");
    internal static DeskSpaceExcelCellV16?[,] BuildCellMatrix(
        DeskSpaceExcelTemplateV16 entry,int maxRows,int maxCols)
    {
        int rows=Math.Clamp(entry.RowCount,1,maxRows);
        int cols=Math.Clamp(entry.ColumnCount,1,maxCols);
        var matrix=new DeskSpaceExcelCellV16?[rows,cols];
        foreach(var cell in entry.Cells)
        {
            if(cell.RowOffset<0||cell.ColumnOffset<0||
               cell.RowOffset>=rows||cell.ColumnOffset>=cols)continue;
            matrix[cell.RowOffset,cell.ColumnOffset]=cell;
        }
        return matrix;
    }
    internal static string[,] BuildDisplayMatrix(
        DeskSpaceExcelTemplateV16 entry,int maxRows,int maxCols)
    {
        var cells=BuildCellMatrix(entry,maxRows,maxCols);
        int rows=cells.GetLength(0),cols=cells.GetLength(1);
        var matrix=new string[rows,cols];
        for(int r=0;r<rows;r++)
            for(int c=0;c<cols;c++)
                matrix[r,c]=cells[r,c]!=null?CellDisplayText(cells[r,c]!):"";
        return matrix;
    }
    internal static string ClipSummarySnippet(DeskSpaceExcelTemplateV16 entry)
    {
        if(entry.Cells.Count==0)return "";
        int rows=Math.Clamp(entry.RowCount,1,24),cols=Math.Clamp(entry.ColumnCount,1,8);
        var matrix=BuildDisplayMatrix(entry,rows,cols);
        for(int r=0;r<rows;r++)
        {
            var items=new List<string>();
            for(int c=0;c<cols;c++)
            {
                var v=(matrix[r,c]??"").Replace("\r"," ").Replace("\n"," ").Trim();
                if(v.Length>0)items.Add(v.Length>18?v.Substring(0,17)+"…":v);
                if(items.Count>=4)break;
            }
            if(items.Count>0)
            {
                string line=string.Join(" | ",items);
                if(entry.ColumnCount>cols || items.Count<entry.ColumnCount)
                    line+=" …";
                return line.Length>66?line.Substring(0,65)+"…":line;
            }
        }
        return "(blank cells)";
    }
    internal static void CopyPlain(DeskSpaceExcelTemplateV16 clip)
    {
        try {
            ownClipboardWrite=true;
            Clipboard.SetText(Tsv(clip),TextDataFormat.UnicodeText);
            SetStatus("Copied "+clip.Cells.Count+" cells to Windows clipboard");
        }
        catch(Exception ex){
            MessageBox.Show("Cannot copy Excel cells: "+ex.Message,"DeskSpace Excel");
        }
        finally{ownClipboardWrite=false;}
    }
    internal static async void PasteExcel(DeskSpaceExcelTemplateV16 clip)
    {
        try
        {
            // Preview safely off UI dispatcher using active Excel selection.
            var preview=await DeskSpaceExcelWorkerV41.PreviewClipboardAsync(clip,true);
            if(!preview.Verified||preview.Writable==0)
                throw new InvalidOperationException("No writable target cells at the active Excel selection. Select a non-formula cell in Excel and try again.");
            string bookName=string.IsNullOrWhiteSpace(preview.WorkbookFullName)
                ? "" : Path.GetFileName(preview.WorkbookFullName);
            string targetCell=preview.Changes.FirstOrDefault(x=>x.CanApply)?.CellAddress??"";
            string destDesc=(bookName.Length>0?bookName+" · ":"")+
                preview.SheetName+(targetCell.Length>0?" ("+targetCell+")":"");
            var answer=MessageBox.Show(
                "Paste "+preview.Writable+" values into "+destDesc+"?\n"+
                "Existing formulas will be preserved and a workbook backup will be saved first.",
                "DeskSpace · Paste to Excel",
                MessageBoxButton.YesNo,MessageBoxImage.Question);
            if(answer!=MessageBoxResult.Yes)return;
            await DeskSpaceExcelWorkerV41.ApplyClipboardAsync(preview,clip,true);
            SetStatus("Pasted "+preview.Writable+" cells to "+destDesc+" · backup saved");
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Excel");}
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        Instances.Remove(this);
        if(ReferenceEquals(active,this))active=Instances.Count>0?Instances[Instances.Count-1]:null;
        popup.IsOpen=false;
        host.SourceInitialized-=SourceReady;
        host.LocationChanged-=Reposition;
        host.SizeChanged-=Reposition;
        host.IsVisibleChanged-=Visible;
        host.Closed-=Closed;
        if(source!=null){source.RemoveHook(ClipboardMessage);source=null;}
        if(hwnd!=IntPtr.Zero){RemoveClipboardFormatListener(hwnd);hwnd=IntPtr.Zero;}
    }
}
