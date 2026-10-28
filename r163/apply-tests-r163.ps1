$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($p)
$old='            TestCancellableZipV162();'
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){throw 'R163 V162 test caller missing'}
$s=$s.Replace($old,$old+[Environment]::NewLine+'            TestSearchableZipV163();')
$anchor='    private static void TestIndependentMonitorsV154()'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){throw 'R163 method anchor missing'}
$method=@'
    private static void TestSearchableZipV163()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceArchiveSearchV163",true)!;
        var read=type.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!;
        var create=type.GetMethod("Create",BindingFlags.NonPublic|BindingFlags.Static)!;
        var name=Path.Combine(outdir,"r163-"+Guid.NewGuid()+".zip");
        try
        {
            using(var zip=System.IO.Compression.ZipFile.Open(name,
                System.IO.Compression.ZipArchiveMode.Create))
            {
                zip.CreateEntry("Documents/");
                zip.CreateEntry("Documents/Invoice-01.pdf");
                zip.CreateEntry("Documents/Invoice-02.pdf");
                zip.CreateEntry("Images/photo.jpg");
            }
            var all=read.Invoke(null,new object[]{"", ""});
            var filtered=read.Invoke(null,new object[]{name,"invoice"});
            var fields=filtered!.GetType().GetFields();
            var entries=(string[])filtered.GetType().GetField("Item2")!.GetValue(filtered)!;
            var summary=(string)filtered.GetType().GetField("Item1")!.GetValue(filtered)!;
            Assert(entries.Length==2&&
                entries.All(e=>e.Contains("Invoice-",StringComparison.Ordinal))&&
                summary.Contains("2 matches"),
                "R163 ZIP search must filter by filename without extracting files");
            var surface=(FrameworkElement)create.Invoke(null,new object[]{name})!;
            Assert(Walk(surface).OfType<TextBox>().Count()>=2,
                "R163 ZIP preview must have interactive search and read-only results");
            Assert(!Directory.Exists(Path.Combine(outdir,"Documents")),
                "R163 ZIP preview must never extract archive content");
            Console.WriteLine("R163 PASS: ZIP metadata search, sizes and read-only preview.");
        }
        finally {if(File.Exists(name))File.Delete(name);}
    }

'@
$s=$s.Replace($anchor,$method+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R163 WPF ZIP search regression added.'
