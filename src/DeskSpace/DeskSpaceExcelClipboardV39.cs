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
    private readonly List<DeskSpaceExcelTemplateV16> clips=new();
    private HwndSource? source;
    private IntPtr hwnd;
    private bool disposed,ownClipboardWrite,manualOpen,userHidden,collapsed,dragging,customPosition,captureBusy;
    private Point dragStart;
    private double dragOffsetX,dragOffsetY;
    private FrameworkElement? dragHeader;
    private static DeskSpaceExcelClipboardV39? active;
    private int reposition;
    public static void CaptureNow()=>active?.CaptureSelection(false);
    public static void ShowActive()
    {
        if(active==null)return;
        active.manualOpen=true;
        active.userHidden=false;
        active.Render();
    }

    public DeskSpaceExcelClipboardV39(Window hostWindow)
    {
        host=hostWindow;active=this;
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
        if(message==WM_CLIPBOARDUPDATE&&!disposed&&!ownClipboardWrite)
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
            if(disposed || entry==null || entry.Cells.Count==0)return;
            entry.Cells.ForEach(c=>c.Role="Value");
            // An automatic event for the same selection should not duplicate
            // the shelf, but a deliberate second Copy should bring it forward.
            if(clips.Count>0 && clips[0].SourceWorkbook==entry.SourceWorkbook &&
               clips[0].SourceSheet==entry.SourceSheet &&
               clips[0].SourceAddress==entry.SourceAddress &&
               clips[0].Cells.Count==entry.Cells.Count &&
               clips[0].Cells.Zip(entry.Cells,(x,y)=>x.RawValue==y.RawValue).All(x=>x))
                return;
            clips.Insert(0,entry);
            if(clips.Count>8)clips.RemoveAt(clips.Count-1);
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
    public void Enable(){if(!disposed)Render();}
    private void Reposition(object? sender,EventArgs e)
    {
        if(disposed||customPosition)return;
        var working=SystemParameters.WorkArea;
        // Begin alongside the ordinary Files shelf. The user can move this
        // clipboard freely afterwards; window movement preserves offsets.
        popup.HorizontalOffset=host.Left+host.Width+315<working.Right ? 314 : -315;
    }
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(!disposed)popup.IsOpen=!userHidden && (bool)e.NewValue && (clips.Count>0||manualOpen);}
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
        if(clips.Count==0&&!manualOpen){popup.IsOpen=false;return;}
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
        var clear=Button("Clear",()=>{clips.Clear();manualOpen=true;Render();});
        clear.ToolTip="Delete Excel clipboard copies";
        DockPanel.SetDock(clear,Dock.Right);head.Children.Add(clear);
        var title=Label("⋮⋮ Excel clipboard · "+clips.Count,13);
        title.Cursor=Cursors.SizeAll;
        title.ToolTip="Hold and drag to move the Excel clipboard";
        title.MouseLeftButtonDown+=DragStart;
        title.MouseMove+=DragMove;
        title.MouseLeftButtonUp+=DragEnd;
        head.Children.Add(title);
        list.Children.Add(head);
        if(!collapsed && clips.Count==0)
        {
            list.Children.Add(Label("Copy a range in Microsoft Excel to collect cells here."));
            list.Children.Add(Button("Capture selected Excel cells",()=>CaptureSelection(false)));
        }
        foreach(var clip in (collapsed?Array.Empty<DeskSpaceExcelTemplateV16>():clips.ToArray()))
        {
            var row=new StackPanel{Margin=new Thickness(2,2,2,6)};
            var header=new DockPanel();
            var drop=Button("×",()=>{clips.Remove(clip);Render();});
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
        popup.IsOpen=!userHidden && host.IsVisible;
    }
    private static string Tsv(DeskSpaceExcelTemplateV16 entry)
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
    private void CopyPlain(DeskSpaceExcelTemplateV16 clip)
    {
        try {
            ownClipboardWrite=true;
            Clipboard.SetText(Tsv(clip),TextDataFormat.UnicodeText);
        }
        catch(Exception ex){
            MessageBox.Show("Cannot copy Excel cells: "+ex.Message,"DeskSpace Excel");
        }
        finally{ownClipboardWrite=false;}
    }
    private async void PasteExcel(DeskSpaceExcelTemplateV16 clip)
    {
        try
        {
            // Preview safely off UI dispatcher.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(clip,true,true);
            if(!preview.Verified||preview.Writable==0)
                throw new InvalidOperationException("No safe writable target cells. Select the starting Excel cell.");
            var answer=MessageBox.Show(
                "Paste "+preview.Writable+" values into "+
                preview.SheetName+" starting at the selected cell?\n"+
                "Existing formulas and all formatting will be preserved.\n"+
                "A workbook backup will be created before any change.",
                "DeskSpace · Paste Special Values",
                MessageBoxButton.YesNo,MessageBoxImage.Question);
            if(answer!=MessageBoxResult.Yes)return;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(preview,clip,true);
            MessageBox.Show("Values applied to the open workbook.\nBackup: "+
                backup+"\nSave the workbook in Excel after reviewing.",
                "DeskSpace Excel");
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Excel");}
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;clips.Clear();
        if(ReferenceEquals(active,this))active=null;
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
