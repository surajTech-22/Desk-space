$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R193 "+$why+" expected one anchor; got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Once '            TestRankedSearchResultsV192();' @'
            TestRankedSearchResultsV192();
            TestSearchSmartActionsV193();
'@ 'invoke native WPF keyboard and archive regression'
$test=@'
    private static void TestSearchSmartActionsV193()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSearchActionsV193",true)!;
        var nav=type.GetMethod("NextIndex",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        int Move(int count,int current,int delta)=>(int)nav.Invoke(null,
            new object[]{count,current,delta})!;
        Assert(Move(0,0,1)==-1 && Move(5,0,-1)==0 &&
            Move(5,0,1)==1 && Move(5,3,7)==4 &&
            Move(5,4,-7)==0,
            "R193 search keyboard must clamp safely to visible results");
        var can=type.GetMethod("CanZip",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var zip=type.GetMethod("ZipSingleFile",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "DeskSpace-R193-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            var input=System.IO.Path.Combine(folder,"sample.txt");
            var output=System.IO.Path.Combine(folder,"sample.txt.zip");
            System.IO.File.WriteAllText(input,"DeskSpace Search ZIP acceptance");
            Assert((bool)can.Invoke(null,new object?[]{input})! &&
                !(bool)can.Invoke(null,new object?[]{folder})!,
                "R193 single ordinary file allowed; folders never zip without a choice");
            zip.Invoke(null,new object[]{input,output,
                System.Threading.CancellationToken.None});
            using(var archive=System.IO.Compression.ZipFile.OpenRead(output))
            {
                Assert(archive.Entries.Count==1 &&
                    archive.Entries[0].FullName=="sample.txt",
                    "R193 safe ZIP must contain exactly the chosen file");
                using var reader=new System.IO.StreamReader(archive.Entries[0].Open());
                Assert(reader.ReadToEnd()=="DeskSpace Search ZIP acceptance",
                    "R193 ZIP must preserve original bytes");
            }
            Assert(System.IO.File.ReadAllText(input)=="DeskSpace Search ZIP acceptance",
                "R193 never modifies original source");
            bool refusedOverwrite=false;
            try{zip.Invoke(null,new object[]{input,output,
                System.Threading.CancellationToken.None});}
            catch(TargetInvocationException ex) when(ex.InnerException is System.IO.IOException)
            {refusedOverwrite=true;}
            Assert(refusedOverwrite,
                "R193 Search ZIP must never overwrite an existing destination");
        }
        finally { System.IO.Directory.Delete(folder,true); }
        Console.WriteLine("R193 PASS: keyboard boundaries, explicit file ZIP, no overwrite, no source mutation.");
    }

'@
Once '    private static void TestRankedSearchResultsV192()' (
 $test.Replace($crlf,$lf)+'    private static void TestRankedSearchResultsV192()'
) 'insert new native UI regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R193 native WPF regression: navigate results and write isolated ZIP without overwrites.'
