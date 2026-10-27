using System;
using System.IO;
using DeskSpace;
using P=DeskSpace.DeskSpaceExcelCombinedFormulaV147;
internal static class Tests
{
    static int count;
    static void Expect(bool test,string label)
    {
        count++;
        if(!test)throw new InvalidOperationException("R147 failed: "+label);
    }
    static void Reject(Action act,string label)
    {
        bool failed=false;
        try{act();}
        catch(InvalidDataException){failed=true;}
        Expect(failed,label);
    }
    static P.Cell[] Source()=>new[]{
        new P.Cell(0,2,"=SUM(A1:B1)","=SUM(RC[-2]:RC[-1])"),
        new P.Cell(1,2,"=$A$1*B2","=R1C1*RC[-1]")
    };
    static P.Target[] Target(bool first=false,bool second=false)=>
        new[]{
            new P.Target(0,2,first,first,"=SUM(RC[-2]:RC[-1])"),
            new P.Target(1,2,second,second,"=R1C1*RC[-1]")
        };
    public static int Main()
    {
        try
        {
            var source=Source();
            var empty=P.Inspect(source,Target());
            Expect(empty.Writes.Length==2&&empty.ExistingMatches==0,
                "an entirely blank destination can receive both formulas");
            Expect(empty.Writes[0].FormulaR1C1=="=SUM(RC[-2]:RC[-1])",
                "relative references preserved in exact R1C1 text");
            Expect(empty.Writes[1].FormulaR1C1=="=R1C1*RC[-1]",
                "absolute $ references preserved in exact R1C1 text");
            var mixed=P.Inspect(source,Target(true,false));
            Expect(mixed.Writes.Length==1&&mixed.ExistingMatches==1,
                "a previously pasted matching formula is not overwritten");
            var repeated=P.Inspect(source,Target(true,true));
            Expect(repeated.Writes.Length==0&&repeated.ExistingMatches==2,
                "repeated combined paste is idempotent for matching formulas");
            Expect(P.Inspect(Array.Empty<P.Cell>(),Array.Empty<P.Target>()).Writes.Length==0,
                "format-only mappings remain functional");
            Reject(()=>P.Inspect(source,new[]{
                new P.Target(0,2,true,true,"=1+1"),Target()[1]}),
                "do not overwrite a different existing formula");
            Reject(()=>P.Inspect(source,new[]{
                new P.Target(0,2,false,true,""),Target()[1]}),
                "do not overwrite entered Excel values");
            Reject(()=>P.Inspect(source,Array.Empty<P.Target>()),
                "reject unchecked destination cells");
            Reject(()=>P.Inspect(new[]{source[0],source[0]},Target()),
                "reject duplicate saved formula locations");
            Reject(()=>P.Inspect(new[]{new P.Cell(0,2,"","")},Target()),
                "reject marker-only formulas");
            Reject(()=>P.Inspect(new[]{new P.Cell(-1,2,"=1","=1")},Target()),
                "reject invalid saved geometry");
            Console.WriteLine("R147 PASS: "+count+
                " combined formula security, idempotence and blank range assertions.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
