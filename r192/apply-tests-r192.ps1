$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$before,[string]$after,[string]$label)
{
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($n -ne 1){throw ("R192 "+$label+" requires one anchor; found "+$n)}
    $script:s=$script:s.Replace($before,$after)
}
Once '            TestVerifiedUpdateQueueV191();' @'
            TestVerifiedUpdateQueueV191();
            TestRankedSearchResultsV192();
'@ 'native WPF test invocation'
$method=@'
    private static void TestRankedSearchResultsV192()
    {
        var factory=Product.GetType("DeskSpace.DeskSpaceSearchRankingV192",true)!;
        var collectorType=factory.GetNestedType("Collector",BindingFlags.NonPublic)!;
        var collector=Activator.CreateInstance(collectorType,
            BindingFlags.NonPublic|BindingFlags.Instance,null,
            new object[]{"report"},null)!;
        var add=collectorType.GetMethod("Add",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        var finish=collectorType.GetMethod("Finish",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        bool Add(SearchHit hit)=>(bool)add.Invoke(collector,new object[]{hit})!;
        List<SearchHit> Result()=>((System.Collections.IEnumerable)finish.Invoke(
            collector,new object[]{160})!).Cast<SearchHit>().ToList();

        for(int i=0;i<240;i++)
            Add(new SearchHit{Category="Files",Name="report-other-"+i,
                OpenTarget=@"C:\Documents\report-other-"+i+".txt",
                Location="Windows Search or fallback"});
        Add(new SearchHit{Category="Files",Name="report",
            OpenTarget=@"D:\Other\report.txt",Location="D drive"});
        Add(new SearchHit{Category="Applications",Name="Reporting App",
            OpenTarget=@"C:\Apps\ReportApp.exe"});
        Add(new SearchHit{Category="Notes",Name="report",
            OpenTarget="note:192"});
        var first=Result();
        Assert(first.Count<=160 && first.Any(h=>h.OpenTarget==@"D:\Other\report.txt") &&
            first[0].Name=="report",
            "R192 an exact cross-drive match must not be buried by earlier weak matches");
        Assert(first.FindIndex(x=>x.OpenTarget=="note:192") <
               first.FindIndex(x=>x.OpenTarget==@"C:\Apps\ReportApp.exe"),
            "R192 exact note should outrank fuzzy app");
        Assert(!Add(new SearchHit{Category="Files",Name="report",
            OpenTarget=@"d:\OTHER\REPORT.txt",Location="Duplicate Windows Search"}),
            "R192 case insensitive Windows/fallback duplicate must not consume a row");
        var appCollector=Activator.CreateInstance(collectorType,
            BindingFlags.NonPublic|BindingFlags.Instance,null,
            new object[]{"office"},null)!;
        add.Invoke(appCollector,new object[]{
            new SearchHit{Category="Applications",Name="Office",
                OpenTarget=@"C:\Apps\Office.exe"}});
        add.Invoke(appCollector,new object[]{
            new SearchHit{Category="Applications",Name="office",
                OpenTarget=@"C:\Start\Office.lnk"}});
        var all=((System.Collections.IEnumerable)finish.Invoke(
            appCollector,new object[]{160})!).Cast<SearchHit>().ToList();
        Assert(all.Count(x=>x.Category=="Applications" &&
            x.Name.Equals("Office",StringComparison.OrdinalIgnoreCase))==1 &&
            all.Single(x=>x.Category=="Applications" &&
                x.Name.Equals("Office",StringComparison.OrdinalIgnoreCase))
                .OpenTarget.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase),
            "R192 duplicated app paths should prefer the Start menu shortcut");
        Console.WriteLine("R192 PASS: exact-first search, two drives, bounded rows, " +
            "deduped Windows index/drive fallback and app shortcut preference.");
    }

'@
Once '    private static void TestVerifiedUpdateQueueV191()' (
    $method.Replace($crlf,$lf)+'    private static void TestVerifiedUpdateQueueV191()'
) 'native WPF collector tests'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R192 live native search test registered.'
