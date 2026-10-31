$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R238 "+$why+": expected 1 anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestExplicitContextSuggestionsV237();' @'
                TestExplicitContextSuggestionsV237();
                TestStrictDeckOffsetRecoveryV238();
'@ 'invoke native monitor offset validation'
$method=@'
    private static void TestStrictDeckOffsetRecoveryV238()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var target=Product.GetType("DeskSpace.DeskSpaceDeckOffsetV228",true)!;
        var parse=target.GetMethod("ParseForWrite",flags)!;
        System.Collections.IDictionary Parse(string? json,int length)=>
            (System.Collections.IDictionary)parse.Invoke(
                null,new object?[]{json,length})!;
        bool Invalid(string? json,int length)
        {
            try{Parse(json,length);return false;}
            catch(TargetInvocationException ex)
            {
                return ex.InnerException is System.IO.InvalidDataException;
            }
        }
        Assert(Parse(null,0).Count==0,
            "R238 new layout store begins empty");
        string valid="{\"DISPLAY1\":{\"X\":20,\"Y\":-50},\"DISPLAY2\":{\"X\":-80,\"Y\":240}}";
        var map=Parse(valid,System.Text.Encoding.UTF8.GetByteCount(valid));
        Assert(map.Count==2 && map.Contains("display1") && map.Contains("display2"),
            "R238 both monitor positions must survive case-insensitive reads");
        var second=map["DISPLAY2"]!;
        Assert((int)second.GetType().GetProperty("X")!.GetValue(second)! == -80,
            "R238 secondary monitor offset changed in parsed map");
        Assert(Invalid("{not json}",10) && Invalid("null",4) &&
               Invalid("{\"DISPLAY1\":null}",17),
            "R238 malformed JSON/null saved offsets must block overwrite");
        Assert(Invalid("{\"DISPLAY1\":{\"X\":1,\"Y\":2},\"display1\":{\"X\":9,\"Y\":9}}",52),
            "R238 duplicate case-insensitive monitor names must not be merged silently");
        Assert(Invalid(valid,16_385) && Invalid(valid,-1),
            "R238 existing oversized/invalid map byte length must fail closed");
        string longName=new string('Z',129);
        string bad="{\""+longName+"\":{\"X\":0,\"Y\":0}}";
        Assert(Invalid(bad,System.Text.Encoding.UTF8.GetByteCount(bad)),
            "R238 reject excessive monitor identity length");
        Console.WriteLine(
            "R238 PASS: valid two-monitor Deck positions retained; corrupted, null, "+
            "duplicate-case and oversized layout maps fail closed without data writes.");
    }

'@
Swap '    private static void TestExplicitContextSuggestionsV237()' ($method+
 '    private static void TestExplicitContextSuggestionsV237()') 'insert read-only offset recovery tests'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R238: native bad/duplicate/oversized Deck offset validation and case-insensitive two-monitor readback tests registered.'
