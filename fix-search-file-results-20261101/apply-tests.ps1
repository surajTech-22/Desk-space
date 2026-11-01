$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "Search-file test ${why}: anchor count $n"}
 $script:s=$script:s.Replace($a,$b)
}
Once '                TestSearchDeckLayoutV225();' @'
                TestSearchDeckLayoutV225();
                TestActualFileSearch20261101();
'@ 'register native test'
$method=@'
    private static void TestActualFileSearch20261101()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceFileQuickSearchV233",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var search=type.GetMethod("Find",flags)!;
        var indexedMatch=type.GetMethod("MatchesIndexedEntry",flags)!;
        var root=Path.Combine(Path.GetTempPath(),
            "deskspace-real-file-search-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file=Path.Combine(root,"invoice_test_2026.xlsx");
            File.WriteAllText(file,"dummy");
            var folder=Path.Combine(root,"invoice-related");
            Directory.CreateDirectory(folder);
            var unrelated=Path.Combine(folder,"not-related.pdf");
            File.WriteAllText(unrelated,"dummy");
            var found=((System.Collections.IEnumerable)search.Invoke(
                null,new object?[]{"invoice",
                    System.Threading.CancellationToken.None,null,new[]{root}})!)
                .Cast<SearchHit>().ToList();
            Assert(found.Any(x=>x.OpenTarget==file),
                "Filename search must return a real XLSX file, not only folders");
            Assert(!found.Any(x=>x.OpenTarget==unrelated),
                "A matching parent folder must not make an unrelated file match");
            Assert(!(bool)indexedMatch.Invoke(null,new object[]{
                "not-related.pdf",unrelated,"invoice"})!,
                "Fallback must not match parent-directory names as filenames");
            Assert((bool)indexedMatch.Invoke(null,new object[]{
                "invoice_test_2026.xlsx",file,"invoice"})!,
                "Real matching filenames must survive indexed filtering");
        }
        finally {Directory.Delete(root,true);}
        Console.WriteLine(
            "Real-file Search PASS: XLSX found, folder parent false positives excluded.");
    }

'@
Once '    private static void TestSearchDeckLayoutV225()' (
 $method.Replace($crlf,$lf)+'    private static void TestSearchDeckLayoutV225()'
) 'add actual-file regression test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'Real-file Search regression added to shipping Windows/WPF smoke suite.'
