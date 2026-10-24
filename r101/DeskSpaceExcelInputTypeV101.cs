using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DeskSpace;

// Detect actual Excel storage separately from expected cell input type.
// This class intentionally has no Excel COM dependency; every decision can
// be verified in Windows CI before any workbook changes are permitted.
internal static class DeskSpaceExcelInputTypeV101
{
    internal sealed record Conversion(string Status,string Expected,
        object? TypedValue,string Explanation)
    {
        internal bool Safe=>Status=="Converted" || Status=="AlreadyTyped";
    }

    internal static string Stored(object? value,bool formula=false)
    {
        if(formula)return "Formula";
        if(value==null)return "Blank";
        if(value is bool)return "Boolean";
        if(value is double or float or decimal or int or long or short)
            return "Number";
        if(value is DateTime)return "Date";
        return value is string s && s.StartsWith("#",StringComparison.Ordinal)
            && new[]{"#VALUE!","#REF!","#N/A","#NUM!","#DIV/0!","#NAME?"}.
                Contains(s,StringComparer.OrdinalIgnoreCase)
            ?"Error":"Text";
    }

    // Excel's stored Value2 date is a number, not a DateTime. Formatting is
    // needed to infer DATE semantics, while a stored string can still be Text.
    internal static string Expected(string? format,string stored,bool formula=false)
    {
        if(formula || stored=="Formula")return "Formula";
        string code=Simplify(format);
        if(code=="@")return "Text";
        bool date=Regex.IsMatch(code,@"(?i)(y{2,4}|d{1,4}|m{3,4})");
        bool time=Regex.IsMatch(code,@"(?i)(h{1,2}|s{1,2})");
        if(date&&time)return "DateTime";
        if(date)return "Date";
        if(time)return "Time";
        if(code.Contains('%'))return "Percentage";
        if(code.Contains("0") || code.Contains("#") || code.Contains('?'))
            return "Number";
        return stored switch
        {
            "Number"=>"Number","Boolean"=>"Boolean","Date"=>"Date",
            "Text"=>"Text",_=>"Unknown"
        };
    }

    private static string Simplify(string? fmt)
    {
        if(string.IsNullOrWhiteSpace(fmt))return "General";
        // Quoted text, escaped literals, conditions and colour identifiers
        // must not be confused with date/numeric format instructions.
        string code=Regex.Replace(fmt, "\"(?:[^\"]|\"\")*\"","");
        code=Regex.Replace(code,@"\\.","");
        code=Regex.Replace(code,@"\[[^\]]+\]",""); // [Red], [>=100], locales
        return code.Split(';')[0].Trim();
    }

    // Never interpret a serial-looking string as a real date unless the
    // expected format is date/time. Never automatically convert an identifier
    // with significant leading zeroes or a number beyond Excel precision.
    internal static Conversion Normalize(object? input,string expected,
        string? originalNumberFormat=null,string dateOrder="Ask")
    {
        var stored=Stored(input);
        if(input==null || input is string {Length:0})
            return new("NoInput",expected,null,"The destination is blank.");
        if(expected=="Formula")return new("Blocked",expected,null,
            "Formula cells must not be replaced by input data.");
        if(expected=="Unknown"||expected=="Text")
            return new("AlreadyTyped",expected,input,
                "Retain text exactly, including leading zeroes.");
        if(expected=="Boolean")
        {
            if(input is bool)return new("AlreadyTyped",expected,input,"Boolean value.");
            if(input is string b && bool.TryParse(b.Trim(),out bool value))
                return new("Converted",expected,value,"Boolean text parsed.");
            return new("NeedsReview",expected,null,"Expected TRUE or FALSE.");
        }
        if(expected=="Number" || expected=="Percentage")
        {
            if(input is double or float or decimal or int or short or long)
                return new("AlreadyTyped",expected,input,"Already a genuine numeric value.");
            if(input is not string text)return new("NeedsReview",expected,null,
                "Input cannot be interpreted as a number.");
            text=text.Trim().Replace("\u00A0","");
            bool hasPercent=text.EndsWith("%",StringComparison.Ordinal);
            if(hasPercent)text=text[..^1].Trim();
            if(expected=="Percentage"&&!hasPercent)
                return new("NeedsReview",expected,null,
                    "A bare number in a percentage cell is ambiguous.");
            if(expected=="Number"&&hasPercent)
                return new("NeedsReview",expected,null,
                    "Percentage text was supplied to a number cell.");
            if(Regex.IsMatch(text,@"^[+-]?0\d+") &&
               !Regex.IsMatch(text,@"^[+-]?0(?:\.\d+)?$"))
                return new("NeedsReview",expected,null,
                    "Leading zeroes may be meaningful identifiers.");
            bool grouped=Regex.IsMatch(text,@"[,\s]");
            if(grouped && !Regex.IsMatch(text,
                @"^[+-]?(?:\d{1,3}(?:,\d{3})+|\d{1,3}(?:,\d{2})+,\d{3})(?:\.\d+)?$"))
                return new("NeedsReview",expected,null,
                    "Thousands and decimal separators are ambiguous.");
            string digits=new(text.Where(char.IsDigit).ToArray());
            if(digits.TrimStart('0').Length>15)
                return new("NeedsReview",expected,null,
                    "Excel cannot safely preserve more than 15 significant digits.");
            if(!double.TryParse(text,NumberStyles.AllowLeadingSign|
                NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,out double amount) ||
                !double.IsFinite(amount))
                return new("NeedsReview",expected,null,
                    "Number text does not match an unambiguous numeric pattern.");
            return new("Converted",expected,
                expected=="Percentage"?amount/100.0:amount,
                "Numeric text converted; captured Excel number format is retained.");
        }
        if(expected=="Date" || expected=="DateTime" || expected=="Time")
        {
            if(input is DateTime dt)
                return new("AlreadyTyped",expected,dt,"Already a real date/time.");
            if(input is double or float or decimal or int)
                return new("AlreadyTyped",expected,input,
                    "Already a numeric Excel date/time serial; verify workbook date system.");
            if(input is not string dateText)
                return new("NeedsReview",expected,null,"Expected a date or time.");
            dateText=dateText.Trim();
            var iso=new[]{"yyyy-MM-dd","yyyy/M/d","yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss"};
            if(DateTime.TryParseExact(dateText,iso,CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var isoDate))
                return new("Converted",expected,isoDate,
                    "Unambiguous ISO date; use captured display format.");
            var named=new[]{"d MMM yyyy","dd MMM yyyy","d MMMM yyyy",
                "dd MMMM yyyy","MMM d, yyyy","MMMM d, yyyy",
                "d-MMM-yyyy","dd-MMM-yyyy"};
            if(DateTime.TryParseExact(dateText,named,CultureInfo.GetCultureInfo("en-IN"),
                DateTimeStyles.None,out var monthDate))
                return new("Converted",expected,monthDate,
                    "Month-name date converted; display format remains unchanged.");
            var match=Regex.Match(dateText,@"^(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{4})$");
            if(match.Success)
            {
                int first=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
                int second=int.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
                int year=int.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture);
                if(first<=12&&second<=12&&
                   dateOrder!="DMY"&&dateOrder!="MDY")
                    return new("NeedsReview",expected,null,
                        "Ambiguous short date: choose day-first or month-first.");
                bool dayFirst=dateOrder switch
                {
                    "DMY"=>true,"MDY"=>false,_=>first>12
                };
                int day=dayFirst?first:second,month=dayFirst?second:first;
                if(year>=1&&year<=9999&&day>=1&&month>=1&&month<=12 &&
                   day<=DateTime.DaysInMonth(year,month))
                    return new("Converted",expected,new DateTime(year,month,day),
                        "Short date converted to a real Excel date; captured long/short display format remains.");
            }
            return new("NeedsReview",expected,null,
                "Date is invalid, ambiguous, or uses an unsupported locale.");
        }
        return new("NeedsReview",expected,null,"Unsupported expected type.");
    }
}
