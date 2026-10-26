using System;
using System.Collections.Generic;
using System.IO;
using DeskSpace;
using Z=DeskSpace.DeskSpaceExcelZeroIntegrityV145;

static class Program
{
    static int checkedCount;
    static void Assert(bool condition,string message)
    {
        checkedCount++;
        if(!condition)throw new InvalidOperationException("R145 FAIL: "+message);
    }
    static int Main()
    {
        try
        {
            Assert(Z.IsNumericZero(0d) && Z.IsNumericZero(0m) &&
                Z.IsNumericZero(0L) && Z.IsNumericZero(-0f),
                "Excel numeric types can represent a computed zero");
            Assert(!Z.IsNumericZero("0") && !Z.IsNumericZero(false) &&
                !Z.IsNumericZero(null) && !Z.IsNumericZero(double.NaN) &&
                !Z.IsNumericZero(3d),
                "text zeros, blanks, booleans, NaN and nonzero are not numeric zero");
            var formula=new Z.CellState(false,null,true,0d,"","=SUM(RC[-1])");
            var empty=new Z.CellState(false,null,false,null,"","");
            var importedZero=new Z.CellState(false,null,false,0d,"","");
            var existingValue=new Z.CellState(false,100d,false,100d,"","");
            var overwrittenValue=new Z.CellState(false,100d,false,0d,"","");
            Assert(!Z.HasUnexpectedConstant(formula),
                "a real formula calculating zero is not an imported constant");
            Assert(!Z.HasUnexpectedConstant(empty),
                "untouched blank cells must remain blank");
            Assert(Z.HasUnexpectedConstant(importedZero),
                "writing a literal 0 to a previously blank cell is rejected");
            Assert(!Z.HasUnexpectedConstant(existingValue),
                "unchanged existing inputs must be preserved");
            Assert(Z.HasUnexpectedConstant(overwrittenValue),
                "overwriting an input with 0 must be rejected");
            Assert(Z.FormulaZeroCount(new List<Z.CellState>{formula,empty,importedZero})==1,
                "formula zeros are counted separately from literal zero contamination");
            Assert(Z.ZeroExplanation(2).Contains("CALCULATE zero"),
                "real formula result status must not be confused with copied input data");
            Console.WriteLine("R145 PASS: "+checkedCount+" strict zero-integrity assertions.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
