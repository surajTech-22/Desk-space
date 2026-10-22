using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DeskSpace;

// Shared, Information-style radial sector geometry for Excel and future
// modules. Every branch uses the MAIN wheel's centre, not a new floating
// bubble centre; a depth is a narrow, source-proportional annular arc.
internal static class DeskSpaceSubArcGeometryV48
{
    internal const double CX=325,CY=325;
    internal const double HostOuter=195;
    internal const double Gap=7;
    internal const double Band=53;
    internal const double BetweenBands=2;
    internal const double RootInner=HostOuter+Gap;
    internal static double Inner(int depth)
    {
        if(depth<0||depth>8)throw new ArgumentOutOfRangeException(nameof(depth));
        return RootInner+depth*(Band+BetweenBands);
    }
    internal static double Outer(int depth)=>Inner(depth)+Band;
    internal static double Half(int count)=>count<=1?17:Math.Min(57,12.0*count);
    internal static Point Polar(double radius,double angle)
    {
        double a=angle*Math.PI/180;
        return new Point(radius*Math.Cos(a),radius*Math.Sin(a));
    }
    private static Point At(double radius,double angle)
    {
        Point p=Polar(radius,angle);
        return new Point(CX+p.X,CY+p.Y);
    }
    internal static Geometry Sector(int depth,double from,double to)
    {
        double r0=Inner(depth),r1=Outer(depth);
        if(to<=from||to-from>180)
            throw new ArgumentOutOfRangeException(nameof(to),
                "A submenu wedge cannot be a full 360-degree overlay.");
        var figure=new PathFigure{StartPoint=At(r1,from),IsClosed=true};
        figure.Segments.Add(new ArcSegment{
            Point=At(r1,to),Size=new Size(r1,r1),
            SweepDirection=SweepDirection.Clockwise,
            IsLargeArc=to-from>180});
        figure.Segments.Add(new LineSegment(At(r0,to),true));
        figure.Segments.Add(new ArcSegment{
            Point=At(r0,from),Size=new Size(r0,r0),
            SweepDirection=SweepDirection.Counterclockwise,
            IsLargeArc=to-from>180});
        return new PathGeometry(new[]{figure});
    }
    internal static Geometry Clip(Geometry sector,Rect bounds,double margin=10)
    {
        var geometry=sector.Clone();
        geometry.Transform=new TranslateTransform(-bounds.Left+margin,
                                                   -bounds.Top+margin);
        return geometry;
    }
    internal static Rect Bounds(IReadOnlyList<Geometry> list)
    {
        Rect bounds=Rect.Empty;
        foreach(var g in list)bounds.Union(g.Bounds);
        if(bounds.IsEmpty)throw new ArgumentException("No arc sectors to draw");
        return bounds;
    }
    internal static Point PopupOffset(Window owner,Rect bounds,double margin=10)
    {
        // Same owner-local scaling and Popup.Position semantics used by the
        // working Information arc, but with TIGHT bounds to stop WPF moving
        // oversized popup windows to unrelated screen edges.
        return DeskSpaceDonutGeometryV43.PopupOffset(owner,CX,CY,
            bounds.Left-margin,bounds.Top-margin);
    }
    internal static double FitAngle(Window owner,int depth,
        double preferred,double half)
    {
        double scale=DeskSpaceDonutGeometryV43.Scale(owner);
        double w=owner.ActualWidth>0?owner.ActualWidth:owner.Width;
        double h=owner.ActualHeight>0?owner.ActualHeight:owner.Height;
        Rect work=SystemParameters.WorkArea;
        double ox=owner.Left+w/2,oy=owner.Top+h/2;
        if(!double.IsFinite(ox)||!double.IsFinite(oy))
        {
            ox=work.Left+work.Width/2;
            oy=work.Top+work.Height/2;
        }
        return FitAngleAt(depth,preferred,half,new Point(ox,oy),work,scale);
    }
    // Testable screen-edge policy. Every sample along the visible arc is
    // checked, including cardinal extrema between the two endpoints.
    internal static double FitAngleAt(int depth,double preferred,double half,
        Point centre,Rect work,double scale)
    {
        double[] shifts={0,-10,10,-20,20,-30,30,-40,40};
        double best=preferred,bestScore=double.PositiveInfinity;
        foreach(double shift in shifts)
        {
            double candidate=preferred+shift;
            double penalty=Math.Abs(shift)*1.5;
            for(int n=0;n<=16;n++)
            {
                double angle=candidate-half+(2*half*n/16.0);
                var p=Polar(Outer(depth),angle);
                double x=centre.X+p.X*scale;
                double y=centre.Y+p.Y*scale;
                const double margin=12;
                penalty+=80*(Math.Max(0,work.Left+margin-x)+
                    Math.Max(0,x-(work.Right-margin))+
                    Math.Max(0,work.Top+margin-y)+
                    Math.Max(0,y-(work.Bottom-margin)));
            }
            if(penalty<bestScore){bestScore=penalty;best=candidate;}
        }
        return best;
    }

}
