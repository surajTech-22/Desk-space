$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$purpose)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R177 WPF test $($purpose) expected 1 anchor, found $n"}
 $script:s=$script:s.Replace($old,$new)
}
One '            TestComponentHealthV175();' @'
            TestComponentHealthV175();
            TestClipboardTransformV177();
'@ 'run transform UI and pure conversion acceptance'
$method=@'
    private static void TestClipboardTransformV177()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceClipboardTransformsV177",true)!;
        var transform=type.GetMethod("Transform",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var create=type.GetMethod("Create",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        string Change(string source,string mode)=>(string)transform.Invoke(null,
            new object[]{source,mode})!;
        Assert(Change("Invoice A","Plain text")=="Invoice A",
            "R177 plain text must not change source content");
        Assert(Change("Amount [x]*","Markdown literal")==
            @"Amount \[x\]\*",
            "R177 Markdown escape must prevent embedded markup injection");
        Assert(Change("A\nB","JSON string")=="\"A\\nB\"",
            "R177 JSON string must encode newline safely");
        using(var parsed=System.Text.Json.JsonDocument.Parse(
            Change("invoice","JSON object")))
            Assert(parsed.RootElement.GetProperty("text").GetString()=="invoice",
                "R177 JSON object must retain source exactly");
        Assert(Change("https://example.org/docs","Markdown link").StartsWith(
            "[example.org](https://example.org/docs"),
            "R177 safe HTTP(S) URLs become Markdown links");
        try
        {
            Change("file:///C:/secret.txt","Markdown link");
            throw new Exception("R177 file URI accepted unexpectedly");
        }
        catch(TargetInvocationException e) when(e.InnerException is ArgumentException){}
        var owner=new Window{Width=116,Height=116};
        Window? panel=null;
        try
        {
            owner.Show();
            panel=(Window)create.Invoke(null,new object[]{owner})!;
            panel.Show();
            panel.UpdateLayout();
            var buttons=Walk(panel).OfType<Button>().ToArray();
            Assert(buttons.Any(b=>b.Content?.ToString()?
                .Contains("Read clipboard")==true),
                "R177 user must explicitly initiate clipboard read");
            Assert(buttons.Any(b=>b.Content?.ToString()=="Save As..."),
                "R177 file writes require explicit Save As action");
            Assert(buttons.Single(b=>b.Content?.ToString()=="Save As...").
                IsEnabled==false,
                "R177 Save As must be disabled before user reads text");
            var preview=Walk(panel).OfType<TextBox>().ToArray();
            Assert(preview.Length>=2&&preview.All(b=>b.Text.Length==0),
                "R177 clipboard content must not be read on window open");
            Console.WriteLine("R177 PASS: plain, Markdown, JSON, URL safety "+
                "and real WPF opt-in clipboard conversion UI.");
        }
        finally
        {
            panel?.Close();owner.Close();
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
 $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'insert native WPF opt-in clipboard conversion'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R177: compiled pure transformations and WPF no-automatic-read acceptance.'
