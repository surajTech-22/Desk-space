$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$from='            TestClipboardConversionsV198();'
$to=$from+$lf+'            TestVisibleFeatureHubV199();'
$n=([regex]::Matches($s,[regex]::Escape($from))).Count
if($n -ne 1){throw ("R199 native smoke invocation expected one anchor, got "+$n)}
$s=$s.Replace($from,$to)
$from='    private static void TestUnifiedActionPaletteV197()'
$method=@'
    private static void TestVisibleFeatureHubV199()
    {
        var main=Product.GetType("DeskSpace.MainWindow",true)!;
        var init=main.GetMethod("InitializeFeatureHubV199",
            BindingFlags.Instance|BindingFlags.NonPublic);
        var navigate=main.GetMethod("SelectExistingFeatureTabV199",
            BindingFlags.Instance|BindingFlags.NonPublic);
        Assert(init!=null && navigate!=null &&
            init.GetParameters().Length==0 &&
            navigate.GetParameters().Length==1,
            "R199 Workspace must expose its Features tab and existing-tab navigation");
        var search=Product.GetType("DeskSpace.DeskSpaceSearchV11",true)!;
        Assert(search.GetMethod("Create",
            BindingFlags.Instance|BindingFlags.Public)!=null,
            "R199 visible Action launcher must be attached to existing donut Search");
        Console.WriteLine("R199 PASS: compiled Workspace Features tab, navigation and widget Search entry.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($from))).Count
if($n -ne 1){throw ("R199 native smoke method expected one anchor, got "+$n)}
$s=$s.Replace($from,$method.Replace($crlf,$lf)+$from)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R199: native smoke reflection checks for real compiled Workspace and Search feature entry points.'
