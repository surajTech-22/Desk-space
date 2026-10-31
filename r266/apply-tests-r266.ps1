$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$anchor='                TestDeckSaveRecoveryV265();'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R266 native test registration missing R265 anchor'
}
$s=$s.Replace($anchor,$anchor+$lf+'                TestExcelFormulaAndFormatPasteV266();')
$methodAnchor='    private static void TestDeckSaveRecoveryV265()'
$method=@'
    private static void TestExcelFormulaAndFormatPasteV266()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceExcelFormulaPlanV266",true)!;
        var entry=type.GetNestedType("Input",BindingFlags.NonPublic)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var plan=type.GetMethod("Build",flags)!;
        var matches=type.GetMethod("Matches",flags)!;
        object Entry(int row,int col,string kind,string formula="",
            bool merge=false,bool header=false)=>
            Activator.CreateInstance(entry,
                new object[]{row,col,kind,formula,merge,header})!;
        object[] Build(params object[] inputs)
        {
            var typed=Array.CreateInstance(entry,inputs.Length);
            for(int i=0;i<inputs.Length;i++)typed.SetValue(inputs[i],i);
            return ((System.Collections.IEnumerable)plan.Invoke(
                null,new object[]{typed,2,2})!).Cast<object>().ToArray();
        }
        bool Reject(params object[] cells)
        {
            try{Build(cells);return false;}
            catch(TargetInvocationException exception)
            {return exception.InnerException is InvalidDataException;}
        }
        object[] Clean()=>
            new object[]{Entry(0,0,"Text"),Entry(0,1,"Formula","=RC[-1]*2"),
                Entry(1,0,"Blank"),Entry(1,1,"Blank")};
        var selected=Build(Clean());
        Assert(selected.Length==1 &&
               (int)selected[0].GetType().GetProperty("Row")!.GetValue(selected[0])! ==0 &&
               (int)selected[0].GetType().GetProperty("Column")!.GetValue(selected[0])! ==1 &&
               (string)selected[0].GetType().GetProperty("FormulaR1C1")!.GetValue(selected[0])!
                   =="=RC[-1]*2",
            "R266 only stored formulas survive, never adjacent source values or blank cells");
        Assert((bool)matches.Invoke(null,new object[]{selected[0],true,"=RC[-1]*2"})! &&
               !(bool)matches.Invoke(null,new object[]{selected[0],true,"=RC[-1]*3"})! &&
               !(bool)matches.Invoke(null,new object[]{selected[0],false,"=RC[-1]*2"})!,
            "R266 formula readback must be exact relative R1C1, not a displayed zero");
        Assert(Reject(Entry(0,0,"Text"),Entry(0,1,"Formula",""),
               Entry(1,0,"Blank"),Entry(1,1,"Blank")) &&
               Reject(Entry(0,0,"Text"),Entry(0,1,"Formula","=RC[-1]*2",header:true),
               Entry(1,0,"Blank"),Entry(1,1,"Blank")) &&
               Reject(Entry(0,0,"Text"),Entry(0,1,"Formula","=RC[-1]*2",merge:true),
               Entry(1,0,"Blank"),Entry(1,1,"Blank")) &&
               Reject(Entry(0,0,"Text","=RC[-1]"),Entry(0,1,"Formula","=RC[-1]*2"),
               Entry(1,0,"Blank"),Entry(1,1,"Blank")) &&
               Reject(Entry(0,0,"Text"),Entry(0,0,"Formula","=R1C1"),
               Entry(1,0,"Blank"),Entry(1,1,"Blank")),
            "R266 fail closed on missing/conflicting formula metadata, table header, merged and duplicate positions");
        Assert(Build(Entry(0,0,"Text"),Entry(0,1,"Text"),
            Entry(1,0,"Blank"),Entry(1,1,"Blank")).Length==0,
            "R266 format-only source without formulas must still work");
        Console.WriteLine("R266 PASS: exact source formula reconstruction plan, "+
            "no source body values/zeros, table/merged exclusions and strict R1C1 readback.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($methodAnchor))).Count -ne 1){
 throw 'R266 native method insertion anchor missing'
}
$s=$s.Replace($methodAnchor,$method.Replace($crlf,$lf)+$methodAnchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R266: native WPF synthetic Excel format+formula readback and negative cases registered.'
