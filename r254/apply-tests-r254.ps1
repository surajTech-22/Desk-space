$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestResourceBudgetV253();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R254 invocation anchor missing'
}
$s=$s.Replace($a,$a+$lf+'                TestBatchRenamePreviewV254();')
$anchor='    private static void TestResourceBudgetV253()'
$method=@'
    private static void TestBatchRenamePreviewV254()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceBatchRenamePreviewV254",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var prepare=type.GetMethod("Prepare",flags)!;
        var inspect=type.GetMethod("InspectFolder",flags)!;
        (bool Eligible,int Count,int Blocked) Review(string[] files,
            string prefix,string suffix)
        {
            var plan=prepare.Invoke(null,new object[]{files,prefix,suffix})!;
            var t=plan.GetType();
            var entries=((System.Collections.IEnumerable)
                t.GetProperty("Files")!.GetValue(plan)!).Cast<object>().ToArray();
            return (
                (bool)t.GetProperty("Eligible")!.GetValue(plan)!,
                entries.Length,
                entries.Count(x=>(bool)x.GetType()
                    .GetProperty("Conflict")!.GetValue(x)!)
            );
        }
        Assert(Review(new[]{"a.txt","b.csv"},"2026-","").Eligible &&
               Review(new[]{"a.txt"},"","-final").Eligible,
            "R254 safe filename prefix/suffix preview should pass");
        Assert(!Review(new[]{"a.txt","pre-a.txt"},"pre-","").Eligible &&
               Review(new[]{"a.txt","pre-a.txt"},"pre-","").Blocked>0,
            "R254 preview must detect names occupied by a different source file");
        Assert(!Review(new[]{"a.txt"},"","").Eligible &&
               !Review(new[]{"a.txt"},"bad:","").Eligible &&
               !Review(new[]{"a.txt"},"bad/","").Eligible &&
               !Review(new[]{"a.txt","A.TXT"},"p-","").Eligible &&
               !Review(Array.Empty<string>(),"x-","").Eligible,
            "R254 invalid, empty, duplicate-case and no-op previews blocked");
        var root=Path.Combine(outdir,"r254-rename-preview");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"one.txt"),"source unchanged");
            File.WriteAllText(Path.Combine(root,"two.txt"),"other unchanged");
            var hidden=Path.Combine(root,"private.txt");
            File.WriteAllText(hidden,"exclude hidden");
            File.SetAttributes(hidden,FileAttributes.Hidden);
            Directory.CreateDirectory(Path.Combine(root,"nested"));
            File.WriteAllText(Path.Combine(root,"nested","not-listed.txt"),"nested");
            var plan=inspect.Invoke(null,new object[]{root,"Q-",""})!;
            var t=plan.GetType();
            Assert((bool)t.GetProperty("Eligible")!.GetValue(plan)! &&
                   ((System.Collections.IEnumerable)t.GetProperty("Files")!
                     .GetValue(plan)!).Cast<object>().Count()==2,
                "R254 folder preview must exclude hidden/nested and preserve originals");
            Assert(File.ReadAllText(Path.Combine(root,"one.txt"))=="source unchanged" &&
                   File.Exists(Path.Combine(root,"two.txt")) &&
                   !File.Exists(Path.Combine(root,"Q-one.txt")),
                "R254 preview must never move, modify or rename source");
            Console.WriteLine(
                "R254 PASS: read-only batch filename preview, collision/no-op/"+
                "invalid Windows name rejection, hidden/nested exclusion and original file preservation.");
        }
        finally{Directory.Delete(root,true);}
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R254 native method anchor missing'
}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R254: native folder and filename collision tests registered.'
