$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R226 "+$label+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestSearchDeckLayoutV225();' @'
                TestSearchDeckLayoutV225();
                TestCompactDeckHeaderV226();
'@ 'native call'
$method=@'
    private static void TestCompactDeckHeaderV226()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDeckHeaderV226",true)!;
        var method=type.GetMethod("Format",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Title(bool copying,int a,int h,int n)=>
            (string)method.Invoke(null,new object[]{copying,a,h,n})!;
        Assert(Title(false,3,0,0)=="Deck · 3 apps" &&
            Title(false,1,0,0)=="Deck · 1 app" &&
            Title(false,0,0,0)=="Deck",
            "R226 zero-held/recent counters must not widen a running-app-only Deck");
        Assert(Title(false,0,2,0)=="Deck · 2 held" &&
            Title(false,2,0,1)=="Deck · 2 apps · 1 new" &&
            Title(false,2,3,4)=="Deck · 2 apps · 3 held · 4 new",
            "R226 retain actual nonzero held and new-file counts");
        Assert(Title(true,2,3,4)=="Copying…" &&
            Title(false,-1,-2,-3)=="Deck",
            "R226 Copy progress and corrupted count guards must stay clear");
        var deck=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        Assert(deck.GetMethod("SetSearchActiveV225",
            BindingFlags.Instance|BindingFlags.NonPublic)!=null,
            "R226 retain Search/Deck suspension while reducing heading length");
        Console.WriteLine(
            "R226 PASS: compact app-only title, nonzero held/new counters and safe header state.");
    }

'@
Swap '    private static void TestSearchDeckLayoutV225()' ($method+
'    private static void TestSearchDeckLayoutV225()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R226 compact Deck header native acceptance added.'
