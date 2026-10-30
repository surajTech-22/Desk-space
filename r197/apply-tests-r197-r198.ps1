$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$reason)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw ("R197/198 "+$reason+" expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '            TestPerMonitorDpiV195();' @'
            TestPerMonitorDpiV195();
            TestUnifiedActionPaletteV197();
            TestClipboardConversionsV198();
'@ 'test invocation after DPI acceptance'
$methods=@'
    private static void TestUnifiedActionPaletteV197()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
        var filter=type.GetMethod("Filter",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var commands=(System.Collections.IEnumerable)filter.Invoke(
            null,new object?[]{""})!;
        var all=commands.Cast<object>().ToArray();
        Assert(all.Length>=5,"R197 action palette must expose existing tool actions");
        string Id(object c)=>(string)c.GetType().GetProperty("Id")!.GetValue(c)!;
        string Name(object c)=>(string)c.GetType().GetProperty("Name")!.GetValue(c)!;
        bool Confirm(object c)=>(bool)c.GetType().GetProperty("Confirm")!.GetValue(c)!;
        Assert(all.Select(Id).Distinct().Count()==all.Length &&
            all.All(c=>!string.IsNullOrWhiteSpace(Name(c))),
            "R197 command names and IDs must be unique");
        Assert(all.Any(c=>Id(c)=="diagnostics" && Confirm(c)) &&
            all.Where(c=>Id(c)!="diagnostics").All(c=>!Confirm(c)),
            "R197 export must carry explicit confirmation");
        object[] Find(string query)=>((System.Collections.IEnumerable)filter.Invoke(
            null,new object?[]{query})!).Cast<object>().ToArray();
        Assert(Find("layout").Any(c=>Id(c)=="layouts") &&
            Find("clipboard").Any(c=>Id(c)=="clipboard") &&
            Find("unknown no-match string").Length==0 &&
            Find(new string('x',100)).Length==0,
            "R197 palette must filter safely without arbitrary execution");

        var create=type.GetMethod("Create",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var owner=new System.Windows.Window{Width=210,Height=150,
            ShowInTaskbar=false};
        owner.Show();
        try
        {
            var palette=(System.Windows.Window)create.Invoke(
                null,new object[]{owner,true})!;
            try
            {
                Assert(palette.Owner==owner && palette.Title=="DeskSpace actions",
                    "R197 command palette must be owned by the current widget");
                var inputs=Walk(palette).OfType<System.Windows.Controls.TextBox>().ToArray();
                var lists=Walk(palette).OfType<System.Windows.Controls.ListBox>().ToArray();
                Assert(inputs.Length==1 && lists.Length==1 &&
                    lists[0].Items.Count==all.Length,
                    "R197 native WPF search/list controls must exist");
                inputs[0].Text="clipboard";
                Assert(lists[0].Items.Count==1,
                    "R197 live WPF command filter must react to text changes");
            }
            finally{palette.Close();}
        }
        finally{owner.Close();}
        Console.WriteLine("R197 PASS: keyboard Action Palette, approved local actions and confirmation gate.");
    }

    private static void TestClipboardConversionsV198()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceClipboardTransformsV177",true)!;
        var transform=type.GetMethod("Transform",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Convert(string text,string mode)=>(string)transform.Invoke(
            null,new object[]{text,mode})!;
        string json=Convert("{\"count\":2,\"list\":[1,2]}","Format JSON");
        Assert(json.Contains(Environment.NewLine) &&
            json.Contains("\"count\": 2"),
            "R198 JSON formatter must preserve valid data with indentation");
        string csv=Convert("Name,Value\r\n\"A,B\",\"C\"\"D\"\r\nQ,2",
            "CSV to Markdown table");
        Assert(csv.Contains("| Name | Value |") &&
            csv.Contains("| A,B | C\"D |") &&
            csv.Contains("| Q | 2 |"),
            "R198 correct escaped quotes and embedded comma in CSV");
        Assert(Convert("a\r\nb\r\na\r\nb","Unique lines")==
            "a"+Environment.NewLine+"b",
            "R198 stable duplicate-line removal must preserve first occurrences");
        bool Rejected(string input,string mode)
        {
            try{Convert(input,mode);return false;}
            catch(TargetInvocationException ex)
            {
                return ex.InnerException is ArgumentException or
                    System.Text.Json.JsonException or FormatException;
            }
        }
        Assert(Rejected("{invalid","Format JSON") &&
            Rejected("Header,Value\n\"broken,2","CSV to Markdown table") &&
            Rejected("a,b\none","CSV to Markdown table") &&
            Rejected(new string('x',262145),"Unique lines"),
            "R198 malformed or oversized content must fail closed");
        Console.WriteLine("R198 PASS: explicit JSON, quoted CSV/Markdown and privacy-preserving line filtering.");
    }

'@
Swap '    private static void TestPerMonitorDpiV195()' ($methods.Replace($crlf,$lf)+
 '    private static void TestPerMonitorDpiV195()') 'native WPF methods'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R197-R198 native WPF and pure conversion regression tests registered.'
