using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// Single Formats & Formulas transaction: validate formula locations before
// any Excel changes, then apply native R1C1 after formatting readback.
// Existing formula values never count as permission to overwrite user data.
internal static class DeskSpaceExcelCombinedFormulaV147
{
    internal sealed record Cell(int Row,int Column,string FormulaA1,
        string FormulaR1C1);
    internal sealed record Target(int Row,int Column,bool HasFormula,
        bool HasValue,string R1C1);
    internal sealed record Insert(int Row,int Column,string FormulaR1C1);
    internal sealed record Decision(Insert[] Writes,int ExistingMatches);

    internal static Decision Inspect(
        IReadOnlyList<Cell> source,IReadOnlyList<Target> destination)
    {
        if(source.Count==0)return new(Array.Empty<Insert>(),0);
        var target=destination.ToDictionary(x=>(x.Row,x.Column));
        var observed=new HashSet<(int,int)>();
        var pending=new List<Insert>();
        int matches=0;
        foreach(var formula in source)
        {
            if(formula.Row<0 || formula.Column<0 ||
               !observed.Add((formula.Row,formula.Column)) ||
               string.IsNullOrWhiteSpace(formula.FormulaA1) ||
               string.IsNullOrWhiteSpace(formula.FormulaR1C1) ||
               !formula.FormulaA1.StartsWith("=",StringComparison.Ordinal) ||
               !formula.FormulaR1C1.StartsWith("=",StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Saved formula geometry/expression is invalid. Recapture.");
            if(!target.TryGetValue((formula.Row,formula.Column),out var actual))
                throw new InvalidDataException(
                    "Formula destination was not fully inspected.");
            if(actual.HasFormula)
            {
                if(!string.Equals(actual.R1C1,formula.FormulaR1C1,
                    StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Destination contains a different formula. "+
                        "Existing formulas will never be replaced.");
                matches++;
            }
            else if(actual.HasValue)
                throw new InvalidDataException(
                    "Formula position contains an existing value. "+
                    "No content will be overwritten.");
            else pending.Add(new(formula.Row,formula.Column,formula.FormulaR1C1));
        }
        return new(pending.ToArray(),matches);
    }
}
