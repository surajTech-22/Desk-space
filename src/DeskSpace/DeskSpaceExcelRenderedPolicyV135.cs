using System;
using System.Collections.Generic;
namespace DeskSpace;

// Excel ListObject display formatting is COM-generated. It is not an editable
// native per-cell style layer: row stripes, header formatting, theme resolution
// and placeholder header VALUES can change DisplayFormat comparisons although
// the correct real ListObject/TableStyle was reproduced. The format writer
// independently checks all source direct formats (including borders), exact
// table style/structure, merged areas, underlines, formulas and cell values.
// Do not require a byte-identical DisplayFormat for cells inside a validated
// actual table. For non-table cells, continue comparing rendered appearance.
internal static class DeskSpaceExcelRenderedPolicyV135
{
    internal static bool RequiresExactRenderedMatch(
        int row,int column,IReadOnlyList<DeskSpaceExcelTableV106> tables)
    {
        foreach(var t in tables)
        {
            if(!t.FullySelected || string.IsNullOrWhiteSpace(t.TableStyle) ||
               t.TableStyle.StartsWith("System._ComObject",StringComparison.OrdinalIgnoreCase))
                continue; // Invalid table metadata MUST NOT bypass verification.
            if(row>=t.Row && row<t.Row+t.RowCount &&
               column>=t.Column && column<t.Column+t.ColumnCount)
                return false;
        }
        return true;
    }
}
