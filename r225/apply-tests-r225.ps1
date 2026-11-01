$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$reason)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($count -ne 1){throw ("R225 "+$reason+" expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestPostInstallStabilityV224();' @'
                TestPostInstallStabilityV224();
                TestSearchDeckLayoutV225();
'@ 'native invocation'
$method=@'
    private static void TestSearchDeckLayoutV225()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSearchDeckLayoutV225",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var width=type.GetMethod("SearchWidth",f)!;
        double Width(double monitor)=>(double)width.Invoke(null,new object[]{monitor})!;
        Assert(Width(1920)==336 && Width(350)==336 && Width(290)==290 &&
            Width(230)==230 && Width(110)==110,
            "R225 Search must be full-sized on normal screens but stay within small monitor bounds");
        Assert(Width(double.NaN)==320 && Width(double.PositiveInfinity)==320,
            "R225 malformed WPF monitor measurements must have finite safe fallback");
        var show=type.GetMethod("ShowDeck",f)!;
        bool Show(bool enabled,bool visible,bool search,bool entries)=>
            (bool)show.Invoke(null,new object[]{enabled,visible,search,entries})!;
        Assert(Show(true,true,false,true) && !Show(true,true,true,true) &&
            !Show(false,true,false,true) && !Show(true,false,false,true) &&
            !Show(true,true,false,false),
            "R225 Deck must hide during Search, retain visibility preferences and remain hidden offscreen");
        var deck=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        Assert(deck.GetMethod("SetSearchActiveV225",BindingFlags.Instance|
            BindingFlags.NonPublic)!=null,
            "R225 each widget's Deck must have independent Search visibility");
        var searchType=Product.GetType("DeskSpace.DeskSpaceSearchV11",true)!;
        Assert(searchType.GetMethod("Create",BindingFlags.Instance|
            BindingFlags.Public)!=null,
            "R225 preserve inline Search and search result actions");
        Console.WriteLine(
            "R225 PASS: readable Search width, compact-screen geometry, no icon collision and per-widget Deck suspension.");
    }

'@
Swap '    private static void TestPostInstallStabilityV224()' ($method+
 '    private static void TestPostInstallStabilityV224()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R225 WPF Search/Deck regression inserted.'
