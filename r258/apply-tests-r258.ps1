$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R258 "+$why+": expected 1 anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestWindowRestorePreviewV257();' @'
                TestWindowRestorePreviewV257();
                TestContextPrivacyAuditV258();
'@ 'register on-demand privacy regression'
$method=@'
    private static void TestContextPrivacyAuditV258()
    {
        var audit=Product.GetType("DeskSpace.DeskSpaceContextAuditV258",true)!;
        var summary=audit.GetMethod("Summary",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Explain(bool active,int n,string category)=>
            (string)summary.Invoke(null,new object[]{active,n,category})!;
        var off=Explain(false,99,"Browser");
        Assert(off.Contains("Off.") && !off.Contains("Browser") &&
               !off.Contains("99"),
            "R258 stopping listener hides all previous app details and counts");
        Assert(Explain(true,12,"Excel").Contains("12") &&
               Explain(true,12,"Excel").Contains("Excel") &&
               !Explain(true,100000,"Browser").Contains("100000"),
            "R258 session audit shows only capped recognized category count");
        Assert(!Explain(true,4,@"C:\Private\Customer.xlsx").Contains("Customer") &&
               Explain(true,4,"Unknown").Contains("None"),
            "R258 app names, document paths and unapproved categories cannot enter audit");
        var app=Product.GetType("DeskSpace.DeskSpaceContextSuggestionsV237",true)!;
        var sessionType=app.GetNestedType("Session",BindingFlags.NonPublic)!;
        var owner=new Window{Width=160,Height=160};
        var dialog=new Window{Width=200,Height=200};
        var status=new TextBlock();
        var suggestions=new StackPanel();
        var auditView=new TextBlock();
        var session=Activator.CreateInstance(sessionType,
            BindingFlags.Instance|BindingFlags.NonPublic,
            null,new object[]{dialog,owner,status,suggestions,auditView},null)!;
        try
        {
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            sessionType.GetField("category",flags)!.SetValue(session,"Browser");
            sessionType.GetField("recognizedV258",flags)!.SetValue(session,7);
            suggestions.Children.Add(new Button{Content="Old recommendation"});
            sessionType.GetMethod("Stop",flags)!.Invoke(session,null);
            Assert(suggestions.Children.Count==0 &&
                   auditView.Text.Contains("Off.") &&
                   !auditView.Text.Contains("Browser") &&
                   !auditView.Text.Contains("7") &&
                   status.Text.Contains("Stopped"),
                "R258 Stop unsubscribes and clears all retained session suggestions/category/counters");
            Console.WriteLine("R258 PASS: explicit audit excludes raw paths, "+
                "bounded category counts, session Stop clears private counters and recommendations.");
        }
        finally
        {
            ((IDisposable)session).Dispose();
            dialog.Close();owner.Close();
        }
    }

'@
Swap '    private static void TestWindowRestorePreviewV257()' (
 $method.Replace($crlf,$lf)+'    private static void TestWindowRestorePreviewV257()'
) 'insert native session privacy and teardown test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R258: Windows native session privacy and Stop/clear behavior tests registered.'
