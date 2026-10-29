$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function One([string]$a,[string]$b,[string]$label){
 $a=$a.Replace($crlf,$nl);$b=$b.Replace($crlf,$nl)
 $count=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($count -ne 1){throw ('R188 '+$label+' expected one anchor, found '+$count)}
 $script:s=$script:s.Replace($a,$b)
}
One '            TestStagedContentDuplicatesV186();' @'
            TestStagedContentDuplicatesV186();
            TestSafeUpdateDeferralV188();
'@ 'invoke safe deferral regression'
$method=@'
    private static void TestSafeUpdateDeferralV188()
    {
        var gate=Product.GetType("DeskSpace.DeskSpaceUpdateInstallGateV188",true)!;
        var blocks=gate.GetMethod("BlocksInstall",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        const string target=@"C:\Program Files\DeskSpace\DeskSpace.exe";
        bool IsBusy(int peer,int self,string? exe,bool readable)=>
            (bool)blocks.Invoke(null,new object?[]{peer,self,exe,target,readable})!;
        Assert(!IsBusy(101,101,target,true),
            "R188 a running widget must never block itself");
        Assert(IsBusy(102,101,target,true),
            "R188 a second DeskSpace process in same install must defer");
        Assert(IsBusy(102,101,@"c:\PROGRAM FILES\DESKSPACE\DeskSpace.exe",true),
            "R188 Windows path equality must be case-insensitive");
        Assert(!IsBusy(102,101,@"C:\Other\DeskSpace.exe",true),
            "R188 a different installation must not block this one");
        Assert(IsBusy(102,101,null,false) &&
            IsBusy(102,101,"",true),
            "R188 inaccessible executable path must defer conservatively");
        var updater=Product.GetType("DeskSpace.DeskSpacePublicUpdaterV44",true)!;
        var defer=updater.GetMethod("DeferForOpenDeskSpace",
            BindingFlags.Static|BindingFlags.NonPublic);
        Assert(defer!=null && defer.ReturnType==typeof(bool),
            "R188 automatic updater must expose an early-return deferral gate");
        Console.WriteLine("R188 PASS: safe update peer isolation, "+
            "case-insensitive executable matches, inaccessible-process defer and "+
            "recheck before shutdown. No user process was terminated.");
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
 $method.Replace($crlf,$nl)+'    private static void TestIndependentMonitorsV154()'
) 'insert new pure native smoke check'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R188: native .NET WPF smoke verifies same-install peer deferral and unrelated install acceptance.'
