using System;
using System.Collections.Generic;
using System.IO;
using DeskSpace;

static class Program
{
    static int tests;
    static void Pass(bool condition,string message)
    {
        tests++;
        if(!condition)throw new Exception("FAILED: "+message);
    }
    static void Reject(Action action,string message)
    {
        bool rejected=false;
        try{action();}
        catch(InvalidDataException){rejected=true;}
        Pass(rejected,message);
    }
    static DeskSpaceExcelTemplateV16 Make()=>
        new(){
            Id=Guid.NewGuid(),
            SheetVisualV101=new(){
                Rows=1,Columns=3,Cells=new(){
                    new(){Row=0,Column=0,Address="$G$8",StoredType="Formula",
                        FormulaA1="=IF(F8=\"\",\"\",$F$8*G9)",
                        FormulaR1C1="=IF(RC[-1]=\"\",\"\",R8C6*R[1]C)"},
                    new(){Row=0,Column=1,Address="$H$8",StoredType="Formula",
                        FormulaA1="=SUM(G8:G10)",
                        FormulaR1C1="=SUM(RC[-1]:R[2]C[-1])"},
                    new(){Row=0,Column=2,Address="$I$8",StoredType="Empty"}
                }
            }
        };
    static int Main()
    {
        try
        {
            var source=Make();
            DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(source);
            Pass(true,"valid A1 and R1C1 formula expressions captured");
            var reloaded=Make(); reloaded.Id=source.Id;
            DeskSpaceExcelFormulaIntegrityV142.ValidateRoundtrip(source,reloaded);
            Pass(true,"exact formula characters roundtrip");
            reloaded.SheetVisualV101!.Cells[0].FormulaA1="=0";
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateRoundtrip(source,reloaded),
                "changed persisted formula rejected");
            reloaded=Make();reloaded.Id=source.Id;
            reloaded.SheetVisualV101!.Cells[0].FormulaR1C1="";
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(reloaded),
                "missing R1C1 expression rejected");
            reloaded=Make();reloaded.Id=source.Id;
            reloaded.SheetVisualV101!.Cells[0].FormulaA1="";
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(reloaded),
                "empty A1 expression rejected");
            reloaded=Make();reloaded.Id=source.Id;
            reloaded.SheetVisualV101!.Cells[2].FormulaA1="=1+1";
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(reloaded),
                "formula text without formula metadata rejected");
            reloaded=Make();reloaded.Id=source.Id;
            reloaded.SheetVisualV101!.Cells.RemoveAt(2);
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(reloaded),
                "partial captured range rejected");
            reloaded=Make();reloaded.Id=source.Id;
            reloaded.SheetVisualV101!.Cells[2].Column=1;
            Reject(()=>DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(reloaded),
                "duplicate physical cells rejected");
            Console.WriteLine("R142 formula integrity PASS: "+tests+" assertions.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
namespace DeskSpace
{
    internal sealed class DeskSpaceExcelTemplateV16
    {
        public Guid Id {get;set;}
        public DeskSpaceExcelSheetVisualV101? SheetVisualV101 {get;set;}
    }
    internal sealed class DeskSpaceExcelSheetVisualV101
    {
        public int Rows {get;set;}
        public int Columns {get;set;}
        public List<DeskSpaceExcelStyledCellV101> Cells {get;set;}=new();
    }
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
