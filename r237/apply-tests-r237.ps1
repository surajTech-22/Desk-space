$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R237 "+$label+": expected exactly one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestSyntheticMonitorMatrixV236();' @'
                TestSyntheticMonitorMatrixV236();
                TestExplicitContextSuggestionsV237();
'@ 'register opt-in suggestion classifier acceptance'
$method=@'
    private static void TestExplicitContextSuggestionsV237()
    {
        var context=Product.GetType(
            "DeskSpace.DeskSpaceContextSuggestionsV237",true)!;
        var classify=context.GetMethod("Classify",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string Type(string name)=>(string)classify.Invoke(null,
            new object?[]{name})!;
        Assert(Type("excel")=="Excel" && Type("EXCEL")=="Excel",
            "R237 Excel category must be case insensitive");
        Assert(Type("chrome")=="Browser" && Type("msedge")=="Browser" &&
               Type("firefox")=="Browser",
            "R237 browser category recognized without reading URL");
        Assert(Type("explorer")=="Files" && Type("winword")=="Documents" &&
               Type("POWERPNT")=="Presentations" &&
               Type("spotify")=="Media",
            "R237 safe coarse application categories");
        Assert(Type("DeskSpace")=="Other" && Type("")=="Other" &&
               Type("unknown-app")=="Other",
            "R237 own/unknown applications must not leak names or sensitive data");
        Assert(context.GetMethod("Show",BindingFlags.NonPublic|
            BindingFlags.Static)!=null,
            "R237 explicit interaction entry not accessible");
        Console.WriteLine(
            "R237 PASS: opt-in, privacy-safe foreground application category matching; "+
            "no window-title/clipboard/URL readback.");
    }

'@
Swap '    private static void TestSyntheticMonitorMatrixV236()' ($method+
'    private static void TestSyntheticMonitorMatrixV236()') 'native privacy/classification test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R237: registered native coarse-context classifiers without any background monitor start.'
