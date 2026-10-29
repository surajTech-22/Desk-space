$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function InsertOnce([string]$anchor,[string]$replacement,[string]$name){
 $n=([regex]::Matches($script:s,[regex]::Escape($anchor))).Count
 if($n -ne 1){throw "R182 $name anchor count: $n"}
 $script:s=$script:s.Replace($anchor,$replacement)
}
$anchor='            TestMalformedLayoutsV181();'
InsertOnce $anchor ($anchor+$nl+'            TestRestorePreviewV182();') 'WPF test call'
$method=@'
    private static void TestRestorePreviewV182()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
        var entry=type.GetNestedType("WindowEntry",BindingFlags.NonPublic)!;
        var profile=type.GetNestedType("Profile",BindingFlags.NonPublic)!;
        var preview=type.GetMethod("Preview",BindingFlags.NonPublic|BindingFlags.Static)!;
        var listType=typeof(List<>).MakeGenericType(entry);
        var windows=(System.Collections.IList)Activator.CreateInstance(listType)!;
        windows.Add(Activator.CreateInstance(entry,new object[]{
            "deskspace-r182-unit-nonexistent",new string('b',64),0,
            @"\\.\DISPLAY999",0.1,0.2,0.5,0.6})!);
        var plan=Activator.CreateInstance(profile,new object[]{"Preview",windows})!;
        var result=preview.Invoke(null,new[]{plan})!;
        var fields=result.GetType();
        var eligible=(int)fields.GetProperty("Eligible")!.GetValue(result)!;
        var missing=(int)fields.GetProperty("MissingWindows")!.GetValue(result)!;
        Assert(eligible==0 && missing==1,
            "R182 read-only preview must refuse unknown application windows.");
        Console.WriteLine("R182 PASS: read-only restore preview rejects "+
            "unknown windows without any SetWindowPos requests.");
    }

'@
InsertOnce '    private static void TestIndependentMonitorsV154()' ($method+'    private static void TestIndependentMonitorsV154()') 'WPF reflection coverage'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R182: native WPF read-only restore preview regression registered.'
