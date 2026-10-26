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

        // Excel's xlColorIndexAutomatic (-4105) is palette dependent.
        // Matching automatic modes do NOT require identical computed RGB.
        // Likewise, theme colours vary by workbook but should retain their
        // theme slot. Explicit RGB is required to match exactly.
        if(source.DirectFontThemeColor>0)
            Exact("Font.ThemeColor",source.DirectFontThemeColor,
                measured.DirectFontThemeColor);
        else if(source.DirectFontColorIndex==-4105)
        {
            if(measured.DirectFontColorIndex!=-4105 &&
               measured.DirectFontThemeColor<=0)
                failures.Add("Font.ColorIndex must retain automatic colour");
        }
        else
            Exact("Font.Color (explicit RGB)",source.FontBgr,measured.FontBgr);

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
        }

        Exact("BorderLeft",source.BorderLeft,measured.BorderLeft);
        Exact("BorderRight",source.BorderRight,measured.BorderRight);
        Exact("BorderTop",source.BorderTop,measured.BorderTop);
        Exact("BorderBottom",source.BorderBottom,measured.BorderBottom);
        return failures.ToArray();
    }
}
