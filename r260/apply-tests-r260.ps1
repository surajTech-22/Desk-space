$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestPaletteFacetsV259();'
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R260 invocation missing "+$n)}
$s=$s.Replace($a,$a+$lf+'                TestSearchIndexHealthV260();')
$anchor='    private static void TestPaletteFacetsV259()'
$method=@'
    private static void TestSearchIndexHealthV260()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSearchIndexHealthV260",true)!;
        var index=Product.GetType("DeskSpace.DeskSpaceSearchIndexV13",true)!;
        var palette=Product.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var statsType=type.GetNestedType("Stats",BindingFlags.NonPublic)!;
        var describe=type.GetMethod("Describe",flags)!;
        string Status(bool scanning,int volumes,int finished,long names)
        {
            var stats=Activator.CreateInstance(statsType,new object[]{
                scanning,volumes,finished,names})!;
            return (string)describe.Invoke(null,new object[]{stats})!;
        }
        Assert(Status(true,2,1,490).Contains("1 / 2") &&
               Status(true,2,1,490).Contains("490") &&
               Status(false,2,2,1000).Contains("Not currently scanning"),
            "R260 aggregate index progress/status is honest and contains counts");
        Assert(Status(false,0,0,0).Contains("No indexed volumes") &&
               Status(true,0,0,0).Contains("Discovering") &&
               Status(false,1,2,42).Contains("unavailable"),
            "R260 pre-index, scanning and invalid counters fail safely");
        Assert(index.GetMethod("SafeCountsV260",flags)!=null,
            "R260 actual existing multi-drive search index provides aggregate counters");
        var commands=((System.Collections.IEnumerable)palette.GetField(
            "Commands",flags)!.GetValue(null)!).Cast<object>();
        Assert(commands.Any(x=>(string)x.GetType()
                .GetProperty("Id")!.GetValue(x)! == "searchhealth"),
            "R260 index health must be discoverable through existing Action Palette");
        var owner=new Window{Width=165,Height=165,ShowInTaskbar=false};
        Window? dialog=null;
        try
        {
            owner.Show();
            dialog=(Window)type.GetMethod("Create",flags)!
                .Invoke(null,new object[]{owner})!;
            dialog.Show();dialog.UpdateLayout();
            var buttons=Walk(dialog).OfType<Button>()
                .Select(x=>x.Content?.ToString()).ToArray();
            Assert(buttons.Contains("Refresh status") &&
                   buttons.Contains("Start indexing names") &&
                   buttons.Contains("Close"),
                "R260 WPF health dialog requires explicit refresh/start/close, no automatic scan");
        }
        finally{dialog?.Close();owner.Close();}
        Console.WriteLine("R260 PASS: safe aggregate index status, "+
            "no pre-start scanning, explicit WPF indexing button, command discoverability.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R260 method anchor "+$n)}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R260: Windows WPF index status/explicit permission tests registered.'
