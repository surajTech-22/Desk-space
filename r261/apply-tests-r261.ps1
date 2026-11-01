$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestSearchIndexHealthV260();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){throw 'R261 native call anchor missing'}
$s=$s.Replace($a,$a+$lf+'                TestRunningAppsOrderV261();')
$anchor='    private static void TestSearchIndexHealthV260()'
$method=@'
    private static void TestRunningAppsOrderV261()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceRunningAppsV88",true)!;
        var entry=type.GetNestedType("Entry",BindingFlags.NonPublic)!;
        var groups=type.GetMethod("GroupVisible",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var windows=type.GetMethod("WindowsFor",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        object App(string name,int hwnd,string title)=>Activator.CreateInstance(
            entry,new object[]{name,"",new IntPtr(hwnd),title})!;
        System.Collections.IList List(params object[] items)
        {
            var value=(System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(entry))!;
            foreach(var item in items)value.Add(item);
            return value;
        }
        object[] Group(System.Collections.IList list)=>
            ((System.Collections.IEnumerable)groups.Invoke(null,
                new object[]{list})!).Cast<object>().ToArray();
        string Name(object e)=>(string)e.GetType()
            .GetProperty("Name")!.GetValue(e)!;
        string Title(object e)=>(string)e.GetType()
            .GetProperty("WindowTitle")!.GetValue(e)!;
        var source=List(
            App("Alpha",10,"Alpha"),
            App("Beta",20,"B window"),
            App("Beta",21,"A window"),
            App("Beta",21,"duplicate same HWND"),
            App("Gamma",30,"Gamma"));
        var result=Group(source);
        Assert(result.Length==3 && Name(result[0])=="Beta" &&
               Name(result[1])=="Alpha" && Name(result[2])=="Gamma",
            "R261 two distinct Beta windows before single-window Alpha/Gamma, duplicate HWND ignored");
        var beta=(System.Collections.IEnumerable)windows.Invoke(null,
            new object[]{result[0],source})!;
        var betaWindows=beta.Cast<object>().ToArray();
        Assert(betaWindows.Length==2 &&
               Title(betaWindows[0])=="A window" &&
               Title(betaWindows[1])=="B window",
            "R261 grouped app flyout retains all windows in stable alphabetical window order");
        var singles=Group(List(App("Zulu",100,"z"),
            App("Alpha",101,"a"),App("Bravo",102,"b")));
        Assert(singles.Length==3 && Name(singles[0])=="Alpha" &&
               Name(singles[1])=="Bravo" && Name(singles[2])=="Zulu",
            "R261 all single-window apps must remain alphabetically ordered");
        var many=List(Enumerable.Range(0,32)
            .Select(i=>App("App"+i.ToString("D2"),i+500,"x")).ToArray());
        Assert(Group(many).Length==24,
            "R261 original running apps 24 icon limit preserved");
        Console.WriteLine("R261 PASS: multi-window groups first, "+
            "stable alphabetical singles and flyouts, unique HWND count, 24-item cap.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){throw 'R261 native test insertion anchor missing'}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R261: native running app/window grouping and sort tests registered.'
