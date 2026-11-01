using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// R263: compare a fresh READ-ONLY Excel COM proposal with exactly what
// the user approved. No workbook writes, no COM calls and no app state here.
internal static class DeskSpaceExcelSuggestionReviewV263
{
    internal static bool SameTarget(
        DeskSpaceExcelProposalV16? shown,DeskSpaceExcelProposalV16? current)
    {
        if(shown==null||current==null ||
           !shown.Verified||!current.Verified ||
           shown.Writable<1||current.Writable<1 ||
           shown.UsedReferenceSignatureV120 !=
               current.UsedReferenceSignatureV120 ||
           shown.StartRow!=current.StartRow ||
           shown.StartColumn!=current.StartColumn ||
           !string.Equals(shown.WorkbookFullName,current.WorkbookFullName,
                StringComparison.OrdinalIgnoreCase) ||
           string.IsNullOrWhiteSpace(shown.WorkbookFullName) ||
           !string.Equals(shown.SheetName,current.SheetName,
                StringComparison.OrdinalIgnoreCase) ||
           string.IsNullOrWhiteSpace(shown.SheetName) ||
           shown.Changes==null||current.Changes==null ||
           shown.Changes.Count==0 ||
           shown.Changes.Count!=current.Changes.Count ||
           shown.Writable!=current.Writable)
            return false;
        // Include all proposal cells, even "skip" cells, to prevent an
        // innocent-looking partial overwrite after another Excel action.
        for(int i=0;i<shown.Changes.Count;i++)
        {
            var a=shown.Changes[i];
            var b=current.Changes[i];
            if(a==null||b==null ||
               a.Row!=b.Row||a.Column!=b.Column ||
               a.CanApply!=b.CanApply ||
               !string.Equals(a.CellAddress,b.CellAddress,
                   StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(a.PreviousRaw,b.PreviousRaw,
                   StringComparison.Ordinal) ||
               !string.Equals(a.Previous,b.Previous,
                   StringComparison.Ordinal) ||
               !string.Equals(a.NewValue,b.NewValue,
                   StringComparison.Ordinal) ||
               !string.Equals(a.Decision,b.Decision,
                   StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
