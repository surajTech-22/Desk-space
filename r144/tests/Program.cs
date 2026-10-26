using System;
using System.Collections.Generic;
using System.IO;
using DeskSpace;
using P=DeskSpace.DeskSpaceExcelFormulaOnlyPolicyV144;

internal static class Program
{
    static int checks;
    static void Check(bool ok,string message)
    {
        checks++;
        if(!ok)throw new Exception("R144 FAIL: "+message);
    }
    static void Reject(Action test,string message)
    {
        bool rejected=false;
        try{test();}
        catch(InvalidDataException){rejected=true;}
        Check(rejected,message);
    }
    static P.Source[] Source()=>new[]{
        new P.Source(0,1,"=SUM(A1:A3)","=SUM(RC[-1]:R[2]C[-1])"),
        new P.Source(2,1,"=$A$1*B3","=R1C1*RC")
    };
    static P.Target[] Empty()=>new[]{
        new P.Target(0,1,false,false,""),
        new P.Target(2,1,false,false,"")
    };
    static int Main()
    {
        try
        {
            var sources=Source();
            var empty=Empty();
            var plan=P.Plan(sources,empty,3,3);
            Check(plan.Assignments.Length==2 && plan.AlreadyPresent==0,
                "paste formulas into an entirely blank target, unlike R143");
            Check(plan.Assignments[0].R1C1=="=SUM(RC[-1]:R[2]C[-1])",
                "preserve native relative R1C1 formula text");
            Check(plan.Assignments[1].R1C1=="=R1C1*RC",
                "preserve native $ absolute cell reference semantics");
            var same=new[]{
                new P.Target(0,1,true,true,sources[0].R1C1),
                new P.Target(2,1,false,false,"")
            };
            plan=P.Plan(sources,same,3,3);
            Check(plan.Assignments.Length==1&&plan.AlreadyPresent==1,
                "skip existing exactly equal formulas");
            Reject(()=>P.Plan(sources,new[]{
                    new P.Target(0,1,true,true,"=1+1"),empty[1]},3,3),
                "reject conflicting pre-existing formula");
            Reject(()=>P.Plan(sources,new[]{
                    new P.Target(0,1,false,true,""),empty[1]},3,3),
                "reject overwriting existing user data");
            Reject(()=>P.Plan(Array.Empty<P.Source>(),empty,3,3),
                "do not run Paste Special without saved formulas");
            Reject(()=>P.Plan(new[]{sources[0],sources[0]},empty,3,3),
                "reject duplicate formula coordinates");
            Reject(()=>P.Plan(new[]{new P.Source(9,1,"=1","=1")},empty,3,3),
                "reject source outside captured selection");
            Reject(()=>P.Plan(new[]{new P.Source(0,1,"","")},empty,3,3),
                "reject missing formula expression");
            Reject(()=>P.Plan(sources,empty,0,3),
                "reject invalid captured geometry");
            Check(P.Plan(sources,new[]{
                    new P.Target(0,1,true,true,sources[0].R1C1),
                    new P.Target(2,1,true,true,sources[1].R1C1)
                },3,3).Assignments.Length==0,
                "repeated paste of identical formulas is a no-op");
            Console.WriteLine("R144 PASS: "+checks+" formula-only paste planning checks.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
