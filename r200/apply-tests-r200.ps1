$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$before='            TestVisibleFeatureHubV199();'
$after=$before+$lf+'            TestCrossProcessFeatureNavigationV200();'
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R200 invocation "+$n)}
$s=$s.Replace($before,$after)
$before='    private static void TestVisibleFeatureHubV199()'
$method=@'
    private static void TestCrossProcessFeatureNavigationV200()
    {
        var nav=Product.GetType("DeskSpace.DeskSpaceFeatureNavigationV200",true)!;
        var allowed=nav.GetMethod("IsAllowed",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Allowed(string? word)=>(bool)allowed.Invoke(null,new object?[]{word})!;
        Assert(Allowed("FEATURES") && Allowed("NOTES") &&
            Allowed("REMINDERS") && !Allowed("EXCEL_MAPPING") &&
            !Allowed("NOTEPAD.EXE") && !Allowed("../workspace") &&
            !Allowed("features") && !Allowed(null),
            "R200 IPC must accept only exact allowlisted feature destinations");
        var workspace=Product.GetType("DeskSpace.MainWindow",true)!;
        Assert(workspace.GetMethod("ShowFeatureDestinationV200",
            BindingFlags.Instance|BindingFlags.NonPublic)!=null,
            "R200 must route to an existing Workspace tab, not a second store");
        var palette=Product.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
        var commands=palette.GetField("Commands",
            BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var names=((System.Collections.IEnumerable)commands).Cast<object>()
            .Select(c=>(string)c.GetType().GetProperty("Id")!.GetValue(c)!)
            .ToArray();
        Assert(new[]{"features","excel","reminders","notes"}.All(names.Contains),
            "R200 approved Workspace destinations must be discoverable in palette");
        Console.WriteLine("R200 PASS: fixed same-user IPC verbs and discoverable Workspace navigation.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R200 method "+$n)}
$s=$s.Replace($before,$method.Replace($crlf,$lf)+$before)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R200 native test ensures same-user IPC fixed verbs and all palette destinations.'
