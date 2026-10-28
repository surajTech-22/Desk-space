$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 if(([regex]::Matches($script:s,[regex]::Escape($before))).Count -ne 1)
   {throw "R162 test $why anchor mismatch"}
 $script:s=$script:s.Replace($before,$after)
}
Swap '            TestDeskSpaceFoundationV161();' @'
            TestDeskSpaceFoundationV161();
            TestCancellableZipV162();
'@ 'new real ZIP cancellation test entry'
$method=@'
    private static void TestCancellableZipV162()
    {
        var worker=Product.GetType("DeskSpace.DeskSpaceFolderZipV162",true)!;
        var zip=worker.GetMethod("Create",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var prune=worker.GetMethod("PruneAbandoned",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var input=Path.Combine(outdir,"r162-folder-"+Guid.NewGuid().ToString("N"));
        var inner=Path.Combine(input,"nested");
        var empty=Path.Combine(input,"empty");
        var sourceTemp=Path.Combine(outdir,"r162-temporary-"+Guid.NewGuid().ToString("N"));
        var success=Path.Combine(sourceTemp,"test.zip");
        var cancelled=Path.Combine(sourceTemp,"cancelled.zip");
        Directory.CreateDirectory(inner);
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(sourceTemp);
        File.WriteAllText(Path.Combine(input,"a.txt"),"original");
        File.WriteAllBytes(Path.Combine(inner,"big.dat"),new byte[2*1024*1024]);
        try
        {
            using(var cts=new System.Threading.CancellationTokenSource())
            {
                var count=0;
                var progress=new Action<int>(n=>{count=n;cts.Cancel();});
                try
                {
                    zip.Invoke(null,new object?[]{
                        input,cancelled,cts.Token,progress});
                    throw new Exception("R162 cancel did not stop ZIP");
                }
                catch(System.Reflection.TargetInvocationException e)
                    when(e.InnerException is OperationCanceledException){}
                Assert(count==1,
                    "R162 ZIP must report file count and stop on cancellation");
                Assert(!File.Exists(cancelled)&&!File.Exists(cancelled+".partial"),
                    "R162 cancelled ZIP must not leak a staged or partial file");
            }
            zip.Invoke(null,new object?[]{
                input,success,System.Threading.CancellationToken.None,null});
            using(var opened=System.IO.Compression.ZipFile.OpenRead(success))
            {
                var root=Path.GetFileName(input)+"/";
                Assert(opened.GetEntry(root+"nested/big.dat")!=null&&
                    opened.GetEntry(root+"empty/")!=null,
                    "R162 complete ZIP must retain nested and empty folders");
            }
            Assert(File.ReadAllText(Path.Combine(input,"a.txt"))=="original",
                "R162 cancelling or finishing ZIP must not change original");

            var staleRoot=Path.Combine(sourceTemp,"stale");
            var old=Path.Combine(staleRoot,Guid.NewGuid().ToString("N"));
            var unknown=Path.Combine(staleRoot,Guid.NewGuid().ToString("N"));
            var recent=Path.Combine(staleRoot,Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(old);
            Directory.CreateDirectory(unknown);
            Directory.CreateDirectory(recent);
            File.WriteAllText(Path.Combine(old,"a.zip.partial"),"old");
            File.WriteAllText(Path.Combine(unknown,"keep.txt"),"do not touch");
            File.WriteAllText(Path.Combine(recent,"active.zip.partial"),"active");
            var age=DateTime.UtcNow.AddDays(-3);
            foreach(var file in new[]{
                Path.Combine(old,"a.zip.partial"),
                Path.Combine(unknown,"keep.txt")})
                File.SetLastWriteTimeUtc(file,age);
            Directory.SetLastWriteTimeUtc(old,age);
            Directory.SetLastWriteTimeUtc(unknown,age);
            var removed=(int)prune.Invoke(null,new object[]{
                staleRoot,TimeSpan.FromHours(24)})!;
            Assert(removed==1&&!Directory.Exists(old)&&
                File.Exists(Path.Combine(unknown,"keep.txt"))&&
                File.Exists(Path.Combine(recent,"active.zip.partial")),
                "R162 cleanup must only remove old app-generated ZIP files");
            Console.WriteLine("R162 PASS: streaming ZIP cancellation, no partial leaks, "+
                "original preserved, nested content intact, safe stale cleanup.");
        }
        finally
        {
            if(Directory.Exists(input))Directory.Delete(input,true);
            if(Directory.Exists(sourceTemp))Directory.Delete(sourceTemp,true);
        }
    }

'@
Swap '    private static void TestIndependentMonitorsV154()' ($method.Replace($crlf,$lf)+
 '    private static void TestIndependentMonitorsV154()') 'insert native WPF ZIP worker test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R162 WPF regression installed: cancel ZIP, preserve originals, remove partial files, prune only stale ZIP temp.'
