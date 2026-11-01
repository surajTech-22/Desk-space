$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R269 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestExcelFormulaAndFormatPasteV266();' @'
                TestExcelFormulaAndFormatPasteV266();
                TestUniversalSearchSharedIndexV269();
'@ 'native WPF search invocation'
$method=@'
    private static void TestUniversalSearchSharedIndexV269()
    {
        var policy=Product.GetType(
            "DeskSpace.DeskSpaceUniversalSearchPolicyV269",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var filter=policy.GetMethod("Filter",f)!;
        var accept=policy.GetMethod("Accept",f)!;
        var categories=(string[])policy.GetField("Categories",f)!.GetValue(null)!;
        Assert(categories.Contains("Files") && categories.Contains("Applications") &&
               categories.Contains("Notes") && categories.Contains("Reminders") &&
               categories.Contains("Calendar") && categories.Contains("Information") &&
               !categories.Contains("Commands"),
            "R269 all visible category filters must be backed by actual search results");
        var inputs=new[]{
            new SearchHit{Category="Files",Name="budget.csv",
                OpenTarget=@"D:\Reports\budget.csv"},
            new SearchHit{Category="Files",Name="duplicate",
                OpenTarget=@"d:\reports\BUDGET.csv"},
            new SearchHit{Category="Applications",Name="Editor",
                OpenTarget=@"C:\Apps\Editor.lnk"},
            new SearchHit{Category="Notes",Name="Quick note",
                OpenTarget="note:123"},
            new SearchHit{Category="Reminders",Name="Invoice",
                OpenTarget="deskspace:reminder:345"}
        };
        SearchHit[] Run(string kind,int cap=150)=>
            ((System.Collections.IEnumerable)filter.Invoke(null,
                new object[]{inputs,kind,cap})!).Cast<SearchHit>().ToArray();
        Assert(Run("All").Length==4 && Run("Files").Length==1 &&
               Run("Applications").Length==1 && Run("Notes").Length==1 &&
               Run("Reminders").Length==1 && Run("Files",0).Length==1 &&
               Run("Calendar").Length==0,
            "R269 search category filtering, case-insensitive duplicate and result budget");
        bool Accept(int old,int current,bool cancelled,string term,string typed,
            string selected,string active)=>(bool)accept.Invoke(null,
                new object[]{old,current,cancelled,term,typed,selected,active})!;
        Assert(Accept(1,1,false,"budget","budget","Files","Files") &&
               !Accept(1,2,false,"budget","budget","Files","Files") &&
               !Accept(1,1,true,"budget","budget","Files","Files") &&
               !Accept(1,1,false,"budget","budgetx","Files","Files") &&
               !Accept(1,1,false,"budget","budget","Files","Notes"),
            "R269 a stale or cancelled search must never update UI with unrelated results");
        // Instantiation does not trigger a scan or read personal file content;
        // search begins only when the user explicitly enters a query.
        var window=new UniversalSearchWindow(
            ()=>new Workspace(),WidgetSettingsV3.Load());
        try
        {
            var field=typeof(UniversalSearchWindow).GetField("category",
                BindingFlags.Instance|BindingFlags.NonPublic)!;
            var combo=(ComboBox)field.GetValue(window)!;
            Assert(combo.Items.Count==categories.Length &&
                   combo.Items.Cast<object>().Any(x=>x.ToString()=="Reminders"),
                "R269 real Workspace Search must display all genuinely supported groups");
        }
        finally{window.Close();}
        Console.WriteLine("R269 PASS: real WPF Universal Search, multi-drive hit categories, "+
            "case-insensitive dedupe, bounded results and cancelled/stale query rejection.");
    }

'@
Swap '    private static void TestDeckSaveRecoveryV265()' ($method+
 '    private static void TestDeckSaveRecoveryV265()') 'native source test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R269 native WPF Search category, dedupe and async stale-query regression registered.'
