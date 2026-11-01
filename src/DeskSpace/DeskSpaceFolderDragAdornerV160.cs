using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DeskSpace;

// Drawn INSIDE the existing widget HWND's WPF visual tree.
// There is deliberately no Popup, Window, ContextMenu or Deck UI here.
// WPF drag/drop keeps ownership: the pointer is hit-tested only when dropped
// onto one of the two halves of the visible circle.
internal sealed class DeskSpaceFolderDragAdornerV160 : Adorner
{
    private bool? hoverZip;
    internal DeskSpaceFolderDragAdornerV160(UIElement decorated) : base(decorated)
    {
        IsHitTestVisible=false;
    }

    internal static double Diameter(Size hostSize)=>
        Math.Clamp(Math.Min(hostSize.Width,hostSize.Height)-8.0,84.0,132.0);

    internal static bool? ChoiceAt(Size hostSize,Point local)
    {
        double r=Diameter(hostSize)/2;
        double dx=local.X-hostSize.Width/2.0;
        double dy=local.Y-hostSize.Height/2.0;
        if(dx*dx+dy*dy>r*r)return null;
        return dx<0;
    }

    internal void Hover(Point local)
    {
        var next=ChoiceAt(AdornedElement.RenderSize,local);
        if(next==hoverZip)return;
        hoverZip=next;
        InvalidateVisual();
    }

    private static Geometry Half(Point centre,double r,bool left)
    {
        var top=new Point(centre.X,centre.Y-r);
        var bottom=new Point(centre.X,centre.Y+r);
        var g=new StreamGeometry();
        using(var ctx=g.Open())
        {
            ctx.BeginFigure(centre,true,true);
            ctx.LineTo(top,true,false);
            ctx.ArcTo(bottom,new Size(r,r),0,false,
                left?SweepDirection.Counterclockwise:SweepDirection.Clockwise,
                true,false);
        }
        g.Freeze();
        return g;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size=AdornedElement.RenderSize;
        if(size.Width<=0||size.Height<=0)return;
        double r=Diameter(size)/2.0;
        var center=new Point(size.Width/2.0,size.Height/2.0);
        var left=new SolidColorBrush(hoverZip==true
            ?Color.FromRgb(91,119,206):Color.FromArgb(246,54,75,145));
        var right=new SolidColorBrush(hoverZip==false
            ?Color.FromRgb(91,119,206):Color.FromArgb(246,68,87,158));
        left.Freeze();right.Freeze();

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(246,24,36,72)),
            new Pen(Brushes.White,1.4),center,r,r);
        dc.DrawGeometry(left,null,Half(center,r,true));
        dc.DrawGeometry(right,null,Half(center,r,false));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(185,238,243,255)),1.4),
            new Point(center.X,center.Y-r),new Point(center.X,center.Y+r));
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(
            Color.FromArgb(220,226,235,255)),1.8),center,r,r);

        var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach(var pair in new[]{("ZIP",-r*0.50),("Copy",r*0.50)})
        {
            var text=new FormattedText(pair.Item1,CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,new Typeface("Segoe UI Semibold"),
                r<53?11:13,Brushes.White,dpi);
            dc.DrawText(text,new Point(center.X+pair.Item2-text.Width/2,
                center.Y-text.Height/2));
        }
    }
}
