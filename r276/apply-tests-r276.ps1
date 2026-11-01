$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R276 '+$why+' anchor='+$count)}
 $script:s=$script:s.Replace($old,$next)
}
Swap '                TestSafeBatchRenameApplyUndoV275();' @'
                TestSafeBatchRenameApplyUndoV275();
                TestEarlyWidgetStartupRecoveryV276();
'@ 'register startup WPF test'
$method=@'
    private static void TestEarlyWidgetStartupRecoveryV276()
    {
        var recovery=Product.GetType(
            "DeskSpace.DeskSpaceStartupRecoveryV183",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        Assert(recovery.GetMethod("BeginEarly",flags)!=null &&
               recovery.GetMethod("MarkCleanExit",flags)!=null,
            "R276 startup marker and clean-exit recovery must be callable before Loaded");
        var fallback=Product.GetType(
            "DeskSpace.DeskSpaceStartupFallbackV276",true)!;
        var create=fallback.GetMethod("Create",flags)!;
        var window=(Window)create.Invoke(null,new object[]{"SimulatedException"})!;
        try
        {
            Assert(window.Title.Contains("Widget recovery") &&
                   window.ShowInTaskbar,
                "R276 early failure must show an independent visible recovery window");
            var layout=(StackPanel)((ScrollViewer)window.Content).Content;
            var message=layout.Children.OfType<TextBlock>()
                .First(x=>x.Text.Contains("could not start"));
            Assert(message.Text.Contains("SimulatedException") &&
                   message.Text.Contains("have not been changed"),
                "R276 startup fallback should show safe error type and preserve data");
            var buttons=layout.Children.OfType<WrapPanel>()
                .Single().Children.OfType<Button>()
                .Select(b=>b.Content?.ToString()??"").ToArray();
            Assert(buttons.Contains("Open DeskSpace Workspace") &&
                   buttons.Contains("Close recovery window"),
                "R276 must let user enter normal Workspace or exit manually");
        }
        finally{window.Close();}
        Console.WriteLine("R276 PASS: pre-Loaded primary startup marker, "+
            "safe independent recovery window, user-initiated Workspace and close.");
    }

'@
Swap '    private static void TestSafeBatchRenameApplyUndoV275()' ($method+
 '    private static void TestSafeBatchRenameApplyUndoV275()') 'test implementation'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R276: native WPF no-user-data startup fallback UI checks registered.'
