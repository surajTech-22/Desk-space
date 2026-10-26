$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$view="$r/DeskSpaceExcelSheetViewV101.cs"
$apply="$r/DeskSpaceExcelFormatApplyV109.cs"
$check="$r/DeskSpaceExcelDirectReadbackV139.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf)
 $b=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R140 $($label): expected one code anchor, found $n"}
 [IO.File]::WriteAllText($file,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
# Nullable preserves legacy templates without pretending they captured tint.
Swap $view @'
    public int DirectFillThemeColor {get;set;}
'@ @'
    public int DirectFillThemeColor {get;set;}
    public double? DirectFontTintAndShade {get;set;}
    public double? DirectFillTintAndShade {get;set;}
'@ 'store precise tint/shade with backward compatible nullable metadata'
Swap $view @'
    private static double RequiredMeasure(Func<object?> read,string label)
'@ @'
    private static double? Tint(Func<object?> read)
    {
        try
        {
            var input=read();
            if(input==null)return null;
            double value=Convert.ToDouble(input,CultureInfo.InvariantCulture);
            return double.IsFinite(value)?value:null;
        }
        catch(COMException){return null;}
        catch(InvalidCastException){return null;}
        catch(FormatException){return null;}
        catch(OverflowException){return null;}
    }
    private static double RequiredMeasure(Func<object?> read,string label)
'@ 'read optional per-cell tint'
Swap $view @'
                DirectFillThemeColor=Number(()=> (object?)cell.Interior.ThemeColor,0),
'@ @'
                DirectFillThemeColor=Number(()=> (object?)cell.Interior.ThemeColor,0),
                DirectFontTintAndShade=Tint(()=> (object?)cell.Font.TintAndShade),
                DirectFillTintAndShade=Tint(()=> (object?)cell.Interior.TintAndShade),
'@ 'capture font/fill tint'

# Font ColorIndex=-4105 is authoritative for AUTOMATIC even if Excel also
# reports an inherited ThemeColor 2. Never assume a derived DisplayFormat
# difference is proof that a theme/explicit colour can be thrown away.
Swap $apply @'
        if(feature?.TableName.Length>0 && s.DisplayStyleAvailable &&
           s.DisplayFontBgr!=s.FontBgr)
            // The visible colour is supplied by the real Excel table style,
            // while the ordinary Font layer is an automatic/default colour.
            // An explicit RGB write would incorrectly override the header.
            cell.Font.ColorIndex=-4105;
        else if(s.DirectFontThemeColor>0)
            cell.Font.ThemeColor=s.DirectFontThemeColor;
        else if(s.DirectFontColorIndex==-4105)
            cell.Font.ColorIndex=-4105;
        else
            cell.Font.Color=s.FontBgr;
'@ @'
        // Colour mode takes precedence over derived workbook display RGB.
        // Excel frequently reports ThemeColor=2 even for an AUTOMATIC font.
        // Preserve exact source DIRECT mode; ListObject controls appearance.
        if(s.DirectFontColorIndex==-4105)
            cell.Font.ColorIndex=-4105;
        else if(s.DirectFontThemeColor>0)
            cell.Font.ThemeColor=s.DirectFontThemeColor;
        else
            cell.Font.Color=s.FontBgr;
        if(s.DirectFontColorIndex!=-4105 &&
           s.DirectFontTintAndShade.HasValue)
            cell.Font.TintAndShade=s.DirectFontTintAndShade.Value;
'@ 'preserve font color mode and tint instead of overriding table theme'
Swap $apply @'
        else
            throw new InvalidDataException("Unsupported direct Excel fill pattern.");
        cell.HorizontalAlignment=s.HorizontalAlignment;
'@ @'
        else
            throw new InvalidDataException("Unsupported direct Excel fill pattern.");
        if(s.DirectFillPattern==1 && s.DirectFillTintAndShade.HasValue)
            cell.Interior.TintAndShade=s.DirectFillTintAndShade.Value;
        cell.HorizontalAlignment=s.HorizontalAlignment;
'@ 'preserve fill tint'
Swap $apply @'
        cell.IndentLevel=s.IndentLevel;
        // Neither formulas nor values are changed by format-only application.
'@ @'
        cell.IndentLevel=s.IndentLevel;
        cell.Orientation=s.TextOrientation;
        if(feature!=null)
        {
            if(feature.Superscript && feature.Subscript)
                throw new InvalidDataException(
                    "Source cell has contradictory superscript/subscript metadata.");
            cell.Font.Superscript=feature.Superscript;
            cell.Font.Subscript=feature.Subscript;
            cell.Locked=feature.Locked;
            cell.FormulaHidden=feature.FormulaHidden;
        }
        // Neither formulas nor values are changed by format-only application.
'@ 'preserve orientation superscript/subscript protection'
# Existing section changed by R139: replace actual policy with exhaustive
# property-level checks, keeping explicit comparisons strict and reporting names.
Swap $check @'
        Exact("StrikeThrough",source.StrikeThrough,measured.StrikeThrough);
'@ @'
        Exact("StrikeThrough",source.StrikeThrough,measured.StrikeThrough);
        Exact("HorizontalAlignment",source.HorizontalAlignment,measured.HorizontalAlignment);
        Exact("VerticalAlignment",source.VerticalAlignment,measured.VerticalAlignment);
        Exact("TextOrientation",source.TextOrientation,measured.TextOrientation);
        Exact("IndentLevel",source.IndentLevel,measured.IndentLevel);
        Exact("WrapText",source.WrapText,measured.WrapText);
        Exact("ShrinkToFit",source.ShrinkToFit,measured.ShrinkToFit);
'@ 'read back every written alignment/wrap/orientation field'
Swap $check @'
        if(source.DirectFontThemeColor>0)
            Exact("Font.ThemeColor",source.DirectFontThemeColor,
                measured.DirectFontThemeColor);
        else if(source.DirectFontColorIndex==-4105)
'@ @'
        // Automatic colour MUST take priority over a nonzero inherited
        // ThemeColor value: Excel can report ThemeColor=2 for automatic fonts.
        if(source.DirectFontColorIndex==-4105)
'@ 'prioritize actual automatic font mode'
Swap $check @'
        else
            Exact("Font.Color (explicit RGB)",source.FontBgr,measured.FontBgr);

        bool sourceNoFill
'@ @'
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

        bool sourceNoFill
'@ 'strict font theme and tint readback when applicable'
Swap $check @'
                Exact("Interior.Color (explicit RGB)",source.FillBgr,
                    measured.FillBgr);
        }

        Exact("BorderLeft",source.BorderLeft,measured.BorderLeft);
'@ @'
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
'@ 'strict filled cell tint readback'
Swap $check @'
        Exact("BorderBottom",source.BorderBottom,measured.BorderBottom);
        return failures.ToArray();
'@ @'
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
'@ 'verify actual visible edge colours and weights'
Write-Host 'R140 capture/apply/readback aligned: font automatic mode before theme, real RGB/theme/tint fidelity, fill pattern/tint, border colour/weight, alignment, orientation, wrap, shrink, font and merged table. Existing formula/blank-value protection retained.'