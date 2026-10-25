using System;
using System.Collections.Generic;
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
        Console.WriteLine("All R107 guard tests passed. No Excel destination modified.");
        return 0;
    }
}
