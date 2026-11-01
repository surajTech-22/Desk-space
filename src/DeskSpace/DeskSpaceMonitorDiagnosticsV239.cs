using System;
using System.Collections.Generic;
using Rectangle=System.Drawing.Rectangle;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// R239: on-demand monitor diagnostics; no persistent screen observer.
// No document title, active-window content, clipboard or private path reads.
// The on-screen report is local only and is not uploaded.
internal static class DeskSpaceMonitorDiagnosticsV239
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll",SetLastError=true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);

    [DllImport("shcore.dll",PreserveSig=true)]
    private static extern int GetDpiForMonitor(IntPtr monitor,int dpiType,
        out uint dpiX,out uint dpiY);

    internal sealed record Display(string Name,bool Primary,
        Rectangle Bounds,Rectangle WorkingArea,
        int? EffectiveDpi,int DeckOffsetX,int DeckOffsetY);

    internal static string Summarize(Display display)
    {
        static string Rect(Rectangle r)=>
            r.X+", "+r.Y+"  ·  "+r.Width+" × "+r.Height+" px";
        var dpi=display.EffectiveDpi is int n && n>0?
            n+" DPI (Windows-reported)":"DPI not available";
        return (display.Primary?"Primary · ":"Secondary · ")+
            display.Name+"\n"+
            "Monitor: "+Rect(display.Bounds)+"\n"+
            "Working area: "+Rect(display.WorkingArea)+"\n"+
            dpi+"\n"+
            "Saved Deck offset: X "+display.DeckOffsetX+
            ", Y "+display.DeckOffsetY+
            " physical px (0 may mean default)";
    }

    private static int? ReportedDpi(Rectangle bounds)
    {
        try
        {
            var point=new NativePoint{
                X=bounds.Left+bounds.Width/2,
                Y=bounds.Top+bounds.Height/2};
            var monitor=MonitorFromPoint(point,2);
            if(monitor==IntPtr.Zero)return null;
            var result=GetDpiForMonitor(monitor,0,out uint x,out uint y);
            if(result!=0 || x<48 || x>960 || y<48 || y>960)return null;
            return (int)x;
        }
        catch(DllNotFoundException){return null;}
        catch(EntryPointNotFoundException){return null;}
    }

    internal static IReadOnlyList<Display> Snapshot()
    {
        var result=new List<Display>();
        Forms.Screen[] screens;
        try{screens=Forms.Screen.AllScreens;}
        catch(System.ComponentModel.Win32Exception){return result;}
        foreach(var screen in screens)
        {
            // A corrupt stored offset map reads as a temporary zero
            // (R238), without erasing the original or modifying settings.
            DeskSpaceDeckOffsetV228.Offset offset;
            try{offset=DeskSpaceDeckOffsetV228.Read(screen.DeviceName);}
            catch(Exception ex)when(ex is System.IO.IOException or
                UnauthorizedAccessException)
            {
                offset=new DeskSpaceDeckOffsetV228.Offset(0,0);
            }
            result.Add(new Display(screen.DeviceName,screen.Primary,
                screen.Bounds,screen.WorkingArea,ReportedDpi(screen.Bounds),
                offset.X,offset.Y));
        }
        return result;
    }

    internal static void Show(Window owner)
    {
        var window=new Window{
            Title="DeskSpace — Display diagnostics",
            Width=525,Height=570,MinWidth=410,MinHeight=350,
            Owner=owner,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ResizeMode=ResizeMode.CanResize};
        var content=new StackPanel{Margin=new Thickness(16)};
        content.Children.Add(new TextBlock{
            Text="Monitor and Deck position diagnostics",
            FontSize=18,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,8)});
        content.Children.Add(new TextBlock{
            Text="On-demand Windows display information. Nothing is changed, "+
                 "uploaded or monitored in the background. Reported DPI is "+
                 "advisory and does not replace physical multi-monitor testing.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,12)});
        var cards=new StackPanel();
        void Refresh()
        {
            cards.Children.Clear();
            var found=Snapshot();
            if(found.Count==0)
            {
                cards.Children.Add(new TextBlock{
                    Text="Windows did not report any connected monitors.",
                    TextWrapping=TextWrapping.Wrap});
                return;
            }
            cards.Children.Add(new TextBlock{
                Text=found.Count+" connected monitor(s)",
                FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(0,0,0,10)});
            foreach(var screen in found)
            {
                cards.Children.Add(new TextBlock{
                    Text=Summarize(screen),
                    TextWrapping=TextWrapping.Wrap,
                    Margin=new Thickness(0,0,0,12),
                    Padding=new Thickness(10),
                    Background=Brushes.GhostWhite});
            }
        }
        var refresh=new Button{
            Content="Refresh display information",
            HorizontalAlignment=HorizontalAlignment.Left,
            Padding=new Thickness(10,5,10,5),
            Margin=new Thickness(0,0,0,12)};
        refresh.Click+=(_,_)=>Refresh();
        content.Children.Add(refresh);
        content.Children.Add(cards);
        window.Content=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=content};
        Refresh();
        window.Show();
    }
}
