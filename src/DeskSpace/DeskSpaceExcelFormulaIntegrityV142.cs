using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskSpace;

// Formulas are stored as expressions, never as their calculated values.
// A source workbook is never mutated by this verification.
internal static class DeskSpaceExcelFormulaIntegrityV142
{
    private static Dictionary<(int Row,int Column),(string A1,string R1C1)>
        Extract(DeskSpaceExcelTemplateV16 snapshot)
    {
        var sheet=snapshot.SheetVisualV101 ??
            throw new InvalidDataException("Excel capture has no sheet snapshot.");
        if(sheet.Rows<=0 || sheet.Columns<=0 ||
           sheet.Cells.Count != sheet.Rows*sheet.Columns)
            throw new InvalidDataException(
                "Incomplete Excel cell capture; refusing to save a partial formula mapping.");

        var result=new Dictionary<(int,int),(string,string)>();
        var seen=new HashSet<(int,int)>();
        foreach(var cell in sheet.Cells)
        {
            var key=(cell.Row,cell.Column);
            if(cell.Row<0 || cell.Column<0 ||
               cell.Row>=sheet.Rows || cell.Column>=sheet.Columns ||
               !seen.Add(key))
                throw new InvalidDataException(
                    "Excel capture has missing, duplicate or out-of-range cells.");
            bool declared=string.Equals(cell.StoredType,"Formula",StringComparison.Ordinal);
            bool hasText=!string.IsNullOrWhiteSpace(cell.FormulaA1) ||
                         !string.IsNullOrWhiteSpace(cell.FormulaR1C1);
            if(declared)
            {
                if(string.IsNullOrWhiteSpace(cell.FormulaA1) ||
                   string.IsNullOrWhiteSpace(cell.FormulaR1C1) ||
                   !cell.FormulaA1.StartsWith("=",StringComparison.Ordinal) ||
                   !cell.FormulaR1C1.StartsWith("=",StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Excel formula expression missing or invalid at "+
                        (cell.Address.Length>0?cell.Address:
                         "row "+(cell.Row+1)+", column "+(cell.Column+1))+
                        ". The format mapping was NOT saved. Recapture the original selection.");
                result.Add(key,(cell.FormulaA1,cell.FormulaR1C1));
            }
            else if(hasText)
                throw new InvalidDataException(
                    "Inconsistent Excel cell formula metadata. Recapture the selected range.");
        }
        return result;
    }

    internal static void ValidateCapture(DeskSpaceExcelTemplateV16 source)
    {
        _=Extract(source);
    }

    internal static void ValidateRoundtrip(
        DeskSpaceExcelTemplateV16 source,DeskSpaceExcelTemplateV16? readBack)
    {
        if(readBack==null || readBack.Id!=source.Id)
            throw new InvalidDataException(
                "Saved format mapping not found during immediate disk verification.");
        var original=Extract(source);
        var stored=Extract(readBack);
        if(original.Count!=stored.Count)
            throw new InvalidDataException(
                "Saved formula count changed. Mapping persistence verification failed.");
        foreach(var item in original)
        {
            if(!stored.TryGetValue(item.Key,out var value) ||
               !string.Equals(item.Value.A1,value.A1,StringComparison.Ordinal) ||
               !string.Equals(item.Value.R1C1,value.R1C1,StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Saved formula expression differs from captured Excel formula at "+
                    "row "+(item.Key.Row+1)+", column "+(item.Key.Column+1)+
                    ". Recheck the saved mapping before applying.");
        }
    }
}
