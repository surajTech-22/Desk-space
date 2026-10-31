$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                TestResourceSnapshotV205();'
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R206 isolated native test call anchor missing'
}
$s=$s.Replace($old,$old+$lf+'                TestCancellableFolderZipV206();')
$old='    private static void TestResourceSnapshotV205()'
$method=@'
    private static void TestCancellableFolderZipV206()
    {
        var zip=Product.GetType("DeskSpace.DeskSpaceFolderZipV206",true)!;
        var method=zip.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic)!;
        var ui=Product.GetType("DeskSpace.DeskSpaceZipProgressWindowV206",true)!;
        Assert(ui.GetMethod("Show",BindingFlags.Static|
            BindingFlags.NonPublic)!=null,
            "R206 a cancellable user-visible ZIP progress window must exist");
        string temp=Path.Combine(Path.GetTempPath(),
            "deskspace-r206-"+Guid.NewGuid().ToString("N"));
        string input=Path.Combine(temp,"original");
        string empty=Path.Combine(input,"empty");
        string output=Path.Combine(temp,"finished.zip");
        Directory.CreateDirectory(empty);
        string original=Path.Combine(input,"accounting.txt");
        File.WriteAllText(original,"Synthetic test only");
        try
        {
            void Create(string destination,System.Threading.CancellationToken token)
            {
                try
                {
                    method.Invoke(null,new object?[]{input,destination,null,token});
                }
                catch(TargetInvocationException ex)when(ex.InnerException!=null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                        ex.InnerException).Throw();
                    throw;
                }
            }
            using(var cancelled=new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();
                bool rejected=false;
                try{Create(output,cancelled.Token);}
                catch(OperationCanceledException){rejected=true;}
                Assert(rejected&&!File.Exists(output)&&!File.Exists(output+".partial"),
                    "R206 cancelled ZIP may not create a finished or partial file");
            }
            Create(output,System.Threading.CancellationToken.None);
            using(var archive=System.IO.Compression.ZipFile.OpenRead(output))
            {
                Assert(archive.Entries.Any(x=>x.FullName.EndsWith(
                    "original/accounting.txt",StringComparison.Ordinal)) &&
                    archive.Entries.Any(x=>x.FullName.EndsWith(
                    "original/empty/",StringComparison.Ordinal)),
                    "R206 ZIP must retain source root, empty directories and data");
            }
            Assert(File.ReadAllText(original)=="Synthetic test only",
                "R206 original folder must remain read-only");
            bool existingRefused=false;
            try{Create(output,System.Threading.CancellationToken.None);}
            catch(IOException){existingRefused=true;}
            Assert(existingRefused&&File.Exists(output),
                "R206 must refuse to overwrite an existing ZIP");
            bool nestedRefused=false;
            try{Create(Path.Combine(input,"unsafe.zip"),
                System.Threading.CancellationToken.None);}
            catch(InvalidDataException){nestedRefused=true;}
            Assert(nestedRefused&&!File.Exists(Path.Combine(input,"unsafe.zip")),
                "R206 must refuse output that nests inside input folder");
        }
        finally
        {
            if(Directory.Exists(temp))Directory.Delete(temp,true);
        }
        Console.WriteLine("R206 PASS: cancellable bounded ZIP, empty folders, no overwrites and source preserved.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R206 native test method anchor missing'
}
$s=$s.Replace($old,$method.Replace($crlf,$lf)+$old)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R206 native cancellation, source protection, ZIP layout and no-overwrite regression added.'
