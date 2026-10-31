$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestRunningAppsOrderV261();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1)
{throw 'R262 native invocation missing'}
$s=$s.Replace($a,$a+$lf+'                TestPinnedRecentDocumentChoicesV262();')
$anchor='    private static void TestRunningAppsOrderV261()'
$method=@'
    private static void TestPinnedRecentDocumentChoicesV262()
    {
        var chooser=Product.GetType("DeskSpace.DeskSpacePinnedDocumentChoiceV262",true)!;
        var picker=Product.GetType("DeskSpace.DeskSpacePinnedRecentPickerV262",true)!;
        var recent=Product.GetType("DeskSpace.DeskSpacePinnedRecentV122",true)!;
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var cand=chooser.GetNestedType("Candidate",BindingFlags.NonPublic)!;
        var filter=chooser.GetMethod("Filter",flags)!;
        var safe=chooser.GetMethod("SafeDocument",flags)!;
        System.Collections.IList NewList()=> (System.Collections.IList)
            Activator.CreateInstance(typeof(List<>).MakeGenericType(cand))!;
        void Add(System.Collections.IList items,string path,DateTime when,string exe)
            =>items.Add(Activator.CreateInstance(cand,
                new object[]{path,when,exe})!);
        object[] Select(System.Collections.IList items)=>
            ((System.Collections.IEnumerable)filter.Invoke(null,
                new object[]{"excel.exe",items})!).Cast<object>().ToArray();
        var root=Path.GetFullPath(Path.Combine(outdir,"r262-pinned-recent"));
        Directory.CreateDirectory(root);
        try
        {
            var excel=Path.Combine(root,"sales.xlsx");
            var csv=Path.Combine(root,"clients.csv");
            var blocked=Path.Combine(root,"danger.bat");
            var other=Path.Combine(root,"unrelated.pdf");
            File.WriteAllText(excel,"synthetic test workbook");
            File.WriteAllText(csv,"synthetic,nonprivate");
            File.WriteAllText(blocked,"@echo off");
            File.WriteAllText(other,"synthetic PDF test only");
            var docs=NewList();
            Add(docs,excel,DateTime.UtcNow.AddMinutes(-2),"excel.exe");
            Add(docs,excel,DateTime.UtcNow.AddMinutes(-1),"excel.exe");
            Add(docs,csv,DateTime.UtcNow.AddMinutes(-5),"excel.exe");
            Add(docs,blocked,DateTime.UtcNow,"excel.exe");
            Add(docs,other,DateTime.UtcNow,"AcroRd32.exe");
            var selected=Select(docs);
            Assert(selected.Length==2 &&
                   (string)selected[0].GetType()
                     .GetProperty("Path")!.GetValue(selected[0])! == excel,
                "R262 only matched safe document extension/handler, latest-first duplicate collapsed");
            Assert(!(bool)safe.Invoke(null,new object?[]{blocked})! &&
                   !(bool)safe.Invoke(null,new object?[]{root+"/missing.xlsx"})! &&
                   (bool)safe.Invoke(null,new object?[]{csv})!,
                "R262 block scripts and missing docs, allow real existing documents");
            var bulk=NewList();
            for(int i=0;i<14;i++)
            {
                var path=Path.Combine(root,"invoice"+i+".xlsx");
                File.WriteAllText(path,"synthetic workbook");
                Add(bulk,path,DateTime.UtcNow.AddSeconds(-i),"excel.exe");
            }
            Assert(Select(bulk).Length==10,
                "R262 recent file chooser reads no more than 10 matching results");
            Assert(recent.GetMethod("CandidatesV262",flags)!=null &&
                   recent.GetMethod("CanLaunchRecentV262",flags)!=null,
                "R262 existing pinned-app owner must provide on-demand data and handler recheck");
            var owner=new Window{Width=140,Height=140,ShowInTaskbar=false};
            Window? dialog=null;
            try
            {
                owner.Show();
                dialog=(Window)picker.GetMethod("Create",flags)!
                    .Invoke(null,new object[]{owner,
                        Path.Combine(root,"missing-pinned.exe")})!;
                dialog.Show();dialog.UpdateLayout();
                var buttons=Walk(dialog).OfType<Button>()
                    .Select(x=>x.Content?.ToString()).ToArray();
                Assert(buttons.Contains("Open pinned app only") &&
                       buttons.Contains("Close") &&
                       !buttons.Any(x=>x?.Contains("danger.bat")==true),
                    "R262 explicit WPF chooser exposes only manual app/close actions, no script");
            }
            finally{dialog?.Close();owner.Close();}
            Assert(File.ReadAllText(excel)=="synthetic test workbook",
                "R262 read-only chooser never changes source document contents");
            Console.WriteLine("R262 PASS: on-demand pinned recent matching, "+
                "max 10 deduped documents, script exclusion, latest-first and explicit WPF controls.");
        }
        finally{Directory.Delete(root,true);}
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1)
{throw 'R262 native method anchor missing'}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R262: native Windows pinned app recent document safety and WPF picker tests registered.'
