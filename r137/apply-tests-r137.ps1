$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$test='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($test).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label){
 $count=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R137 $label: expected one insertion; got $count"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            TestExcelTableRenderedPolicyV135();
'@ @'
            TestExcelTableRenderedPolicyV135();
            TestReleaseNotificationV137();
'@ 'run compiled notification safety tests'
Swap @'
    private static void TestExcelTableRenderedPolicyV135()
'@ @'
    private static void TestReleaseNotificationV137()
    {
        var receiver=Product.GetType("DeskSpace.DeskSpaceReleasePushV133",true)!;
        var parse=receiver.GetMethod("TryReleaseEvent",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var notice=Product.GetType("DeskSpace.DeskSpaceReleaseNoticeV137",true)!;
        Assert(notice.GetMethod("Show",BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "Non-modal verified-release WPF notification must be available");
        string Message(string channel,string tag)=>
            System.Text.Json.JsonSerializer.Serialize(new {
                @event="message",
                topic="deskspace-verified-release-r133-20261026-9a6c412f",
                message=channel+"|"+tag
            });
        bool Match(string raw,string channel)
        {
            object?[] args={raw,channel,null};
            bool matched=(bool)parse.Invoke(null,args)!;
            if(matched)
                Assert(((string)args[2]!).Length>4,
                    "Valid release event did not preserve its tag");
            return matched;
        }
        Assert(Match(Message("stable","v99.99.999"),"stable"),
            "Valid verified stable release hint not recognized");
        Assert(Match(Message("tester","tester-v99.99.999"),"tester"),
            "Valid tester release hint not recognized");
        Assert(!Match(Message("tester","tester-v99.99.999"),"stable"),
            "Cross-channel event must be ignored");
        Assert(!Match(Message("stable","v99.99.999"),"tester"),
            "Cross-channel stable event must be ignored");
        Assert(!Match(Message("stable","v1.0.1"),"stable"),
            "Older replayed releases must not re-trigger updates");
        Assert(!Match(Message("stable","https://unsafe.example"),"stable"),
            "Untrusted release event cannot supply arbitrary URLs");
        Console.WriteLine("R137 PASS: cached release events parsed per channel, stale and unsafe hints rejected, WPF notice compiled.");
    }
    private static void TestExcelTableRenderedPolicyV135()
'@ 'compiled WPF regression for event routing'
[IO.File]::WriteAllText($test,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R137 compiled smoke tests installed for tester/stable event parsing, replayed old versions and WPF notice accessibility.'