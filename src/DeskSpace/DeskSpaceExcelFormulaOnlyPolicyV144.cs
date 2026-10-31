using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// Pure formula-only paste planner. No Excel clipboard, input data, styles,
// number formats, or table content is written by this planner.
internal static class DeskSpaceExcelFormulaOnlyPolicyV144
{
    internal sealed record Source(int Row,int Column,string A1,string R1C1);
    internal sealed record Target(int Row,int Column,bool HasFormula,
        bool HasValue,string ExistingR1C1);
    internal sealed record Assignment(int Row,int Column,string R1C1);
    internal sealed record PlanResult(Assignment[] Assignments,int AlreadyPresent);

    internal static PlanResult Plan(
        IReadOnlyList<Source> formulas,
        IReadOnlyList<Target> destinations,int rows,int cols)
    {
        if(rows<1 || cols<1 || rows*(long)cols>1800)
            throw new InvalidDataException("Captured Excel range is invalid.");
        if(formulas.Count==0)
            throw new InvalidDataException("Saved mapping has no formulas to paste.");
        var locations=new HashSet<(int,int)>();
        var byPosition=destinations.ToDictionary(x=>(x.Row,x.Column));
        var insert=new List<Assignment>();
        int retained=0;
        foreach(var source in formulas)
        {
            if(source.Row<0 || source.Row>=rows ||
               source.Column<0 || source.Column>=cols ||
               !locations.Add((source.Row,source.Column)))
                throw new InvalidDataException("Duplicate/out-of-bounds formula position.");
            if(!source.A1.StartsWith("=",StringComparison.Ordinal) ||
               !source.R1C1.StartsWith("=",StringComparison.Ordinal))
                throw new InvalidDataException("Saved formula is incomplete; recapture from Excel.");
            if(!byPosition.TryGetValue((source.Row,source.Column),out var target))
                throw new InvalidDataException("Destination formula cell was not inspected.");
            if(target.HasFormula)
            {
                if(!string.Equals(target.ExistingR1C1,source.R1C1,
                    StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "A different formula already exists in a destination cell. "+
                        "No formulas have been pasted.");
                retained++;
            }
            else if(target.HasValue)
                throw new InvalidDataException(
                    "A destination formula cell contains user data. "+
                    "No formulas have been pasted.");
            else
                insert.Add(new(source.Row,source.Column,source.R1C1));
        }
        return new(insert.ToArray(),retained);
    }
}
