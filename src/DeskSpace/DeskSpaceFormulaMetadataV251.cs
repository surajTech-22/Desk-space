using System;

namespace DeskSpace;

// Stored formulas must be complete, literal R1C1 expressions. This validator
// never executes or evaluates a formula; it protects the existing preflight.
internal static class DeskSpaceFormulaMetadataV251
{
    internal const int MaxFormulaChars=8192;
    internal static bool Valid(string? storedType,string? formula)
    {
        bool isFormula=string.Equals(storedType,"Formula",
            StringComparison.OrdinalIgnoreCase);
        if(!isFormula)return string.IsNullOrEmpty(formula);
        if(string.IsNullOrEmpty(formula) ||
           formula.Length>MaxFormulaChars || formula[0]!='=' ||
           formula.Length<2)return false;
        foreach(char c in formula)
        {
            if(c=='\0' || (char.IsControl(c) &&
                c!='\r' && c!='\n' && c!='\t'))
                return false;
        }
        return true;
    }
}
