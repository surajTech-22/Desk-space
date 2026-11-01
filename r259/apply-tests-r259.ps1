$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestContextPrivacyAuditV258();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){throw 'R259 acceptance call anchor unavailable'}
$s=$s.Replace($a,$a+$lf+'                TestPaletteFacetsV259();')
$anchor='    private static void TestContextPrivacyAuditV258()'
$method=@'
    private static void TestPaletteFacetsV259()
    {
        var palette=Product.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var search=palette.GetMethod("Filter",flags)!;
        object[] Filter(string query)=>((System.Collections.IEnumerable)
            search.Invoke(null,new object?[]{query})!).Cast<object>().ToArray();
        string Field(object command,string name)=>
            (string)command.GetType().GetProperty(name)!.GetValue(command)!;
        var all=Filter("");
        Assert(all.Length>=5 && all.Any(x=>Field(x,"Group")=="System") &&
            all.Any(x=>Field(x,"Group")=="Windows"),
            "R259 original command registrations must survive scoped filter");
        var systems=Filter("@System");
        var windows=Filter("@windows");
        Assert(systems.Length>0 &&
            systems.All(x=>Field(x,"Group")=="System") &&
            windows.Length>0 &&
            windows.All(x=>Field(x,"Group")=="Windows"),
            "R259 exact case-insensitive palette group facet");
        Assert(Filter("@does-not-exist").Length==0 &&
               Filter("@System @Windows").Length==0 &&
               Filter("@").Length==0 &&
               Filter(new string('x',769)).Length==0,
            "R259 invalid/ambiguous/oversized queries fail closed");
        Assert(Filter("@all").Length==all.Length &&
            Filter("@windows layout").Any(x=>Field(x,"Id")=="layouts"),
            "R259 unfiltered facet and in-group term matching");
        var policy=Product.GetType("DeskSpace.DeskSpacePaletteQueryV259",true)!;
        var navigate=policy.GetMethod("Navigate",flags)!;
        int Next(int current,int count,int delta)=>
            (int)navigate.Invoke(null,new object[]{current,count,delta})!;
        Assert(Next(0,12,7)==7 && Next(11,12,7)==11 &&
               Next(0,12,-7)==0 && Next(0,0,7)==-1,
            "R259 keyboard paging navigation must clamp without wrapping");
        var owner=new Window{Width=145,Height=145};
        Window? dialog=null;
        try
        {
            owner.Show();
            dialog=(Window)palette.GetMethod("Create",flags)!
                .Invoke(null,new object[]{owner,false})!;
            dialog.Show();dialog.UpdateLayout();
            var info=Walk(dialog).OfType<TextBlock>()
                .Select(x=>x.Text).ToArray();
            Assert(info.Any(x=>x.Contains("@Windows")) &&
                info.Any(x=>x.Contains("PgUp/PgDn")),
                "R259 real WPF palette exposes facet and keyboard instructions");
        }
        finally{dialog?.Close();owner.Close();}
        Console.WriteLine("R259 PASS: real Action Palette group facets, "+
            "term search, malformed/oversized filtering, keyboard paging and native UI.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1)
{throw 'R259 native method anchor missing'}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R259: Windows native WPF category search and keyboard tests registered.'
