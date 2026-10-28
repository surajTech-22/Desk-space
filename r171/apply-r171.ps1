$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R171 $($label): expected one anchor, found $n"}
 $script:s=$script:s.Replace($old,$new)
}
One '        outdir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"deskspace-r36-ui");' @'
        if(args.Length>0&&string.Equals(args[0],"--r160-only",
            StringComparison.OrdinalIgnoreCase))
        {
            outdir=Path.Combine(Path.GetTempPath(),"deskspace-r160-only-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outdir);
            var isolated=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            try
            {
                Console.WriteLine("R171 diagnostic: isolated R160 beginning");
                TestFolderDropZipOrCopyV159();
                Console.WriteLine("R171 diagnostic: isolated R160 passed");
                isolated.Shutdown();
                return 0;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("R171 diagnostic failure: "+ex);
                isolated.Shutdown();
                return 1;
            }
        }
        outdir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"deskspace-r36-ui");
'@ 'isolated smoke entry'
One '            commit.Invoke(a,new object[]{false});' @'
            Console.WriteLine("R171 stage A: before Copy action");
            commit.Invoke(a,new object[]{false});
            Console.WriteLine("R171 stage B: Copy action returned");
'@ 'copy invocation checkpoints'
One @'
            Console.WriteLine("R171 stage B: Copy action returned");
            PumpUntil(()=>HeldPaths().Length==1);
'@ @'
            Console.WriteLine("R171 stage B: Copy action returned");
            Console.WriteLine("R171 stage C: before Copy-held wait");
            PumpUntil(()=>HeldPaths().Length==1);
            Console.WriteLine("R171 stage D: Copy-held wait returned");
'@ 'copy wait checkpoints'
One '            commit.Invoke(a,new object[]{true});' @'
            Console.WriteLine("R171 stage E: before ZIP action");
            commit.Invoke(a,new object[]{true});
            Console.WriteLine("R171 stage F: ZIP action returned");
'@ 'ZIP invocation checkpoints'
One '            var staged=HeldPaths().Single();' @'
            Console.WriteLine("R171 stage G: ZIP-held wait returned");
            var staged=HeldPaths().Single();
'@ 'ZIP wait checkpoints'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R171: isolated R160 real WPF Copy/ZIP test and four decisive operation checkpoints.'
