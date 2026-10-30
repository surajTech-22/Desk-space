$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='    private static void TestManualVaultSuggestionsV202()'
$method=@'
    private static void TestWorkflowDryRunV203()
    {
        var view=Product.GetType("DeskSpace.DeskSpaceWorkflowDryRunV203",true)!;
        var preview=view.GetMethod("Preview",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        var model=new DeskSpaceWorkflowTemplateV84
        {
            Name="Synthetic Review",
            Action="Open URL",
            Target="https://example.com/report?token=secret",
            Condition="None",
            Additional=new List<DeskSpaceActionStep>
            {
                new(){Action="Open URL",Target="https://example.org/next"}
            }
        };
        var result=preview.Invoke(null,new object[]{model})!;
        var type=result.GetType();
        bool Accepted(object value)=>(bool)type.GetProperty("Accepted")!.GetValue(value)!;
        object[] Steps(object value)=>((System.Collections.IEnumerable)
            type.GetProperty("Steps")!.GetValue(value)!).Cast<object>().ToArray();
        Assert(Accepted(result) && Steps(result).Length==2 &&
            string.Join("\n",Steps(result)).Contains("example.com") &&
            !string.Join("\n",Steps(result)).Contains("token=secret"),
            "R203 approved workflow preview must show hosts, not private URL queries");
        model.Action="unapproved powershell";
        Assert(!Accepted(preview.Invoke(null,new object[]{model})!),
            "R203 must reject unsupported workflow actions and never execute");
        Assert(view.GetMethod("Create",BindingFlags.NonPublic|
            BindingFlags.Static)!=null,
            "R203 preview must provide explicit native WPF entry");
        Console.WriteLine("R203 PASS: deterministic, safe, non-executing workflow review.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R203 native smoke method expected once, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$method.Replace($crlf,$lf)+$old),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R203 native smoke inserted for approved, invalid and redacted workflow previews.'
