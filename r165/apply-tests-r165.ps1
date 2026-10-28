$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$purpose)
{
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R165 smoke $purpose expected 1 anchor, found $count"}
    $script:s=$script:s.Replace($old,$new)
}
Swap '            TestCancellableCopyV164();' @'
            TestCancellableCopyV164();
            TestInterruptedCopyRecoveryV165();
'@ 'test entry point'
$method=@'
    private static void TestInterruptedCopyRecoveryV165()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceInterruptedCopyV165",true)!;
        var begin=type.GetMethod("Begin",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var prune=type.GetMethod("PruneAbandoned",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var root=Path.Combine(outdir,"r165-staging-"+Guid.NewGuid().ToString("N"));
        var session=Path.Combine(root,Guid.NewGuid().ToString("N"));
        var item=Path.Combine(session,Guid.NewGuid().ToString("N"));
        var destination=Path.Combine(item,"held.txt");
        var partial=destination+".partial";
        Directory.CreateDirectory(item);
        var external=Path.Combine(outdir,"r165-original-"+Guid.NewGuid().ToString("N")+".txt");
        File.WriteAllText(external,"real original stays unchanged");
        var marker=Path.Combine(item,".deskspace-copy-v165.lock");
        var age=DateTime.UtcNow.AddDays(-3);
        try
        {
            using(var lease=(IDisposable)begin.Invoke(null,new object[]{destination})!)
            {
                File.WriteAllText(partial,"abandoned bytes");
                Assert(File.Exists(marker),
                    "R165 copy lease must persist an exclusive marker while active");
            }
            Assert(!File.Exists(marker),
                "R165 normal completion must remove temporary marker");
            var encoded=Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("held.txt"));
            File.WriteAllText(marker,"DeskSpaceCopyV165|"+encoded);
            File.SetLastWriteTimeUtc(marker,age);
            File.SetLastWriteTimeUtc(partial,age);
            // A second live copy process still owns the marker. Even an
            // old timestamp must not persuade startup recovery to delete it.
            using(var live=new FileStream(marker,FileMode.Open,FileAccess.ReadWrite,
                FileShare.None))
            {
                var skipped=(int)prune.Invoke(null,
                    new object[]{root,TimeSpan.FromHours(24)})!;
                Assert(skipped==0&&File.Exists(partial),
                    "R165 recovery must not touch a locked active copy");
            }
            var removed=(int)prune.Invoke(null,
                new object[]{root,TimeSpan.FromHours(24)})!;
            Assert(removed==1&&!File.Exists(partial)&&!File.Exists(marker),
                "R165 recovery must clean a stale marked partial after lock release");

            // A completed Deck copy must be retained even if the process
            // crashed before its final marker-delete operation.
            File.WriteAllText(destination,"completed copy");
            File.WriteAllText(marker,"DeskSpaceCopyV165|"+encoded);
            File.SetLastWriteTimeUtc(marker,age);
            removed=(int)prune.Invoke(null,
                new object[]{root,TimeSpan.FromHours(24)})!;
            Assert(removed==1&&File.Exists(destination)&&
                File.ReadAllText(destination)=="completed copy",
                "R165 recovery must never delete a successfully staged copy");

            File.WriteAllText(marker,"untrusted marker format");
            File.WriteAllText(partial,"must stay");
            File.SetLastWriteTimeUtc(marker,age);
            File.SetLastWriteTimeUtc(partial,age);
            removed=(int)prune.Invoke(null,
                new object[]{root,TimeSpan.FromHours(24)})!;
            Assert(removed==0&&File.Exists(partial),
                "R165 unknown marker must preserve unrecognized files");
            File.Delete(marker);
            File.WriteAllText(marker,"DeskSpaceCopyV165|"+encoded);
            File.SetLastWriteTimeUtc(marker,age);
            File.SetLastWriteTimeUtc(partial,DateTime.UtcNow);
            removed=(int)prune.Invoke(null,
                new object[]{root,TimeSpan.FromHours(24)})!;
            Assert(removed==0&&File.Exists(partial),
                "R165 fresh partial must be retained even with old marker");
            Assert(File.ReadAllText(external)=="real original stays unchanged",
                "R165 startup cleanup must never change user source files");
            Console.WriteLine("R165 PASS: active-lock preservation, stale partial cleanup, "+
                "completed held-file preservation, unknown marker and freshness gates.");
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
            if(File.Exists(external))File.Delete(external);
        }
    }

'@
Swap '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'native WPF archive cleanup test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R165: native real-filesystem regression for startup recovery and exclusive copy markers.'
