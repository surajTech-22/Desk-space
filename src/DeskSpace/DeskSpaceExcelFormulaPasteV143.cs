using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DeskSpace;

// A formula-ready target is NOT an arbitrary partially-filled range.
// Every saved non-formula input must be present with its expected type;
// only saved formula positions may be blank. Unrelated blank positions
// must remain blank. Entirely empty targets remain format-only to avoid
// showing zeroes from calculations over missing inputs.
internal static class DeskSpaceExcelFormulaPasteV143
{
    internal const string Explanation =
        "All required inputs match; only captured formula cells are empty. "+
        "Those exact formulas may be restored without replacing any input.";

    internal static bool Ready(
        IReadOnlyList<DeskSpaceExcelStyledCellV101> source,
        IReadOnlyList<DeskSpaceExcelFullRangeGateV107.Cell> target)
    {
        if(source.Count==0 || source.Count!=target.Count)return false;
        bool hasInput=false,hasMissingFormula=false;
        for(int i=0;i<source.Count;i++)
        {
            var src=source[i];
            var dst=target[i];
            if(src.StoredType=="Formula")
            {
                if(dst.HasData ||
                   string.IsNullOrWhiteSpace(src.FormulaA1) ||
                   string.IsNullOrWhiteSpace(src.FormulaR1C1) ||
                   !src.FormulaA1.StartsWith("=",StringComparison.Ordinal) ||
                   !src.FormulaR1C1.StartsWith("=",StringComparison.Ordinal))
                    return false;
                hasMissingFormula=true;
            }
            else if(src.StoredType=="Blank")
            {
                if(dst.HasData)return false;
            }
            else
            {
                if(!dst.HasData || !string.Equals(
                    Normalize(src.StoredType),Normalize(dst.StoredType),
                    StringComparison.Ordinal))
                    return false;
                hasInput=true;
            }
        }
        return hasInput && hasMissingFormula;
    }

    private static string Normalize(string type) =>
        string.Equals(type,"Date",StringComparison.OrdinalIgnoreCase)?
            "NUMBER":type.ToUpperInvariant();

    // Called only after the full workbook backup and verified target gate.
    // Source transaction/data values are never copied.
    internal static void WriteExactFormulas(dynamic target,
        IReadOnlyList<DeskSpaceExcelStyledCellV101> source)
    {
        foreach(var src in source.Where(x=>x.StoredType=="Formula"))
        {
            dynamic cell=target.Cells[src.Row+1,src.Column+1];
            // An Excel calculated table column may auto-fill its remaining
            // cells after the FIRST exact assignment. Such generated cells
            // are permitted only when they ALREADY equal the captured formula.
            // A different formula/value is never overwritten.
            if(Convert.ToBoolean((object?)cell.HasFormula))
            {
                if(!string.Equals(
                    Convert.ToString((object?)cell.FormulaR1C1),
                    src.FormulaR1C1,StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Excel generated a different formula at "+src.Address+
                        ". Existing formulas are never overwritten.");
                continue;
            }
            if((object?)cell.Value2 != null)
                throw new InvalidDataException(
                    "Formula target contains an existing value at "+src.Address+
                    "; nothing will be overwritten.");
            if(string.IsNullOrWhiteSpace(src.FormulaR1C1) ||
               !src.FormulaR1C1.StartsWith("=",StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Captured formula is incomplete at "+src.Address+".");
            cell.FormulaR1C1=src.FormulaR1C1;
        }
    }
}
