using System;
using System.Collections.Generic;
namespace DeskSpace;

// A format-only paste is NOT a template data copy.
// When the entire destination was blank, no source values or formulas are
// inserted (which would show zero with blank formula inputs). Genuine Excel
// ListObjects require their structural header names. These header names are
// the only permitted new nonblank content; body cells remain empty.
internal static class DeskSpaceExcelBlankFormatPolicyV136
{
    internal static string AllowedStructuralHeader(
        int row,int col,IReadOnlyList<DeskSpaceExcelTableV106> tables)
    {
        foreach(var table in tables)
        {
            if(!table.HasHeaders || row!=table.Row ||
                col<table.Column || col>=table.Column+table.ColumnCount)
                continue;
            int index=col-table.Column;
            return index>=0 && index<table.ColumnNames.Count
                ? table.ColumnNames[index] : "";
        }
        return "";
    }
}
