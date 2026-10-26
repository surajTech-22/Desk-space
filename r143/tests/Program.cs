using System;
using System.Collections.Generic;
using DeskSpace;

static class Tests
{
    static int assertions;
    static void Expect(bool result,string label)
    {
        assertions++;
        if(!result)throw new Exception("R143 FAILED: "+label);
    }
    static List<DeskSpaceExcelStyledCellV101> Source()=>new()
    {
        new(){StoredType="Text"},
        new(){StoredType="Number"},
        new(){StoredType="Formula",FormulaA1="=SUM(A1:B1)",
            FormulaR1C1="=SUM(RC[-2]:RC[-1])"},
        new(){StoredType="Blank"}
    };
    static List<DeskSpaceExcelFullRangeGateV107.Cell> Target(
        bool first=true,bool second=true,bool formula=false,bool extra=false,
        string secondType="Number")=>new()
    {
        new(first,"Text"),
        new(second,secondType),
        new(formula,formula?"Formula":"Blank"),
        new(extra,extra?"Text":"Blank")
    };
    static int Main()
    {
        try
        {
            var source=Source();
            var candidate=Target();
            Expect(DeskSpaceExcelFormulaPasteV143.Ready(source,candidate),
                "all required input cells present with formula slot blank");
            var oldGate=DeskSpaceExcelFullRangeGateV107.Evaluate(
                new List<DeskSpaceExcelFullRangeGateV107.Cell>{
                    new(true,"Text"),new(true,"Number"),
                    new(true,"Formula"),new(false,"Blank")},candidate);
            Expect(!oldGate.MayApply,"legacy gate rejects formula-ready cells");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(source,Target(false,false)),
                "completely empty range must not show zero-valued formulas");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(source,Target(false,true)),
                "partially entered required input rejected");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(source,Target(true,false)),
                "another partially entered required input rejected");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(source,Target(true,true,true)),
                "existing formula may never be overwritten");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(source,Target(true,true,false,true)),
                "unrelated data in a saved blank position rejected");
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(
                source,Target(true,true,false,false,"Text")),
                "incompatible input type rejected");
            var corrupt=Source();
            corrupt[2].FormulaR1C1="";
            Expect(!DeskSpaceExcelFormulaPasteV143.Ready(corrupt,Target()),
                "formula marker without actual R1C1 expression rejected");
            var full=Target(true,true,true);
            Expect(DeskSpaceExcelFullRangeGateV107.Evaluate(
                new List<DeskSpaceExcelFullRangeGateV107.Cell>{
                    new(true,"Text"),new(true,"Number"),new(true,"Formula"),
                    new(false,"Blank")},full).MayApply,
                "already full target remains eligible without overwriting formulas");
            var realTarget=new FakeTarget();
            source[2].Row=0;source[2].Column=2;source[2].Address="$C$1";
            DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(realTarget,source);
            Expect(realTarget.Cells[1,3].FormulaR1C1=="=SUM(RC[-2]:RC[-1])",
                "insert actual captured formula into correct target cell");
            DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(realTarget,source);
            Expect(realTarget.Cells[1,3].FormulaR1C1=="=SUM(RC[-2]:RC[-1])",
                "identical table auto-filled formula is safe and not overwritten");
            realTarget.Cells[1,3].FormulaR1C1="=SUM(RC[-1])";
            bool wrongFormulaRejected=false;
            try{DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(realTarget,source);}
            catch(System.IO.InvalidDataException){wrongFormulaRejected=true;}
            Expect(wrongFormulaRejected,"different calculated table formula rejected");
            realTarget.Cells[1,3].HasFormula=false;
            realTarget.Cells[1,3].Value2=97;
            bool valueRejected=false;
            try{DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(realTarget,source);}
            catch(System.IO.InvalidDataException){valueRejected=true;}
            Expect(valueRejected,"existing user number never overwritten");
            Console.WriteLine("R143 PASS: "+assertions+" formula-footprint and no-zero guards.");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
namespace DeskSpace
{
    internal sealed class DeskSpaceExcelStyledCellV101
    {
        public int Row {get;set;}
        public int Column {get;set;}
        public string Address {get;set;}="";
        public string StoredType {get;set;}="";
        public string FormulaA1 {get;set;}="";
        public string FormulaR1C1 {get;set;}="";
    }
}

public sealed class FakeTarget
{
    public FakeCells Cells {get;}=new();
}
public sealed class FakeCells
{
    private readonly Dictionary<(int,int),FakeCell> cells=new();
    public FakeCell this[int row,int col]
    {
        get
        {
            if(!cells.TryGetValue((row,col),out var cell))
                cells[(row,col)]=cell=new FakeCell();
            return cell;
        }
    }
}
public sealed class FakeCell
{
    private string formula="";
    public bool HasFormula {get;set;}
    public object? Value2 {get;set;}
    public string FormulaR1C1
    {
        get=>formula;
        set {formula=value;HasFormula=true;}
    }
}
