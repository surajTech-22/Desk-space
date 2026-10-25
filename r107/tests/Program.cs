using System;
using System.Collections.Generic;
using System.Linq;
using DeskSpace;
using Gate=DeskSpace.DeskSpaceExcelFullRangeGateV107;
internal static class Program
{
    private static int Main()
    {
        static Gate.Cell E()=>new(false,"Blank");
        static Gate.Cell N()=>new(true,"Number");
        static Gate.Cell T()=>new(true,"Text");
        static Gate.Cell F()=>new(true,"Formula");
        static void Assert(bool ok,string name)
        {
            if(!ok)throw new Exception("R107 FAIL: "+name);
            Console.WriteLine("R107 PASS: "+name);
        }
        var source=new[]{T(),T(),T(),T(),N(),N(),N(),N()};
        Assert(Gate.Evaluate(source,new[]{E(),E(),E(),E(),E(),E(),E(),E()})
            .State==Gate.Status.EntirelyEmpty,"all cells empty eligible");
        Assert(Gate.Evaluate(source,new[]{T(),T(),T(),T(),N(),N(),N(),N()})
            .State==Gate.Status.CompleteMatchingLayout,"complete source type layout eligible");
        Assert(!Gate.Evaluate(source,new[]{T(),T(),E(),E(),N(),E(),E(),E()})
            .MayApply,"partial fill always blocks");
        Assert(Gate.Evaluate(source,new[]{T(),T(),T(),T(),N(),N(),E(),E()})
            .State==Gate.Status.RejectPartial,"even one missing cell blocks");
        Assert(Gate.Evaluate(source,new[]{T(),T(),T(),T(),N(),N(),N(),T()})
            .State==Gate.Status.RejectMismatch,"wrong stored type blocks");
        Assert(!Gate.Evaluate(source,new[]{T(),T(),T(),T(),N()}).MayApply,
            "incomplete size blocks");
        var withStructuralBlank=new[]{T(),E(),N(),F()};
        Assert(Gate.Evaluate(withStructuralBlank,new[]{T(),E(),N(),F()})
            .State==Gate.Status.CompleteMatchingLayout,
            "merged-area structural blank preserved");
        Assert(Gate.Evaluate(withStructuralBlank,new[]{T(),N(),E(),F()})
            .State==Gate.Status.RejectPartial,
            "same count but incorrect placement blocks");
        Assert(Gate.Evaluate(withStructuralBlank,new[]{T(),E(),N(),N()})
            .State==Gate.Status.RejectMismatch,
            "formula replaced by plain value blocks");
        Assert(Gate.Evaluate(withStructuralBlank,new[]{E(),E(),E(),E()})
            .State==Gate.Status.EntirelyEmpty,
            "fully empty structure allowed");
        Assert(Gate.Evaluate(Array.Empty<Gate.Cell>(),Array.Empty<Gate.Cell>())
            .State==Gate.Status.RejectMismatch,"no source cells blocks");
        // Book1(4).xlsx structural test: 5 rows x 6 cols (E7:J11).
        // E7 is a merged anchor; E8:E11 are structural blanks.
        // F7:J7 are five genuine ListObject headers; remaining cells
        // contain numbers and formula-driven values.
        var tableLayout=new Gate.Cell[30];
        for(int row=0;row<5;row++)
        for(int col=0;col<6;col++)
            tableLayout[row*6+col]=
                col==0?(row==0?T():E()):
                row==0?T():
                (col==5 || row==4)?F():N();
        Assert(Gate.Evaluate(tableLayout,Enumerable.Repeat(E(),30).ToArray())
            .State==Gate.Status.EntirelyEmpty,
            "Book1 five-by-six selection accepts completely blank destination");
        Assert(Gate.Evaluate(tableLayout,tableLayout)
            .State==Gate.Status.CompleteMatchingLayout,
            "Book1 table/merged structure accepts all matching input positions");
        var incomplete=(Gate.Cell[])tableLayout.Clone();
        incomplete[7]=E();
        Assert(Gate.Evaluate(tableLayout,incomplete).State==Gate.Status.RejectPartial,
            "Book1 missing one populated input blocks entire range");
        var shifted=(Gate.Cell[])tableLayout.Clone();
        shifted[7]=E();shifted[12]=N();
        Assert(Gate.Evaluate(tableLayout,shifted).State==Gate.Status.RejectPartial,
            "Book1 same filled count but merged structural blank overwritten blocks");
        var changedFormula=(Gate.Cell[])tableLayout.Clone();
        changedFormula[11]=N();
        Assert(Gate.Evaluate(tableLayout,changedFormula).State==Gate.Status.RejectMismatch,
            "Book1 formulas replaced by numeric values block application");
        Console.WriteLine("All R107 guard tests passed. No Excel destination modified.");
        return 0;
    }
}
