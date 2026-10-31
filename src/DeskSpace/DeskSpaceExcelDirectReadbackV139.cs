using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskSpace;

// Re-read Excel's actual DIRECT cell formatting, not the RGB Excel derives
// from workbook theme palettes or from an automatic/no-fill default.
// Direct color modes remain verified, and explicitly written RGB remains
// strictly verified. Structural TableStyle and rendered non-table appearance
// have independent verification in the format apply worker.
internal static class DeskSpaceExcelDirectReadbackV139
{
    internal static string[] Differences(
        DeskSpaceExcelStyledCellV101 source,
        DeskSpaceExcelStyledCellV101 measured)
    {
        var failures=new List<string>();
        void Exact<T>(string label,T expected,T actual)
        {
            if(!EqualityComparer<T>.Default.Equals(expected,actual))
                failures.Add(label+" expected="+expected+" observed="+actual);
        }
        Exact("NumberFormat",source.NumberFormat,measured.NumberFormat);
        Exact("FontName",source.FontName,measured.FontName);
        if(Math.Abs(source.FontSize-measured.FontSize)>0.5)
            failures.Add("FontSize expected="+source.FontSize.ToString(CultureInfo.InvariantCulture)+
                         " observed="+measured.FontSize.ToString(CultureInfo.InvariantCulture));
        Exact("Bold",source.Bold,measured.Bold);
        Exact("Italic",source.Italic,measured.Italic);
        Exact("Underline",source.Underline,measured.Underline);
        Exact("StrikeThrough",source.StrikeThrough,measured.StrikeThrough);
        Exact("HorizontalAlignment",source.HorizontalAlignment,measured.HorizontalAlignment);
        Exact("VerticalAlignment",source.VerticalAlignment,measured.VerticalAlignment);
        Exact("TextOrientation",source.TextOrientation,measured.TextOrientation);
        Exact("IndentLevel",source.IndentLevel,measured.IndentLevel);
        Exact("WrapText",source.WrapText,measured.WrapText);
        Exact("ShrinkToFit",source.ShrinkToFit,measured.ShrinkToFit);

        // Excel's xlColorIndexAutomatic (-4105) is palette dependent.
        // Matching automatic modes do NOT require identical computed RGB.
        // Likewise, theme colours vary by workbook but should retain their
        // theme slot. Explicit RGB is required to match exactly.
        // Automatic colour MUST take priority over a nonzero inherited
        // ThemeColor value: Excel can report ThemeColor=2 for automatic fonts.
        if(source.DirectFontColorIndex==-4105)
        {
            if(measured.DirectFontColorIndex!=-4105 &&
               measured.DirectFontThemeColor<=0)
                failures.Add("Font.ColorIndex must retain automatic colour");
        }
        else if(source.DirectFontThemeColor>0)
            Exact("Font.ThemeColor",source.DirectFontThemeColor,
                measured.DirectFontThemeColor);
        else
            Exact("Font.Color (explicit RGB)",source.FontBgr,measured.FontBgr);
        if(source.DirectFontColorIndex!=-4105 &&
           source.DirectFontTintAndShade.HasValue &&
           (!measured.DirectFontTintAndShade.HasValue ||
            Math.Abs(source.DirectFontTintAndShade.Value-
                measured.DirectFontTintAndShade.Value)>0.001))
            failures.Add("Font.TintAndShade expected="+
                source.DirectFontTintAndShade+" observed="+
                measured.DirectFontTintAndShade);

        bool sourceNoFill=source.DirectFillPattern==-4142 ||
            source.DirectFillPattern==-4105;
        bool measuredNoFill=measured.DirectFillPattern==-4142 ||
            measured.DirectFillPattern==-4105;
        if(sourceNoFill)
        {
            if(!measuredNoFill)
                failures.Add("Interior.Pattern must retain no-fill");
        }
        else
        {
            Exact("Interior.Pattern",source.DirectFillPattern,
                measured.DirectFillPattern);
            if(source.DirectFillThemeColor>0)
                Exact("Interior.ThemeColor",source.DirectFillThemeColor,
                    measured.DirectFillThemeColor);
            else
                Exact("Interior.Color (explicit RGB)",source.FillBgr,
                    measured.FillBgr);
            if(source.DirectFillTintAndShade.HasValue &&
               (!measured.DirectFillTintAndShade.HasValue ||
                Math.Abs(source.DirectFillTintAndShade.Value-
                    measured.DirectFillTintAndShade.Value)>0.001))
                failures.Add("Interior.TintAndShade expected="+
                    source.DirectFillTintAndShade+" observed="+
                    measured.DirectFillTintAndShade);
        }

        Exact("BorderLeft",source.BorderLeft,measured.BorderLeft);
        Exact("BorderRight",source.BorderRight,measured.BorderRight);
        Exact("BorderTop",source.BorderTop,measured.BorderTop);
        Exact("BorderBottom",source.BorderBottom,measured.BorderBottom);
        // Border RGB and weight matter when the corresponding edge exists.
        // Excel returns an arbitrary palette value for edges with no line.
        if(source.BorderLeft && measured.BorderLeft)
        {
            Exact("BorderLeftColor",source.BorderLeftColor,measured.BorderLeftColor);
            Exact("BorderLeftWeight",source.BorderLeftWeight,measured.BorderLeftWeight);
        }
        if(source.BorderRight && measured.BorderRight)
        {
            Exact("BorderRightColor",source.BorderRightColor,measured.BorderRightColor);
            Exact("BorderRightWeight",source.BorderRightWeight,measured.BorderRightWeight);
        }
        if(source.BorderTop && measured.BorderTop)
        {
            Exact("BorderTopColor",source.BorderTopColor,measured.BorderTopColor);
            Exact("BorderTopWeight",source.BorderTopWeight,measured.BorderTopWeight);
        }
        if(source.BorderBottom && measured.BorderBottom)
        {
            Exact("BorderBottomColor",source.BorderBottomColor,measured.BorderBottomColor);
            Exact("BorderBottomWeight",source.BorderBottomWeight,measured.BorderBottomWeight);
        }
        return failures.ToArray();
    }
}
