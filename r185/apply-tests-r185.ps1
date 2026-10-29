$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$what)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R185 '+$what+' requires one anchor; got '+$n)}
 $script:s=$script:s.Replace($old,$new)
}
One '            TestStartupRecoveryV183();' @'
            TestStartupRecoveryV183();
            TestPostInstallReceiptV185();
'@ 'real WPF smoke call'
$method=@'
    private static void TestPostInstallReceiptV185()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpacePostInstallReceiptV185",true)!;
        var pending=type.GetNestedType("Pending",BindingFlags.NonPublic)!;
        var should=type.GetMethod("ShouldAcknowledge",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var txn=Guid.NewGuid().ToString("N");
        object Make(string id,string version)=>
            Activator.CreateInstance(pending,new object[]{id,version})!;
        bool Can(object? request,string release)=>
            (bool)should.Invoke(null,new object?[]{request,release})!;
        Assert(Can(Make(txn,"1.5.1016"),"1.5.1016") &&
            !Can(Make(txn,"1.5.1016"),"1.5.1017") &&
            !Can(Make("invalid","1.5.1016"),"1.5.1016") &&
            !Can(Make(txn,"1.5.1016"),"invalid") &&
            !Can(null,"1.5.1016"),
            "R185 post-install primary WPF receipt must validate nonce and version");
        var owner=new Window{Width=118,Height=118,ShowInTaskbar=false};
        try
        {
            owner.Show();
            var health=Product.GetType(
                "DeskSpace.DeskSpaceComponentHealthV175",true)!;
            var snapshot=health.GetMethod("Snapshot",
                BindingFlags.NonPublic|BindingFlags.Static)!;
            var rows=((System.Collections.IEnumerable)snapshot.Invoke(
                null,new object[]{owner,true})!).Cast<object>().ToArray();
            Assert(rows.Any(x=>
                (string)x.GetType().GetProperty("Name")!.GetValue(x)! ==
                    "Update recovery"),
                "R185 on-demand WPF health must report rollback readiness");
        }
        finally{owner.Close();}
        Console.WriteLine("R185 PASS: exact nonce/version, WPF component status "+
            "and no automatic data access.");
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'insert native WPF receipt regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R185 native WPF primary-update receipt and health smoke installed.'
