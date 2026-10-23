using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Effects;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskSpace;

// Reusable, module-agnostic donut paging, radius, and scroll appearance.
// Every module provides its items, parent anchor and its launch/edit action.
// The engine never loads application or accounting data by itself.
internal static class DeskSpaceSubDonutStandardV62
{
    internal const int Visible=5;
    internal const double ParentSweep=360.0/7.0;
    internal const int DurationMs=155;
    // Source of truth for hidden-count dots AND the wedge divider gap.
    internal const double HiddenCountDotOffset=13.0;
    internal const double HiddenCountDotDiameter=3.8;
    internal const double HiddenCountGapHalf=HiddenCountDotOffset+HiddenCountDotDiameter/2.0;

    internal readonly record struct View(int Total,int Start,int Count,
        int HiddenLeft,int HiddenRight,double Center,double Half,double Step)
    {
        internal double From(int index)=>Center-Half+Step*index;
        internal double To(int index)=>From(index)+Step;
    }

    // One parent plus half of each adjacent parent's width. At root this is
    // 102.857° maximum. Reused for child-of-child arcs with parentSweep
    // equal to the selected child's own angular width.
    internal static View Plan(int total,int start,double center,
        double parentSweep=ParentSweep)
    {
        if(total<0)throw new ArgumentOutOfRangeException(nameof(total));
        if(parentSweep<=0||parentSweep>180)
            throw new ArgumentOutOfRangeException(nameof(parentSweep));
        int count=Math.Min(total,Visible);
        int maxStart=Math.Max(0,total-count);
        start=Math.Clamp(start,0,maxStart);
        double half=count<=1?Math.Min(17,parentSweep):
            Math.Min(12.0*count,parentSweep);
        return new View(total,start,count,start,
            Math.Max(0,total-start-count),center,half,
            count==0?0:2*half/count);
    }

    // Mouse wheels can report detents of ±120 or fine-grained touchpad deltas.
    // Exactly one item shifts for each full detent; small deltas accumulate.
    internal sealed class Wheel
    {
        private int remainder;
        internal int Step(int delta)
        {
            if(delta==0)return 0;
            if(Math.Sign(delta)!=Math.Sign(remainder))remainder=0;
            remainder+=delta;
            int step=0;
            while(Math.Abs(remainder)>=120)
            {
                step+=remainder<0?1:-1;
                remainder+=remainder<0?120:-120;
            }
            return step;
        }
        internal void Reset()=>remainder=0;
    }

    internal static int Advance(View view,int delta)=>
        Math.Clamp(view.Start+delta,0,Math.Max(0,view.Total-view.Count));

    // Universal hidden-count design: the numeral interrupts the VERTICAL
    // radial divider at the end of the visible arc. Never add an exterior
    // badge, a mini wedge or a top-of-canvas label. This uses the actual WPF
    // sector geometry, so it works at any submenu angle, radius and depth.
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

    // Start side: first arc's OUTER start to the second arc's INNER end.
    // End side: first arc's OUTER end to the straight segment's INNER end.
    // Do not use the geometry bounding rectangle: its corners aren't on the
    // wedge divider, which caused the previous detached counters.
    internal static Point DividerMidpoint(Geometry shape,bool leading)
    {
        if(shape is not PathGeometry pg || pg.Figures.Count==0)
            throw new ArgumentException("Sub-donut wedge must use arc geometry",
                nameof(shape));
        var figure=pg.Figures[0];
        if(figure.Segments.Count<3 ||
            figure.Segments[0] is not ArcSegment outerArc ||
            figure.Segments[1] is not LineSegment radialLine ||
            figure.Segments[2] is not ArcSegment innerArc)
            throw new ArgumentException("Unexpected sub-donut arc segments",
                nameof(shape));
        Point outer=leading?figure.StartPoint:outerArc.Point;
        Point inner=leading?innerArc.Point:radialLine.Point;
        var transform=shape.Transform;
        if(transform!=null)
        {
            outer=transform.Transform(outer);
            inner=transform.Transform(inner);
        }
        return new Point((outer.X+inner.X)/2,(outer.Y+inner.Y)/2);
    }


    // Radial divider direction in the on-screen (possibly clipped) canvas.
    // Normalize to an upright half-turn: the COUNT follows the divider,
    // without becoming upside-down as the menu rotates around the circle.
    internal static double DividerAngle(Geometry shape,bool leading)
    {
        if(shape is not PathGeometry pg || pg.Figures.Count==0 ||
           pg.Figures[0].Segments.Count<3 ||
           pg.Figures[0].Segments[0] is not ArcSegment outerArc ||
           pg.Figures[0].Segments[1] is not LineSegment radialLine ||
           pg.Figures[0].Segments[2] is not ArcSegment innerArc)
            throw new ArgumentException("Unexpected hidden-count divider geometry",
                nameof(shape));
        Point outer=leading?pg.Figures[0].StartPoint:outerArc.Point;
        Point inner=leading?innerArc.Point:radialLine.Point;
        if(shape.Transform is Transform transform)
        {
            outer=transform.Transform(outer);
            inner=transform.Transform(inner);
        }
        double angle=Math.Atan2(inner.Y-outer.Y,inner.X-outer.X)
            *180.0/Math.PI-90.0;
        while(angle>90)angle-=180;
        while(angle< -90)angle+=180;
        return angle;
    }

    // The wedge outline is a single closed shape, so its radial stroke
    // cannot be hidden locally without also painting a rectangle over the
    // sector. Preserve the fill path for mouse input, and replace ONLY the
    // outline with equivalent open strokes. The gap ends at the DOTS,
    // rather than leaving blank intervals between the divider and dots.
    private static void SplitDividerStroke(Canvas canvas,
        System.Windows.Shapes.Path wedge,bool leading,bool trailing)
    {
        if(wedge.Stroke==null || wedge.StrokeThickness<=0 ||
           wedge.Data is not PathGeometry shape || shape.Figures.Count==0)
            return;
        var figure=shape.Figures[0];
        if(figure.Segments.Count<3 ||
           figure.Segments[0] is not ArcSegment outerArc ||
           figure.Segments[1] is not LineSegment radialLine ||
           figure.Segments[2] is not ArcSegment innerArc)
            throw new ArgumentException("Unexpected sub-donut divider geometry");

        var outline=new PathGeometry();
        void AddLine(Point a,Point b)
        {
            var segment=new PathFigure{
                StartPoint=a,IsClosed=false,IsFilled=false};
            segment.Segments.Add(new LineSegment(b,true));
            outline.Figures.Add(segment);
        }
        void AddArc(Point start,ArcSegment arc)
        {
            var segment=new PathFigure{
                StartPoint=start,IsClosed=false,IsFilled=false};
            segment.Segments.Add((ArcSegment)arc.CloneCurrentValue());
            outline.Figures.Add(segment);
        }
        void AddRadial(Point outer,Point inner,bool split)
        {
            if(!split){AddLine(outer,inner);return;}
            double dx=inner.X-outer.X,dy=inner.Y-outer.Y;
            double len=Math.Sqrt(dx*dx+dy*dy);
            if(len<1){AddLine(outer,inner);return;}
            // The gap is along the divider. A numeral and TWO small dots
            // are arranged along it, never in a separate floating badge.
            double half=Math.Min(HiddenCountGapHalf,len*.36);
            double ux=dx/len,uy=dy/len;
            var middle=new Point((outer.X+inner.X)/2,
                                 (outer.Y+inner.Y)/2);
            AddLine(outer,new Point(middle.X-ux*half,middle.Y-uy*half));
            AddLine(new Point(middle.X+ux*half,middle.Y+uy*half),inner);
        }
        AddArc(figure.StartPoint,outerArc);
        AddArc(radialLine.Point,innerArc);
        AddRadial(figure.StartPoint,innerArc.Point,leading);
        AddRadial(outerArc.Point,radialLine.Point,trailing);
        outline.Transform=shape.Transform?.CloneCurrentValue()
            ??Transform.Identity;
        var border=new System.Windows.Shapes.Path{
            Data=outline,
            Fill=null,
            Stroke=wedge.Stroke,
            StrokeThickness=wedge.StrokeThickness,
            StrokeLineJoin=wedge.StrokeLineJoin,
            StrokeStartLineCap=wedge.StrokeStartLineCap,
            StrokeEndLineCap=wedge.StrokeEndLineCap,
            StrokeDashArray=wedge.StrokeDashArray,
            IsHitTestVisible=false
        };
        // Only the original sector's outline changes. Do not add a label
        // background or an overlay rectangle, including at nested levels.
        wedge.Stroke=null;
        Canvas.SetZIndex(border,39);
        canvas.Children.Add(border);
    }

    // WCAG-style luminance keeps the numeral high contrast without a stark
    // badge. For gradients, choose the ink with the stronger *minimum*
    // contrast across the available stops; plain pastel wedges use dark ink.
    private static double RelativeLuminance(Color c)
    {
        static double Channel(byte b)
        {
            double v=b/255.0;
            return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4);
        }
        return 0.2126*Channel(c.R)+0.7152*Channel(c.G)+
               0.0722*Channel(c.B);
    }
    private static double InkContrast(Color a,Color b)
    {
        double x=RelativeLuminance(a),y=RelativeLuminance(b);
        return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);
    }
    internal static Color CounterInk(Brush? sectorFill)
    {
        var samples=new System.Collections.Generic.List<Color>();
        if(sectorFill is SolidColorBrush solid)
            samples.Add(solid.Color);
        else if(sectorFill is GradientBrush gradient)
            samples.AddRange(gradient.GradientStops.Select(x=>x.Color));
        if(samples.Count==0)samples.Add(Color.FromRgb(222,229,247));
        Color dark=Color.FromRgb(22,31,52);
        Color white=Color.FromRgb(255,255,255);
        double darkMinimum=samples.Min(c=>InkContrast(dark,c));
        double whiteMinimum=samples.Min(c=>InkContrast(white,c));
        return darkMinimum>=whiteMinimum?dark:white;
    }

    // An actual ink outline (not a plate or badge) keeps the numeral visible
    // on BOTH sides of the sector edge. A shadow blur was not enough when
    // the glyph straddled dark-purple fill and the white desktop background.
    // The background remains entirely transparent, including hover.
    private static ControlTemplate NumberOnlyTemplate(Color ink)
    {
        Color rim=ink.R>190?Color.FromRgb(22,31,52):Colors.White;
        var root=new FrameworkElementFactory(typeof(Grid));
        root.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
        var parent=new RelativeSource(RelativeSourceMode.TemplatedParent);
        void AddInk(Color color,double offsetX,double offsetY)
        {
            var glyph=new FrameworkElementFactory(typeof(TextBlock));
            glyph.SetBinding(TextBlock.TextProperty,
                new Binding("Content"){RelativeSource=parent});
            glyph.SetValue(TextBlock.ForegroundProperty,new SolidColorBrush(color));
            glyph.SetValue(TextBlock.FontFamilyProperty,new FontFamily("Segoe UI"));
            glyph.SetValue(TextBlock.FontWeightProperty,FontWeights.Bold);
            glyph.SetValue(TextBlock.FontSizeProperty,13.5);
            glyph.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);
            glyph.SetValue(FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            glyph.SetValue(FrameworkElement.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            glyph.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.None);
            glyph.SetValue(UIElement.RenderTransformProperty,
                new TranslateTransform(offsetX,offsetY));
            root.AppendChild(glyph);
        }
        // Eight 1-DIP glyph silhouettes create a clean high-contrast edge,
        // WITHOUT a rectangle or background fill anywhere around the text.
        foreach(var point in new[]{
            new Point(-1,-1),new Point(0,-1),new Point(1,-1),
            new Point(-1,0),new Point(1,0),
            new Point(-1,1),new Point(0,1),new Point(1,1)})
            AddInk(rim,point.X,point.Y);
        AddInk(ink,0,0);
        // Dots belong ONLY to hidden-count controls, one before and one
        // after the numeral ALONG the local radial axis. The entire grid
        // rotates together with the divider; no other menu numbers change.
        foreach(double offset in new[]{-HiddenCountDotOffset,HiddenCountDotOffset})
        {
            var dot=new FrameworkElementFactory(
                typeof(System.Windows.Shapes.Ellipse));
            dot.SetValue(FrameworkElement.WidthProperty,HiddenCountDotDiameter);
            dot.SetValue(FrameworkElement.HeightProperty,HiddenCountDotDiameter);
            dot.SetValue(System.Windows.Shapes.Shape.FillProperty,
                new SolidColorBrush(ink));
            dot.SetValue(System.Windows.Shapes.Shape.StrokeProperty,
                new SolidColorBrush(rim));
            dot.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty,1.0);
            dot.SetValue(FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            dot.SetValue(FrameworkElement.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            dot.SetValue(UIElement.RenderTransformProperty,
                new TranslateTransform(0,offset));
            root.AppendChild(dot);
        }
        return new ControlTemplate(typeof(Button)){VisualTree=root};
    }


    // A hidden count is ONE drawn piece of the radial divider, not a
    // separate text/button painted over a white line. These five filled
    // geometries are: divider segment, upper dot, actual numeral glyph,
    // lower dot, divider segment. The click target is invisible.
    private static System.Windows.Shapes.Path DrawInlineCountInk(
        Canvas canvas,Geometry shape,Brush? sectorFill,bool leading,string count)
    {
        var pg=shape as PathGeometry;
        if(pg==null||pg.Figures.Count==0||
           pg.Figures[0].Segments.Count<3||
           pg.Figures[0].Segments[0] is not ArcSegment outerArc||
           pg.Figures[0].Segments[1] is not LineSegment radialLine||
           pg.Figures[0].Segments[2] is not ArcSegment innerArc)
            throw new ArgumentException("Invalid hidden-count divider");
        var fig=pg.Figures[0];
        Point outer=leading?fig.StartPoint:outerArc.Point;
        Point inner=leading?innerArc.Point:radialLine.Point;
        if(shape.Transform is Transform trans)
        {
            outer=trans.Transform(outer);
            inner=trans.Transform(inner);
        }
        Vector axis=inner-outer;
        if(axis.Length<1)throw new ArgumentException("Zero-length divider");
        axis.Normalize();
        Point center=new Point((outer.X+inner.X)/2,
                               (outer.Y+inner.Y)/2);
        double radius=HiddenCountDotDiameter/2.0;
        Point topDot=center-axis*HiddenCountDotOffset;
        Point lowerDot=center+axis*HiddenCountDotOffset;
        Point topEnd=topDot-axis*radius;
        Point lowerStart=lowerDot+axis*radius;

        Geometry Segment(Point a,Point b)
        {
            Vector perpendicular=new Vector(-axis.Y,axis.X)*.9;
            var f=new PathFigure{StartPoint=a+perpendicular,IsClosed=true};
            f.Segments.Add(new LineSegment(b+perpendicular,true));
            f.Segments.Add(new LineSegment(b-perpendicular,true));
            f.Segments.Add(new LineSegment(a-perpendicular,true));
            return new PathGeometry(new[]{f});
        }
        var dark=new SolidColorBrush(CounterInk(sectorFill));
        // The count text belongs INSIDE the narrow wedge edge; not half on
        // the white desktop. Its two DOTS remain on the original radial
        // divider. No additional lines or background plates are drawn.
        Vector perpendicular=new Vector(-axis.Y,axis.X);
        Vector inward=perpendicular*(leading?-1:1);
        Point glyphCenter=center+inward*5.5;
        var font=new Typeface(new FontFamily("Segoe UI"),
            FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
        var formatted=new FormattedText(count,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,font,13.5,dark,1.0);
        Geometry digits=formatted.BuildGeometry(new Point(0,0));
        Rect bounds=digits.Bounds;
        var placement=new TransformGroup();
        placement.Children.Add(new TranslateTransform(
            -bounds.Left-bounds.Width/2,-bounds.Top-bounds.Height/2));
        placement.Children.Add(new RotateTransform(
            DividerAngle(shape,leading)));
        placement.Children.Add(new TranslateTransform(
            glyphCenter.X,glyphCenter.Y));
        digits.Transform=placement;
        var whole=new GeometryGroup();
        // The wedge already owns both halves of the divider. Do not draw
        // a second set of lines here: only the glyph and the two dots fill
        // its genuine stroke gap. This eliminates doubled/background lines.
        whole.Children.Add(new EllipseGeometry(topDot,radius,radius));
        whole.Children.Add(digits);
        whole.Children.Add(new EllipseGeometry(lowerDot,radius,radius));
        var ink=new System.Windows.Shapes.Path{
            Data=whole,
            Tag="DeskSpaceHiddenCountInline",
            Fill=dark,
            Stroke=null,
            StrokeThickness=0,
            IsHitTestVisible=false,
            SnapsToDevicePixels=true
        };
        Canvas.SetZIndex(ink,40);
        canvas.Children.Add(ink);
        return ink;
    }

    private static void AddDividerCount(Canvas canvas,
        System.Windows.Shapes.Path wedge,int hidden,bool leading,Action move)
    {
        Point midpoint=DividerMidpoint(wedge.Data,leading);
        string count=hidden.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Numeral is the only overlaid visual: no mask and no background.
        // Nothing extends beyond the existing radial end divider.
        double width=Math.Max(21,8+count.Length*10),height=39;
        Color color=CounterInk(wedge.Fill);
        DrawInlineCountInk(canvas,wedge.Data,wedge.Fill,leading,count);
        var button=new Button{
            Content=count,
            Width=width,Height=height,
            FontSize=13.5,FontWeight=FontWeights.Bold,
            HorizontalContentAlignment=HorizontalAlignment.Center,
            VerticalContentAlignment=VerticalAlignment.Center,
            Padding=new Thickness(0),
            Background=Brushes.Transparent,
            Opacity=0,
            Foreground=new SolidColorBrush(color),
            BorderThickness=new Thickness(0),
            FocusVisualStyle=null,
            Template=NumberOnlyTemplate(color),
            RenderTransformOrigin=new Point(.5,.5),
            RenderTransform=new RotateTransform(
                DividerAngle(wedge.Data,leading)),
            Cursor=System.Windows.Input.Cursors.Hand,
            ToolTip=(leading?"Hidden before: ":"Hidden after: ")+hidden+
                " · scroll or select to reveal"
        };
        Canvas.SetLeft(button,midpoint.X-width/2);
        Canvas.SetTop(button,midpoint.Y-height/2);
        Canvas.SetZIndex(button,41);
        button.Click+=(_,_)=>move();
        canvas.Children.Add(button);
    }

    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        canvas.BeginAnimation(UIElement.OpacityProperty,null);
        // Small tangential slide plus eased fade makes each detent continuous,
        // with a duration short enough to keep wheel and hover responsive.
        var tangent=(centerAngle+90)*Math.PI/180;
        double dist=direction>0?-11:11;
        var offset=new TranslateTransform();
        canvas.RenderTransform=offset;
        var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        offset.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(dist*Math.Cos(tangent),0,
                TimeSpan.FromMilliseconds(DurationMs)){EasingFunction=easing});
        offset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(dist*Math.Sin(tangent),0,
                TimeSpan.FromMilliseconds(DurationMs)){EasingFunction=easing});
        canvas.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(.74,1,TimeSpan.FromMilliseconds(DurationMs)){
                EasingFunction=easing});
    }
}
