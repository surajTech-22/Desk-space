using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace DeskSpace;
internal static class DeskSpaceExcelActionsV46
{
    internal const string Date="action_static_date", Time="action_static_time",
        DateTimeKind="action_static_datetime", Today="action_formula_today",
        Now="action_formula_now", CustomFormula="action_custom_formula";
    internal static readonly string[] EditorKinds={
        "Value","Reference","Formula","Current date on paste (Ctrl+;)",
        "Current time on paste (Ctrl+Shift+;)","Current date and time on paste",
        "Live date formula (=TODAY())","Live date/time formula (=NOW())",
        "Excel Shortcut (enter Ctrl+; or Ctrl+Shift+;)","Custom formula (paste)"
    };
    internal static bool IsAction(string k)=>k is Date or Time or DateTimeKind
        or Today or Now or CustomFormula;
    internal static string Label(DeskSpaceExcelCellV16 c)=>c.ValueKind switch
    {
        Date=>EditorKinds[3],Time=>EditorKinds[4],
        DateTimeKind=>EditorKinds[5],Today=>EditorKinds[6],Now=>EditorKinds[7],
        _ when c.Role=="Reference"=>"Reference",
        _ when c.ValueKind=="formula"=>"Formula",_=>"Value"
    };
    internal static string FromShortcut(string input)
    {
        string k=new string(input.Trim().ToLowerInvariant()
            .Where(c=>!char.IsWhiteSpace(c)).ToArray());
        return k switch
        {
            "ctrl+;" or "control+;" or "^;"=>Date,
            "ctrl+shift+;" or "control+shift+;" or "^+;"=>Time,
            "ctrl+;ctrl+shift+;" or "ctrl+;spacectrl+shift+;"=>DateTimeKind,
            "=today()" or "today()"=>Today,"=now()" or "now()"=>Now,
            _=>throw new InvalidOperationException(
                "This Excel shortcut cannot safely run as an automatic cell-value action. "+
                "Supported: Ctrl+; (fixed date), Ctrl+Shift+; (time), =TODAY(), =NOW(). "+
                "Other keyboard shortcuts appear in the reference catalog for manual use.")
        };
    }
    internal static string KindFromEditor(string selected,string input)
    {
        if(selected==EditorKinds[3])return Date;
        if(selected==EditorKinds[4])return Time;
        if(selected==EditorKinds[5])return DateTimeKind;
        if(selected==EditorKinds[6])return Today;
        if(selected==EditorKinds[7])return Now;
        if(selected==EditorKinds[8])return FromShortcut(input);
        if(selected==EditorKinds[9])return CustomFormula;
        return "";
    }
    internal static bool IsStaticDateTime(string k)=>k is Date or Time or DateTimeKind;
    internal static bool IsFormulaAction(string k)=>k is Today or Now or CustomFormula;
    internal static object ValueAtPaste(DeskSpaceExcelCellV16 c,DateTime clock)=>
        c.ValueKind switch
        {
            Date=>clock.Date.ToOADate(),
            Time=>clock.TimeOfDay.TotalDays,
            DateTimeKind=>clock.ToOADate(),
            _=>throw new InvalidOperationException("Not a dynamic Excel cell value")
        };
    internal static string Formula(DeskSpaceExcelCellV16 c)=>c.ValueKind switch
    {
        Today=>"=TODAY()",Now=>"=NOW()",
        CustomFormula when c.FormulaR1C1.StartsWith("=",StringComparison.Ordinal)
            =>c.FormulaR1C1,
        _=>throw new InvalidOperationException("Missing user-approved formula")
    };
    internal static string NumberFormat(string k)=>k switch
    {
        Date or Today=>"dd-mmm-yyyy",Time=>"hh:mm:ss",
        DateTimeKind or Now=>"dd-mmm-yyyy hh:mm:ss",_=>"General"
    };
    internal static string PreviewText(DeskSpaceExcelCellV16 c,DateTime clock)=>c.ValueKind switch
    {
        Date=>clock.ToString("dd-MMM-yyyy",CultureInfo.InvariantCulture),
        Time=>clock.ToString("HH:mm:ss",CultureInfo.InvariantCulture),
        DateTimeKind=>clock.ToString("dd-MMM-yyyy HH:mm:ss",CultureInfo.InvariantCulture),
        Today=>"=TODAY() [recalculates]",Now=>"=NOW() [recalculates]",
        CustomFormula=>c.FormulaR1C1,_=>c.Display
    };
    // Excel Windows reference. Only safe, cell-local value shortcuts above
    // are executable mapping actions; others are documentation/search aids.
    internal static readonly (string Keys,string Operation,string Category)[] Shortcuts={
        ("Ctrl+;","Insert today's fixed date","Data"),
        ("Ctrl+Shift+;","Insert current time","Data"),
        ("=TODAY()","Live date formula","Formulas"),
        ("=NOW()","Live date and time formula","Formulas"),
        ("F2","Edit cell","Editing"),("Enter","Confirm cell entry","Editing"),
        ("Alt+Enter","New line inside a cell","Editing"),
        ("Ctrl+Enter","Fill selection with entry","Editing"),
        ("Ctrl+D","Fill down","Editing"),("Ctrl+R","Fill right","Editing"),
        ("Ctrl+C","Copy","Clipboard"),("Ctrl+X","Cut","Clipboard"),
        ("Ctrl+V","Paste","Clipboard"),("Ctrl+Alt+V","Paste Special","Clipboard"),
        ("Ctrl+Z","Undo","Editing"),("Ctrl+Y","Redo","Editing"),
        ("Ctrl+F","Find","Navigation"),("Ctrl+H","Replace","Navigation"),
        ("Ctrl+G","Go to","Navigation"),("F5","Go to","Navigation"),
        ("Ctrl+A","Select range","Navigation"),
        ("Ctrl+Space","Select column","Navigation"),
        ("Shift+Space","Select row","Navigation"),
        ("Ctrl+Home","First cell","Navigation"),
        ("Ctrl+End","Last used cell","Navigation"),
        ("Ctrl+Arrow","Jump to range edge","Navigation"),
        ("Ctrl+Shift+Arrow","Extend selection","Navigation"),
        ("Ctrl+PageUp","Previous sheet","Navigation"),
        ("Ctrl+PageDown","Next sheet","Navigation"),
        ("Ctrl+B","Bold","Formatting"),("Ctrl+I","Italic","Formatting"),
        ("Ctrl+U","Underline","Formatting"),("Ctrl+5","Strikethrough","Formatting"),
        ("Ctrl+1","Format Cells","Formatting"),
        ("Ctrl+Shift+1","Number format","Formatting"),
        ("Ctrl+Shift+2","Time format","Formatting"),
        ("Ctrl+Shift+3","Date format","Formatting"),
        ("Ctrl+Shift+4","Currency format","Formatting"),
        ("Ctrl+Shift+5","Percentage format","Formatting"),
        ("Ctrl+K","Insert hyperlink","Insert"),
        ("Ctrl+T","Create table","Insert"),
        ("Shift+F11","Add sheet","Workbook"),
        ("Ctrl+N","New workbook","Workbook"),
        ("Ctrl+O","Open workbook","Workbook"),
        ("Ctrl+S","Save workbook","Workbook"),
        ("Ctrl+W","Close workbook","Workbook"),
        ("F12","Save As","Workbook"),("Ctrl+P","Print","Workbook"),
        ("F4","Repeat / reference modes","Formulas"),
        ("F9","Recalculate","Formulas"),
        ("Shift+F9","Recalculate sheet","Formulas"),
        ("Alt+=","AutoSum","Formulas"),
        ("Ctrl+Shift+L","Toggle filters","Data"),
        ("Ctrl+Q","Quick Analysis","Data"),
        ("F7","Check spelling","Review"),
        ("Alt+H","Home tab","Ribbon"),("Alt+N","Insert tab","Ribbon"),
        ("Alt+A","Data tab","Ribbon"),("Alt+M","Formulas tab","Ribbon"),
        ("Alt+W","View tab","Ribbon")
    };
}
