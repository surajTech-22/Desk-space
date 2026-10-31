$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R242 "+$why+" expected one anchor; got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestWordDeckPreviewV241();' @'
                TestWordDeckPreviewV241();
                TestExcelFailClosedTypesV242();
'@ 'register full-range negative type tests'
$method=@'
    private static void TestExcelFailClosedTypesV242()
    {
        var gate=Product.GetType("DeskSpace.DeskSpaceExcelFullRangeGateV107",true)!;
        var cell=gate.GetNestedType("Cell",BindingFlags.NonPublic)!;
        var evaluate=gate.GetMethod("Evaluate",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        object Decide((bool Filled,string? Type)[] source,
            (bool Filled,string? Type)[] target)
        {
            Array ToArray((bool Filled,string? Type)[] input)
            {
                var array=Array.CreateInstance(cell,input.Length);
                for(int i=0;i<input.Length;i++)
                    array.SetValue(Activator.CreateInstance(cell,
                        input[i].Filled,input[i].Type),i);
                return array;
            }
            return evaluate.Invoke(null,new object[]{ToArray(source),ToArray(target)})!;
        }
        string State(object outcome)=>outcome.GetType().GetProperty("State")!
            .GetValue(outcome)!.ToString()!;
        bool Allowed(object outcome)=>(bool)outcome.GetType()
            .GetProperty("MayApply")!.GetValue(outcome)!;
        var src=new[]{(true,(string?)"Formula"),(false,(string?)"Blank"),
                      (true,(string?)"Date")};
        var empty=new[]{(false,(string?)"Blank"),(false,(string?)"Blank"),
                        (false,(string?)"Blank")};
        var complete=new[]{(true,(string?)"Formula"),(false,(string?)"Blank"),
                           (true,(string?)"Number")};
        Assert(State(Decide(src,empty))=="EntirelyEmpty" &&
            State(Decide(src,complete))=="CompleteMatchingLayout",
            "R242 legitimate blank target and compatible formula/date structure remain eligible");
        Assert(!Allowed(Decide(src,new[]{(true,(string?)"Text"),
            (false,(string?)"Blank"),(true,(string?)"Number")})),
            "R242 text may never replace expected formula");
        Assert(!Allowed(Decide(src,new[]{(true,(string?)"Formula"),
            (false,(string?)"Blank"),(false,(string?)"Blank")})),
            "R242 partially populated destination must stay blocked");
        foreach(var invalid in new[]{
            (true,(string?)"Opaque"),
            (true,(string?)""),
            (true,(string?)null),
            (true,(string?)"Blank"),
            (false,(string?)"Text"),
            (false,(string?)"Formula")})
        {
            var broken=new[]{invalid,(false,(string?)"Blank"),
                (true,(string?)"Date")};
            var result=Decide(broken,empty);
            Assert(State(result)=="RejectMismatch"&&!Allowed(result),
                "R242 broken source storage/occupancy metadata blocked BEFORE empty-target eligibility: "+invalid);
        }
        var corruptTarget=new[]{(false,(string?)"UNKNOWN"),
            (false,(string?)"Blank"),(false,(string?)"Blank")};
        Assert(!Allowed(Decide(src,corruptTarget)),
            "R242 invalid blank destination storage must not masquerade as safe");
        Assert(!Allowed(Decide(Array.Empty<(bool,string?)>(),
            Array.Empty<(bool,string?)>())),
            "R242 missing source layout blocked");
        Console.WriteLine(
            "R242 PASS: Excel entire-range gate rejects malformed source and "+
            "destination metadata BEFORE blank approval; formulas, dates, "+
            "partial-filled and layout tests remain safe.");
    }

'@
Swap '    private static void TestWordDeckPreviewV241()' ($method+
 '    private static void TestWordDeckPreviewV241()') 'native fail-closed tests'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R242: native Excel preflight metadata safety tests registered.'
