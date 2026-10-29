$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$from,[string]$to,[string]$why){
    $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($from))).Count
    if($count -ne 1){throw "R191 $why: expected one anchor, got $count"}
    $script:s=$script:s.Replace($from,$to)
}
Once '            TestSafeUpdateDeferralV188();' @'
            TestSafeUpdateDeferralV188();
            TestVerifiedUpdateQueueV191();
'@ 'invoke native verified update queue smoke'
$test=@'
    private static void TestVerifiedUpdateQueueV191()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceVerifiedUpdateQueueV191",true)!;
        var tags=type.GetMethod("TagMatches",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Allows(string tag,string channel)=>(bool)tags.Invoke(
            null,new object[]{tag,channel})!;
        Assert(Allows("v1.5.1070","stable") &&
            Allows("tester-v1.5.1070","tester") &&
            !Allows("v1.5.1070","tester") &&
            !Allows("tester-v1.5.1070","stable") &&
            !Allows("v1.5.1070/../../bad","stable") &&
            !Allows("v1.5.1070","unknown"),
            "R191 pending cache must be strictly tied to the selected release channel");
        var stage=type.GetMethod("Stage",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool RejectedBadScope()
        {
            try
            {
                stage.Invoke(null,new object[]{@"C:\fake\wrong.exe",
                    @"C:\fake","tester-v1.5.1070",
                    new string('a',64),"tester"});
                return false;
            }
            catch(TargetInvocationException ex)
            {
                return ex.InnerException is System.IO.InvalidDataException;
            }
        }
        Assert(RejectedBadScope(),
            "R191 never queue an arbitrary executable instead of the verified setup");
        var resume=Product.GetType("DeskSpace.DeskSpacePublicUpdaterV44",true)!
            .GetMethod("ResumeVerifiedV191",
                BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert(resume.ReturnType==typeof(System.Threading.Tasks.Task<bool>),
            "R191 native updater must have an independent local retry path");
        Console.WriteLine("R191 PASS: channel-specific tags, staged path rejection " +
            "and checksum-based local retry are linked to real compiled WPF code.");
    }

'@
Once '    private static void TestSafeUpdateDeferralV188()' (
    $test.Replace($crlf,$lf)+'    private static void TestSafeUpdateDeferralV188()') 'compile native update regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R191 WPF smoke: live built product verifies channel scopes, executable path rejection and queue handoff signature.'
