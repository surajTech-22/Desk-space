$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R274 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$next)
}
Swap '                TestExplicitPreviewChooserV273();' @'
                TestExplicitPreviewChooserV273();
                TestClipboardConversionRecoveryV274();
'@ 'native suite'
$method=@'
    private static void TestClipboardConversionRecoveryV274()
    {
        var tool=Product.GetType(
            "DeskSpace.DeskSpaceClipboardTransformsV177",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var transform=tool.GetMethod("Transform",f)!;
        string Run(string input,string operation)=>
            (string)transform.Invoke(null,new object[]{input,operation})!;
        Assert(Run("{\"b\":1, \"a\": 2}","Minify JSON")==
               "{\"b\":1,\"a\":2}",
            "R274 JSON minify must preserve values and property ordering");
        Assert(Run("{\"b\":1}","Format JSON").Contains(Environment.NewLine),
            "R274 Format JSON should still produce readable indentation");
        Assert(Run("Zebra\r\nalpha\r\nBeta","Sort lines")==
               string.Join(Environment.NewLine,new[]{"alpha","Beta","Zebra"}),
            "R274 deterministic case-insensitive Sort lines");
        Assert(Run(" a \r\n  b  ","Trim lines")==
               "a"+Environment.NewLine+"b",
            "R274 trim lines without collapsing line boundaries");
        Assert(Run("test\ntest\nother","Unique lines")==
               "test"+Environment.NewLine+"other",
            "R274 pre-existing Unique lines must keep working");
        bool jsonRejected=false,csvRejected=false;
        try{Run("{\"broken\":","Format JSON");}
        catch(TargetInvocationException ex) when(ex.InnerException is
            System.Text.Json.JsonException){jsonRejected=true;}
        try{Run("name,amount\n\"unclosed","CSV to Markdown table");}
        catch(TargetInvocationException ex) when(ex.InnerException is
            FormatException){csvRejected=true;}
        Assert(jsonRejected&&csvRejected,
            "R274 malformed JSON/CSV must be validation errors, not fatal WPF errors");
        var window=(Window)tool.GetMethod("Create",f)!.Invoke(null,
            new object[]{new Window()})!;
        try
        {
            // Unshown WPF windows have no materialized visual tree.
            // Test real logical controls already built inside the content.
            var root=(StackPanel)((ScrollViewer)window.Content).Content;
            var labels=root.Children.OfType<StackPanel>()
                .SelectMany(group=>group.Children.OfType<Button>())
                .Select(button=>button.Content?.ToString()??"").ToArray();
            Assert(labels.Contains("Read clipboard (on demand)")&&
                labels.Contains("Clear local preview")&&
                labels.Contains("Copy transformed text")&&
                labels.Contains("Save As..."),
                "R274 actual WPF text tool must retain explicit consent controls");
        }
        finally{window.Close();}
        Console.WriteLine("R274 PASS: JSON minify, sort/trim lines, "+
            "invalid JSON/CSV rejection and explicit WPF clipboard controls.");
    }

'@
Swap '    private static void TestExplicitPreviewChooserV273()' ($method+
'    private static void TestExplicitPreviewChooserV273()') 'method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R274 WPF native clipboard conversion and invalid-data safety tests registered.'
