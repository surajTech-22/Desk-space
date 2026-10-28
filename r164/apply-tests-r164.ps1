$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$purpose)
{
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    if(([regex]::Matches($script:s,[regex]::Escape($old))).Count -ne 1)
        {throw "R164 WPF test insertion failed: $purpose"}
    $script:s=$script:s.Replace($old,$new)
}
Swap '            TestSearchableZipV163();' @'
            TestSearchableZipV163();
            TestCancellableCopyV164();
'@ 'R164 WPF integration test runner'

$method=@'
    private static void TestCancellableCopyV164()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceFolderCopyV164",true)!;
        var copy=type.GetMethod("Copy",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var source=Path.Combine(outdir,"r164-original-"+Guid.NewGuid().ToString("N"));
        var nested=Path.Combine(source,"nested");
        var empty=Path.Combine(source,"empty");
        var cancelled=Path.Combine(outdir,"r164-cancel-"+Guid.NewGuid().ToString("N"));
        var completed=Path.Combine(outdir,"r164-success-"+Guid.NewGuid().ToString("N"));
        var existing=Path.Combine(outdir,"r164-existing-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(empty);
        File.WriteAllBytes(Path.Combine(nested,"large.dat"),new byte[4*1024*1024]);
        File.WriteAllText(Path.Combine(source,"small.txt"),"preserve the original");
        try
        {
            using(var cts=new System.Threading.CancellationTokenSource())
            {
                var sawBytes=false;
                var progress=new Action<long,long,int,int>((written,total,files,filesTotal)=>{
                    if(written>0){sawBytes=true;cts.Cancel();}
                });
                try
                {
                    copy.Invoke(null,new object?[]{source,cancelled,cts.Token,progress});
                    throw new Exception("R164 did not stop cancellation during file copy");
                }
                catch(System.Reflection.TargetInvocationException e)
                    when(e.InnerException is OperationCanceledException){}
                Assert(sawBytes && !Directory.Exists(cancelled) &&
                    !Directory.Exists(cancelled+".partial"),
                    "R164 cancelled copy must delete only incomplete Deck staging");
                Assert(File.ReadAllText(Path.Combine(source,"small.txt"))==
                    "preserve the original",
                    "R164 cancel must never modify original folder files");
            }
            long finalBytes=0;
            var finished=new Action<long,long,int,int>((b,total,done,count)=>{
                finalBytes=b;
            });
            copy.Invoke(null,new object?[]{
                source,completed,System.Threading.CancellationToken.None,finished});
            Assert(Directory.Exists(Path.Combine(completed,"empty"))&&
                File.ReadAllText(Path.Combine(completed,"small.txt"))==
                    "preserve the original"&&
                new FileInfo(Path.Combine(completed,"nested","large.dat")).Length==
                    4L*1024*1024 && finalBytes>=4L*1024*1024,
                "R164 copy must stage intact nested content, empty folders and progress");
            File.WriteAllText(existing,"do not overwrite");
            try
            {
                copy.Invoke(null,new object?[]{
                    Path.Combine(source,"small.txt"),existing,
                    System.Threading.CancellationToken.None,null});
                throw new Exception("R164 overwrote an existing staged item");
            }
            catch(System.Reflection.TargetInvocationException e)
                when(e.InnerException is IOException){}
            Assert(File.ReadAllText(existing)=="do not overwrite",
                "R164 copy must never overwrite another item");
            Console.WriteLine("R164 PASS: cancellable streaming copy, atomic rename, "+
                "partial cleanup, progress, empty folders, protected originals.");
        }
        finally
        {
            if(Directory.Exists(source))Directory.Delete(source,true);
            if(Directory.Exists(completed))Directory.Delete(completed,true);
            if(Directory.Exists(cancelled))Directory.Delete(cancelled,true);
            if(Directory.Exists(cancelled+".partial"))
                Directory.Delete(cancelled+".partial",true);
            if(File.Exists(existing))File.Delete(existing);
        }
    }

'@
Swap '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'R164 reflection-based real-file smoke tests'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R164 WPF smoke added: cancel before complete, safe cleanup and successful staged folder.'
