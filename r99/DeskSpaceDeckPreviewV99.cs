using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskSpace;

// Reuses the existing V85 content preview. Anchored to the DECK's persistent
// Border, not a recreated file row: refreshing Recent Files never resizes
// the deck or destroys a currently open preview popup.
internal sealed class DeskSpaceDeckPreviewV99 : IDisposable
{
    private readonly Popup preview;
    private readonly FrameworkElement deck;
    private readonly DispatcherTimer hoverOpen=new(){
        Interval=TimeSpan.FromMilliseconds(340)
    };
    private readonly DispatcherTimer hoverClose=new(){
        Interval=TimeSpan.FromMilliseconds(500)
    };
    private bool disposed;
    private string current="";
    private Border? pendingRow;
    private string pendingPath="";
    private Func<bool>? pendingMode;
    internal bool IsOpen=>preview.IsOpen;
    internal string CurrentPath=>current;
    internal DeskSpaceDeckPreviewV99(FrameworkElement deck)
    {
        this.deck=deck;
        preview=new Popup{
            PlacementTarget=deck,Placement=PlacementMode.Right,
            HorizontalOffset=5,StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.Fade
        };
        hoverOpen.Tick+=(_,_)=>{
            hoverOpen.Stop();
            var row=pendingRow;
            if(row!=null && row.IsLoaded && row.IsMouseOver &&
               pendingMode?.Invoke()==true)
                Show(pendingPath,row);
        };
        hoverClose.Tick+=(_,_)=>{
            if(preview.Child is FrameworkElement p && p.IsMouseOver)
            {
                hoverClose.Stop();
                return;
            }
            Close();
        };
    }

    internal void Close()
    {
        hoverOpen.Stop();
        pendingRow=null;
        pendingMode=null;
        hoverClose.Stop();
        preview.IsOpen=false;
        preview.Child=null;
        current="";
    }

    internal void CloseIfMissing()
    {
        if(current.Length>0 && !File.Exists(current) && !Directory.Exists(current))
            Close();
    }

    internal void Show(string path,FrameworkElement source)
    {
        if(disposed)return;
        if(!File.Exists(path)&&!Directory.Exists(path)){Close();return;}
        hoverOpen.Stop();
        hoverClose.Stop();
        if(preview.IsOpen && string.Equals(current,path,
            StringComparison.OrdinalIgnoreCase))return;
        Close();
        current=path;
        try
        {
            var content=DeskSpaceFilePreviewV85.Create(path);
            content.MouseEnter+=(_,_)=>hoverClose.Stop();
            content.MouseLeave+=(_,_)=>ScheduleClose();
            preview.Child=content;
            // Anchor stays attached to the main deck. Only the vertical
            // position changes to correspond to the hovered file.
            try
            {
                var offset=source.TranslatePoint(new Point(0,0),deck).Y;
                preview.VerticalOffset=Math.Max(0,offset);
            }
            catch(InvalidOperationException){preview.VerticalOffset=0;}
            preview.IsOpen=true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine("DeskSpace preview unavailable: "+ex.Message);
            Close();
        }
    }

    private void ScheduleClose()
    {
        hoverClose.Stop();
        hoverClose.Start();
    }

    internal void Bind(Button button,Border row,string path,Func<bool> hoverEnabled)
    {
        button.Click+=(_,e)=>{
            e.Handled=true;
            if(preview.IsOpen && string.Equals(current,path,
                StringComparison.OrdinalIgnoreCase))
                Close();
            else Show(path,row);
        };
        row.MouseEnter+=(_,_)=>{
            hoverClose.Stop();
            if(!hoverEnabled())return;
            hoverOpen.Stop();
            pendingRow=row;
            pendingPath=path;
            pendingMode=hoverEnabled;
            hoverOpen.Start();
        };
        row.MouseLeave+=(_,_)=>{
            hoverOpen.Stop();
            if(preview.IsOpen)ScheduleClose();
        };
        row.Unloaded+=(_,_)=>{
            if(ReferenceEquals(pendingRow,row))
            {
                hoverOpen.Stop();
                pendingRow=null;
                pendingMode=null;
            }
        };
    }

    internal static void OpenContainingFolder(string path)
    {
        try
        {
            if(!File.Exists(path)&&!Directory.Exists(path))return;
            var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};
            info.ArgumentList.Add("/select,"+path);
            Process.Start(info);
        }
        catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception
            or InvalidOperationException or UnauthorizedAccessException)
        {MessageBox.Show("Could not locate file: "+ex.Message,"DeskSpace");}
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        Close();
    }
}
