using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DeskSpace;

// Excel Range.Locked/FormulaHidden are workbook-protection metadata, not
// visible formatting. Excel may forbid writing them in a table or other
// restricted region even when other cell formatting is permitted.
// Preserve the destination's existing protection if COM refuses a change;
// final readback checks it is unchanged and reports incomplete protection.
internal static class DeskSpaceExcelProtectionV141
{
    internal static bool TryChange(Func<object?> get,Action put,bool wanted)
    {
        try
        {
            object? original=get();
            if(original==null || original is DBNull)return false;
            if(Convert.ToBoolean(original,CultureInfo.InvariantCulture)==wanted)
                return true; // no COM write needed
            put();
            return true;
        }
        catch(COMException) { return false; }
        catch(InvalidCastException) { return false; }
        catch(FormatException) { return false; }
    }
    internal static void Apply(dynamic cell,bool locked,bool hidden)
    {
        // Do not remove worksheet protection, change data, or write through a
        // property rejection. Verification will flag all missing metadata.
        TryChange(()=>(object?)cell.Locked,()=>cell.Locked=locked,locked);
        TryChange(()=>(object?)cell.FormulaHidden,
                  ()=>cell.FormulaHidden=hidden,hidden);
    }
}
