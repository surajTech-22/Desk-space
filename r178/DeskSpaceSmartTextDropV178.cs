using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskSpace;

// R178 action choices render in the originating widget HWND only. Pure
// data classification refuses files/unknown formats/oversized payloads;
// text is read only during a user-initiated drag over the circle.
internal static class DeskSpaceSmartTextDropV178
{
    internal const int MaxTextChars=262144;
    internal static bool TryExtract(IDataObject data,out string text,out bool url)
    {
        text="";url=false;
        if(data.GetDataPresent(DataFormats.FileDrop))return false;
        try
        {
            object? raw=data.GetDataPresent(DataFormats.UnicodeText)?
                data.GetData(DataFormats.UnicodeText):
                (data.GetDataPresent(DataFormats.Text)?
                    data.GetData(DataFormats.Text):null);
            if(raw is not string candidate ||
                candidate.Length==0 || candidate.Length>MaxTextChars)
                return false;
            text=candidate;
            url=IsWebUrl(candidate);
            return true;
        }
        catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException or
            InvalidOperationException or OutOfMemoryException)
        {
            return false;
        }
    }
    internal static bool IsWebUrl(string text)
    {
        var s=text.Trim();
        return s.Length<=2048 &&
            Uri.TryCreate(s,UriKind.Absolute,out var uri) &&
            (uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps)&&
            uri.Host.Length>0 && s.StartsWith(uri.Scheme+"://",
                StringComparison.OrdinalIgnoreCase);
    }
    // Never run from a DragOver event. Requires explicit valid left/right
    // release from PreviewDrop and runs in a later Dispatcher callback.
    internal static void Commit(Window owner,string text,bool isUrl,bool left)
    {
        try
        {
            if(left && isUrl)
            {
                if(!IsWebUrl(text))return;
                Process.Start(new ProcessStartInfo(text.Trim()){
                    UseShellExecute=true});
                return;
            }
            if(!left)
            {
                Clipboard.SetText(text,TextDataFormat.UnicodeText);
                return;
            }
            var dialog=new SaveFileDialog{
                Title="Save dropped text",
                FileName="DeskSpace-dropped-text.txt",
                Filter="Text document (*.txt)|*.txt",
                DefaultExt=".txt",AddExtension=true,
                OverwritePrompt=true,
                InitialDirectory=Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),"Downloads")
            };
            if(dialog.ShowDialog(owner)==true)
                File.WriteAllText(dialog.FileName,text,
                    new System.Text.UTF8Encoding(false));
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or
            System.Runtime.InteropServices.COMException)
        {
            System.Diagnostics.Debug.WriteLine("Smart drop action failed: "+
                ex.GetType().Name);
            MessageBox.Show(owner,
                "Action did not complete ("+ex.GetType().Name+
                "). Source content was not modified.",
                "DeskSpace Smart Drop",MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

internal sealed class DeskSpaceSmartTextAdornerV178 : Adorner
{
    private bool? hoverLeft;
    private readonly bool web;
    internal DeskSpaceSmartTextAdornerV178(UIElement element,bool isWeb):
        base(element)
    {
        web=isWeb;IsHitTestVisible=false;
    }
    internal void Hover(Point local)
    {
        var next=DeskSpaceFolderDragAdornerV160.ChoiceAt(
            AdornedElement.RenderSize,local);
        if(next==hoverLeft)return;
        hoverLeft=next;
        InvalidateVisual();
    }
    private static Geometry Half(Point centre,double radius,bool left)
    {
        var g=new StreamGeometry();
        using(var ctx=g.Open())
        {
            ctx.BeginFigure(centre,true,true);
            ctx.LineTo(new Point(centre.X,centre.Y-radius),true,false);
            ctx.ArcTo(new Point(centre.X,centre.Y+radius),
                new Size(radius,radius),0,false,
                left?SweepDirection.Counterclockwise:
                SweepDirection.Clockwise,true,false);
        }
        g.Freeze();
        return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size=AdornedElement.RenderSize;
        if(size.Width<=0||size.Height<=0)return;
        var radius=DeskSpaceFolderDragAdornerV160.Diameter(size)/2;
        var centre=new Point(size.Width/2,size.Height/2);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(248,23,34,71)),
            new Pen(Brushes.White,1.5),centre,radius,radius);
        foreach(var pair in new[]{
            (Left:true,Label:web?"Open":"Save"),
            (Left:false,Label:"Copy")})
        {
            var fill=new SolidColorBrush(pair.Left==hoverLeft?
                Color.FromRgb(82,112,195):
                (pair.Left?Color.FromRgb(52,75,145):
                    Color.FromRgb(67,86,159)));
            fill.Freeze();
            dc.DrawGeometry(fill,null,Half(centre,radius,pair.Left));
            var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var label=new FormattedText(pair.Label,
                CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),radius<53?11:13,
                Brushes.White,dpi);
            dc.DrawText(label,new Point(centre.X+
                (pair.Left?-radius*0.5:radius*0.5)-label.Width/2,
                centre.Y-label.Height/2));
        }
        dc.DrawLine(new Pen(Brushes.Gainsboro,1.5),
            new Point(centre.X,centre.Y-radius),
            new Point(centre.X,centre.Y+radius));
        dc.DrawEllipse(null,new Pen(Brushes.White,1.8),
            centre,radius,radius);
    }
}
