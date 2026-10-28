$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$before,[string]$after,[string]$name)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R175 WPF $($name) expected 1 anchor; got $n"}
 $script:s=$script:s.Replace($before,$after)
}
One @'
            TestTrayRecoveryV166();
'@ @'
            TestTrayRecoveryV166();
            TestComponentHealthV175();
'@ 'test after native tray recovery'
$method=@'
    private static void TestComponentHealthV175()
    {
        var health=Product.GetType("DeskSpace.DeskSpaceComponentHealthV175",true)!;
        var clipboard=Product.GetType("DeskSpace.DeskSpaceClipboardSignalV175",true)!;
        var watch=clipboard.GetMethod("Watch",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var snapshot=health.GetMethod("Snapshot",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var create=health.GetMethod("Create",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var owner=new Window{Width=116,Height=116,
            ShowInTaskbar=false,Content=new Border()};
        IDisposable? listener=null;
        Window? dashboard=null;
        try
        {
            owner.Show();
            listener=(IDisposable)watch.Invoke(null,new object[]{owner})!;
            var reported=(System.Collections.IEnumerable)snapshot.Invoke(
                null,new object[]{owner,true})!;
            var rows=reported.Cast<object>().ToArray();
            Assert(rows.Length>=9,
                "R175 component dashboard must independently inspect core modules");
            var names=rows.Select(x=>(string)x.GetType().GetProperty("Name")!.
                GetValue(x)!).ToHashSet();
            foreach(var name in new[]{"Displays","Tray icon","Clipboard signal",
                "Shared Deck","Excel","File search","Updater","Crash diagnostics"})
                Assert(names.Contains(name),"R175 status entry missing: "+name);
            var descriptions=string.Join(" ",rows.Select(x=>
                x.GetType().GetProperty("Detail")!.GetValue(x)!.ToString()));
            Assert(!descriptions.Contains(WorkspaceStore.Root,
                    StringComparison.OrdinalIgnoreCase),
                "R175 health panel must not reveal private workspace path");
            dashboard=(Window)create.Invoke(null,new object[]{owner,true})!;
            dashboard.Show();dashboard.UpdateLayout();
            Assert(dashboard.Title.Contains("Component status")&&
                Walk(dashboard).OfType<Button>().Any(x=>
                    x.Content?.ToString()=="Refresh")&&
                Walk(dashboard).OfType<Button>().Any(x=>
                    x.Content?.ToString()?.Contains("diagnostics")==true),
                "R175 on-demand status needs usable refresh/export controls");
            Assert(Walk(dashboard).OfType<TextBlock>().Any(x=>
                x.Text=="Clipboard signal"),
                "R175 clipboard signal must be present in the actual WPF view");
            Console.WriteLine("R175 PASS: native WPF local status dashboard, "+
                "privacy-safe module snapshot, refresh/export controls, "+
                "OS clipboard-change listener lifecycle.");
        }
        finally
        {
            dashboard?.Close();
            listener?.Dispose();
            owner.Close();
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'insert real status-window regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R175 added real WPF health dashboard regression after tray recovery.'
