$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$what){
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ('R288 '+$what+' anchor='+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestTrayExitUntilExplicitShowV286();' @'
                TestTrayExitUntilExplicitShowV286();
                TestFileFirstSearchV288();
'@ 'native test invocation'
$method=@'
    private static void TestFileFirstSearchV288()
    {
        var index=Product.GetType("DeskSpace.DeskSpaceSearchIndexV13",true)!;
        var ranking=Product.GetType("DeskSpace.DeskSpaceSearchRankingV192",true)!;
        var collector=ranking.GetNestedType("Collector",BindingFlags.NonPublic)!;
        const BindingFlags f=BindingFlags.NonPublic|BindingFlags.Static;
        var file=index.GetMethod("IsSearchableFileV288",f)!;
        var quick=index.GetMethod("QuickFilesV288",f)!;
        bool Eligible(string path)=>(bool)file.Invoke(null,new object[]{path})!;
        var root=Path.GetFullPath(Path.Combine(outdir,
            "r288-file-search-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var hidden=Path.Combine(root,"Invoice hidden.txt");
        try
        {
            var directory=Path.Combine(root,"Invoice archive");
            Directory.CreateDirectory(directory);
            var doc=Path.Combine(root,"Invoice 2026.xlsx");
            var pdf=Path.Combine(directory,"Invoice scan.pdf");
            File.WriteAllText(doc,"spreadsheet");
            File.WriteAllText(pdf,"pdf fixture");
            File.WriteAllText(hidden,"private fixture");
            File.SetAttributes(hidden,FileAttributes.Hidden);
            Assert(Eligible(doc) && Eligible(pdf) && !Eligible(hidden) &&
                   !Eligible(directory),
                "R288 normal existing files must be eligible for search");
            var matches=((System.Collections.IEnumerable)
                quick.Invoke(null,new object[]{
                    root,"INVOICE",System.Threading.CancellationToken.None,2200,3})!)
                    .Cast<SearchHit>().ToArray();
            Assert(matches.Length==2 &&
                   matches.All(x=>x.Category=="Files" &&
                       File.Exists(x.OpenTarget)) &&
                   matches.Any(x=>x.OpenTarget==doc) &&
                   matches.Any(x=>x.OpenTarget==pdf),
                "R288 real filesystem quick search must return both "+
                "case-insensitive XLSX and PDF files, not folders");
            var limited=((System.Collections.IEnumerable)
                quick.Invoke(null,new object[]{
                    root,"invoice",System.Threading.CancellationToken.None,1,3})!)
                    .Cast<SearchHit>().ToArray();
            Assert(limited.Length<=1,
                "R288 quick search must honor its entry budget");
            using(var cancel=new System.Threading.CancellationTokenSource())
            {
                cancel.Cancel();
                bool refused=false;
                try{quick.Invoke(null,new object[]{
                        root,"invoice",cancel.Token,2200,3});}
                catch(TargetInvocationException ex)
                    when(ex.InnerException is OperationCanceledException)
                {refused=true;}
                Assert(refused,"R288 cancelled searches cannot traverse files");
            }
            object instance=Activator.CreateInstance(collector,
                BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                null,new object[]{"invoice"},null)!;
            var add=collector.GetMethod("Add",BindingFlags.Instance|
                BindingFlags.NonPublic)!;
            var finish=collector.GetMethod("Finish",BindingFlags.Instance|
                BindingFlags.NonPublic)!;
            add.Invoke(instance,new object[]{new SearchHit{
                Category="Files",Name="Invoice archive",
                OpenTarget=directory,Location=directory}});
            add.Invoke(instance,new object[]{new SearchHit{
                Category="Files",Name="Invoice 2026.xlsx",
                OpenTarget=doc,Location=doc}});
            var sorted=((System.Collections.IEnumerable)finish.Invoke(
                instance,new object[]{160})!).Cast<SearchHit>().ToArray();
            Assert(sorted.Length==2 && sorted[0].OpenTarget==doc,
                "R288 a matching real file must rank ahead of directories");
            Console.WriteLine("R288 PASS: existing XLSX/PDF file search, "+
                "hidden exclusion, cancellation, bounded scanning and file-first result.");
        }
        finally
        {
            try
            {
                if(File.Exists(hidden))
                    File.SetAttributes(hidden,FileAttributes.Normal);
                Directory.Delete(root,true);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
Swap '    private static void TestTrayExitUntilExplicitShowV286()' ($method+
 '    private static void TestTrayExitUntilExplicitShowV286()') 'native file-search method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R288 native XLSX/PDF file-search regression registered.'
