using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// Only captures existing formulas into empty target cells, without copying
// values, running macros, or touching a partially populated worksheet.
// The COM writer verifies exact R1C1 readback and preserves the original
// SaveCopyAs rollback route.
internal static class DeskSpaceExcelFormulaPlanV266
{
    internal sealed record Input(int Row,int Column,string StoredType,
        string FormulaR1C1,bool MergedChild,bool TableHeader);
    internal sealed record Placement(int Row,int Column,string FormulaR1C1);

    internal static IReadOnlyList<Placement> Build(
        IReadOnlyList<Input> cells,int rows,int cols)
    {
        if(cells==null || rows<1 || cols<1 ||
           rows*(long)cols>1800 || cells.Count!=(long)rows*cols)
            throw new InvalidDataException("Captured formula grid is incomplete.");
        var placements=new List<Placement>();
        var seen=new HashSet<(int,int)>();
        foreach(var cell in cells)
        {
            if(cell==null || cell.Row<0 || cell.Row>=rows ||
               cell.Column<0 || cell.Column>=cols ||
               !seen.Add((cell.Row,cell.Column)) ||
               !DeskSpaceFormulaMetadataV251.Valid(
                   cell.StoredType,cell.FormulaR1C1))
                throw new InvalidDataException(
                    "Captured formula cell positions or metadata are inconsistent.");
            if(!string.Equals(cell.StoredType,"Formula",
                StringComparison.OrdinalIgnoreCase))
                continue;
            if(cell.MergedChild || cell.TableHeader)
                throw new InvalidDataException(
                    "A formula occupies a merged child or table header; recapture source.");
            placements.Add(new(cell.Row,cell.Column,cell.FormulaR1C1));
        }
        return placements.OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
    }

    internal static bool Matches(Placement expected,
        bool actualHasFormula,string? actualR1C1)=>
        actualHasFormula && string.Equals(
            expected.FormulaR1C1,actualR1C1,StringComparison.Ordinal);
}
