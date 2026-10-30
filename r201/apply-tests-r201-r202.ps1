$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$nativeMode=@'
        try
        {
            if(args.Length>1 && args[1]=="--r201-only")
            {
                TestPortableProfilesV201();
                TestManualVaultSuggestionsV202();
                TestWorkflowDryRunV203();
                Console.WriteLine("R201-R203 isolated native WPF acceptance PASS.");
                app.Shutdown();
                return 0;
            }
'@.Replace($crlf,$lf)
$modeAnchor='        try'+$lf+'        {'+$lf+'            TestWeather();'
if(([regex]::Matches($s,[regex]::Escape($modeAnchor))).Count -ne 1){
 throw 'R201 isolated native entry anchor missing'
}
$s=$s.Replace($modeAnchor,$nativeMode+'            TestWeather();')
$old='            TestSubArcScreenFit();'
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R201 native test invocation anchor missing'
}
$s=$s.Replace($old,$old+$lf+
 '            TestPortableProfilesV201();'+$lf+
 '            TestManualVaultSuggestionsV202();')
$old='    private static void TestDeckVisibilityV89()'
$methods=@'
    private static void TestPortableProfilesV201()
    {
        var transfer=Product.GetType("DeskSpace.DeskSpaceProfilePortabilityV201",true)!;
        var parser=transfer.GetMethod("Parse",BindingFlags.Static|BindingFlags.NonPublic)!;
        string payload="{\"Format\":\"deskspace-widget-profiles-v1\",\"Profiles\":["+
            "{\"Name\":\"Work\",\"CenterMode\":\"Clock\",\"ShowWeather\":false,"+
            "\"WeatherCity\":\"\",\"PinnedApps\":[]}]}";
        var good=((System.Collections.IEnumerable)parser.Invoke(
            null,new object[]{payload})!).Cast<object>().ToArray();
        Assert(good.Length==1,"R201 must parse one allowlisted widget profile");
        bool Rejected(string json)
        {
            try{parser.Invoke(null,new object[]{json});return false;}
            catch(TargetInvocationException ex)
            {return ex.InnerException is FormatException or
                System.Text.Json.JsonException;}
        }
        Assert(Rejected("{\"Format\":\"wrong\",\"Profiles\":[]}") &&
            Rejected(payload.Replace("\"PinnedApps\":[]",
                "\"Password\":\"secret\",\"PinnedApps\":[]")) &&
            Rejected(payload.Replace("\"Name\":\"Work\"",
                "\"Name\":\"Work\\nBad\"")) &&
            Rejected(new string('x',262145)),
            "R201 must reject unrecognized fields, controls and oversized profiles");
        var profile=Product.GetType("DeskSpace.DeskSpaceWorkspaceProfilesV85",true)!;
        var importer=profile.GetMethod("ImportNewProfilesV201",
            BindingFlags.Static|BindingFlags.NonPublic);
        Assert(importer!=null,"R201 import must use original profile data owner");
        Console.WriteLine("R201 PASS: strict widget-only profile schema and atomic-import entry.");
    }

    private static void TestManualVaultSuggestionsV202()
    {
        var site=Product.GetType("DeskSpace.DeskSpaceVaultContextV202",true)!;
        var host=site.GetMethod("CanonicalHost",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        string? Host(string value,bool active)=>(string?)host.Invoke(
            null,new object?[]{value,active});
        Assert(Host("https://example.com/login",true)=="example.com" &&
            Host("https://example.com.evil.test",true)=="example.com.evil.test" &&
            Host("http://example.com",true)==null &&
            Host("https://user:pass@example.com",true)==null &&
            Host("https://127.0.0.1",true)==null &&
            Host("example.com",true)==null &&
            Host("example.com",false)=="example.com",
            "R202 must canonicalize only exact approved HTTPS DNS hosts");
        var path=Path.Combine(Path.GetTempPath(),
            "deskspace-v202-"+Guid.NewGuid().ToString("N")+".enc");
        try
        {
            using var vault=new DeskSpaceVaultV10(path);
            var match=site.GetMethod("Match",BindingFlags.Static|
                BindingFlags.NonPublic)!;
            object[] Lookup(string url)=>((System.Collections.IEnumerable)
                match.Invoke(null,new object[]{vault,url})!).Cast<object>().ToArray();
            Assert(Lookup("https://example.com").Length==0,
                "R202 may not query a locked vault");
            vault.Unlock("Synthetic passphrase for V202 testing only");
            vault.Upsert(new DeskSpaceVaultV10.Secret{
                Label="Example account",Username="test-user",
                Password="SyntheticSecretNotToExpose",
                Website="https://example.com/login"
            });
            Assert(Lookup("https://example.com").Length==1 &&
                Lookup("https://example.com.attacker.test").Length==0 &&
                Lookup("http://example.com").Length==0,
                "R202 must prevent origin suffix and HTTP matching");
            var result=Lookup("https://example.com")[0];
            Assert(result.GetType().GetProperty("Password")==null &&
                result.GetType().GetProperty("Username")==null,
                "R202 must return ONLY display labels, never secret fields");
            vault.Lock();
            Assert(Lookup("https://example.com").Length==0,
                "R202 must stop providing matches when locked again");
        }
        finally{if(File.Exists(path))File.Delete(path);}
        var modal=Product.GetType("DeskSpace.DeskSpaceVaultContextWindowV202",true)!;
        Assert(modal.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "R202 must have a manual, explicit entry point");
        Console.WriteLine("R202 PASS: exact HTTPS matching, locked state, metadata-only results.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R201 native test method anchor missing'
}
$s=$s.Replace($old,$methods.Replace($crlf,$lf)+$old)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R201/202 native WPF privacy and strict profile schema tests inserted.'
