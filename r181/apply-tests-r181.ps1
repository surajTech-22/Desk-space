$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$nl=[string][char]10
$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function InsertOnce([string]$anchor,[string]$replacement,[string]$name){
 $n=([regex]::Matches($script:s,[regex]::Escape($anchor))).Count
 if($n -ne 1){throw "R181 $name anchor count: $n"}
 $script:s=$script:s.Replace($anchor,$replacement)
}
$anchor='            TestPortableLayoutsV180();'
InsertOnce $anchor ($anchor+$nl+'            TestMalformedLayoutsV181();') 'test invocation'
$test=@'
    private static void TestMalformedLayoutsV181()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
        var entry=type.GetNestedType("WindowEntry",BindingFlags.NonPublic)!;
        var profile=type.GetNestedType("Profile",BindingFlags.NonPublic)!;
        var import=type.GetMethod("ImportProfile",BindingFlags.NonPublic|BindingFlags.Static)!;
        var valid=type.GetMethod("ValidProfile",BindingFlags.NonPublic|BindingFlags.Static)!;
        var restore=type.GetMethod("Restore",BindingFlags.NonPublic|BindingFlags.Static)!;
        var hash=new string('a',64);
        var badJson=new[]{
            "{\"Name\":\"Office\",\"Windows\":[null]}",
            "{\"Name\":\"Office\",\"Windows\":null}",
            "{\"Name\":\"Office\",\"Windows\":[{\"Process\":null,\"TitleHash\":\""+
                hash+"\",\"Instance\":0,\"Monitor\":\"DISPLAY\",\"X\":0,\"Y\":0,\"W\":.5,\"H\":.5}]}",
            "{\"Name\":\"Office\",\"Windows\":[{\"Process\":\"notepad\",\"TitleHash\":null,\"Instance\":0,\"Monitor\":\"DISPLAY\",\"X\":0,\"Y\":0,\"W\":0.5,\"H\":0.5}]}",
            "{\"Name\":\"Office\",\"Windows\":[{\"Process\":\"notepad\",\"TitleHash\":\""+
                hash+"\",\"Instance\":0,\"Monitor\":null,\"X\":0,\"Y\":0,\"W\":0.5,\"H\":0.5}]}"
        };
        foreach(var json in badJson)
        {
            try
            {
                import.Invoke(null,new object[]{json});
                throw new Exception("R181 accepted corrupt portable layout.");
            }
            catch(TargetInvocationException e) when(e.InnerException is ArgumentException){}
        }
        var listType=typeof(List<>).MakeGenericType(entry);
        var list=(System.Collections.IList)Activator.CreateInstance(listType)!;
        list.Add(Activator.CreateInstance(entry,new object?[]{
            null,hash,0,@"\\.\DISPLAY1",0.1,0.2,0.5,0.6})!);
        var invalid=Activator.CreateInstance(profile,new object[]{"Office",list})!;
        Assert(!(bool)valid.Invoke(null,new[]{invalid})!,
            "R181 null process must be rejected without a crash");
        var outcome=restore.Invoke(null,new[]{invalid})!;
        var restored=(int)outcome.GetType().GetField("Item1")!.GetValue(outcome)!;
        Assert(restored==0,"R181 invalid profiles must never move application windows");
        Console.WriteLine("R181 PASS: corrupt window profiles are rejected without "+
            "throwing unhandled NullReferenceException or moving windows.");
    }

'@
InsertOnce '    private static void TestIndependentMonitorsV154()' ($test+'    private static void TestIndependentMonitorsV154()') 'regression body'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R181: WPF test entry covers corrupt imports, invalid restore and null process fields.'
