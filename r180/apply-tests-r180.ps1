$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
$anchor='            TestWindowLayoutsV179();'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R180 requires existing R179 WPF regression entry'}
$s=$s.Replace($anchor,$anchor+[string][char]10+'            TestPortableLayoutsV180();')
$method=@'
    private static void TestPortableLayoutsV180()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
        var entry=type.GetNestedType("WindowEntry",BindingFlags.NonPublic)!;
        var profile=type.GetNestedType("Profile",BindingFlags.NonPublic)!;
        var item=Activator.CreateInstance(entry,new object[]{
            "notepad",new string('a',64),0,@"\\.\DISPLAY1",0.1,0.2,0.5,0.6})!;
        var listType=typeof(List<>).MakeGenericType(entry);
        var list=(System.Collections.IList)Activator.CreateInstance(listType)!;
        list.Add(item);
        var p=Activator.CreateInstance(profile,new object[]{"Office",list})!;
        var export=type.GetMethod("ExportProfile",BindingFlags.NonPublic|BindingFlags.Static)!;
        var import=type.GetMethod("ImportProfile",BindingFlags.NonPublic|BindingFlags.Static)!;
        var json=(string)export.Invoke(null,new[]{p})!;
        Assert(json.Contains("Office")&&json.Contains("notepad")&&
            !json.Contains("Customer invoice"),
            "R180 portable JSON must include layout coordinates only");
        var loaded=import.Invoke(null,new object[]{json})!;
        Assert((string)profile.GetProperty("Name")!.GetValue(loaded)!=="Office",
            "R180 portable profile roundtrip must preserve name");
        foreach(var invalid in new[]{"{}",new string('X',131073),
            "{\"Name\":\"bad/path\",\"Windows\":[]}"})
        {
            try{import.Invoke(null,new object[]{invalid});
                throw new Exception("R180 unsafe portable profile was accepted");}
            catch(TargetInvocationException e) when(
                e.InnerException is ArgumentException){}
        }
        Console.WriteLine("R180 PASS: portable layouts roundtrip and reject "+
            "untrusted names, unbounded data or invalid schema.");
    }

'@
$insert='    private static void TestIndependentMonitorsV154()'
if(([regex]::Matches($s,[regex]::Escape($insert))).Count -ne 1){
 throw 'R180 test method anchor mismatch'}
$s=$s.Replace($insert,$method+$insert)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R180: native WPF suite extended to validate profile export/import roundtrip.'
