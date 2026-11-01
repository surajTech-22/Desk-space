using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DeskSpace;

// A native Excel formula is not a copied literal zero. SUM(blank) may
// compute numeric zero. Keep source formulas exact and inputs physically
// empty; never replace formulas with IF() or fill empty input cells with 0.
internal static class DeskSpaceExcelZeroIntegrityV145
{
    internal sealed record CellState(bool WasFormula,object? Before,
        bool NowFormula,object? After,string BeforeFormula,string AfterFormula);

    internal static bool IsNumericZero(object? value)
    {
        if(value is null or bool)return false;
        if(value is not (byte or sbyte or short or ushort or int or uint or
            long or ulong or float or double or decimal))return false;
        try{return Convert.ToDouble(value,CultureInfo.InvariantCulture)==0d;}
        catch(OverflowException){return false;}
        catch(FormatException){return false;}
    }

    // Strict audit: an unchanged user cell retains its original raw value.
    // An inserted formula may CALCULATE 0, but it is never a literal input.
    internal static bool HasUnexpectedConstant(CellState state)
    {
        if(state.WasFormula || state.NowFormula)return false;
        if(state.Before==null)return state.After!=null;
        return !Equals(state.Before,state.After);
    }

    internal static int FormulaZeroCount(
        IReadOnlyList<CellState> observed)=>observed.Count(c=>
            c.NowFormula && IsNumericZero(c.After));

    internal static string ZeroExplanation(int computedZeroCount) =>
        computedZeroCount==0?"No formula results currently display zero.":
        computedZeroCount+" formulas currently CALCULATE zero. "+
        "No literal zeros were copied into input cells. Excel will "+
        "recalculate these formulas when inputs change.";
}
