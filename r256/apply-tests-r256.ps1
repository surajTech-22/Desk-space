$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestFeatureActionGuardV255();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){throw 'R256 smoke call anchor missing'}
$s=$s.Replace($a,$a+$lf+'                TestFolderPreDropV256();')
$anchor='    private static void TestFeatureActionGuardV255()'
$method=@'
    private static void TestFolderPreDropV256()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceFolderPreDropV256",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var hover=type.GetMethod("Hover",flags)!;
        var authorize=type.GetMethod("Authorized",flags)!;
        object Hover(bool zip,bool copy)=>
            hover.Invoke(null,new object[]{zip,copy})!;
        var zipChoice=Hover(true,false);
        var copyChoice=Hover(false,true);
        var noChoice=Hover(false,false);
        Assert(zipChoice.ToString()=="Zip"&&copyChoice.ToString()=="Copy"&&
               noChoice.ToString()=="None"&&Hover(true,true).ToString()=="None",
            "R256 ZIP/Copy requires hovering precisely one option during drag");
        bool Approved(object choice,string[]? pending,string[]? dropped)=>
            (bool)authorize.Invoke(null,new object?[]{choice,pending,dropped})!;
        var paths=new[]{"C:\\test\\One","D:\\test\\Two"};
        Assert(Approved(zipChoice,paths,paths) &&
               Approved(copyChoice,paths,new[]{"c:\\test\\one","d:\\test\\two"}),
            "R256 preselected ZIP/Copy source identity required");
        Assert(!Approved(noChoice,paths,paths) &&
               !Approved(zipChoice,paths,new[]{"C:\\test\\Changed"}) &&
               !Approved(zipChoice,null,paths) &&
               !Approved(zipChoice,paths,null) &&
               !Approved(zipChoice,Array.Empty<string>(),Array.Empty<string>()),
            "R256 no hover, changed source or absent choice must reject Drop");
        var many=Enumerable.Range(0,129).Select(i=>"C:\\folder"+i).ToArray();
        Assert(!Approved(zipChoice,many,many),
            "R256 oversized drop source list must fail closed");
        Console.WriteLine("R256 PASS: during-drag single ZIP/Copy hover, "+
            "exact file identity, missing-choice and 128-source bounds.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){throw 'R256 native method anchor missing'}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R256: native pre-drop ZIP/Copy authorization test registered.'
